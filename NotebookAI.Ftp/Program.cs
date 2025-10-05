using System.Net;
using System.Text.RegularExpressions;
using Adventures.Shared.Ftp.Interfaces;
using Adventures.Shared.Ftp.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FluentFTP;

namespace NotebookAI.Ftp
{
    internal class Program
    {
        private record FtpSettings(string RemoteFolder);

        public static async Task<int> Main(string[] args)
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
            var token = cts.Token;

            try
            {
                var host = Host.CreateDefaultBuilder(args)
                    .ConfigureAppConfiguration((ctx, cfg) =>
                    {
                        cfg.AddUserSecrets<Program>(optional: true);
                    })
                    .ConfigureServices((ctx, services) =>
                    {
                        services.AddFtp(ctx.Configuration, "Ftp", ServiceLifetime.Scoped); // Scoped with pooling
                    })
                    .ConfigureLogging(lb => lb.AddConsole())
                    .Build();

                var sourcePath = args?.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                    ? args[0]
                    : FindDefaultSourcePath();

                var config = host.Services.GetRequiredService<IConfiguration>();
                var remoteSettings = GetFtpSettings(config);
                var baseDir = NormalizeBase(remoteSettings.RemoteFolder);
                var slot1 = CombineRemote(baseDir, "slot1"); // staging
                var slot2 = CombineRemote(baseDir, "slot2"); // backup

                // IMPORTANT: use asynchronous scope disposal because pooled FTP client only implements IAsyncDisposable
                await using var scope = host.Services.CreateAsyncScope();
                var ftp = scope.ServiceProvider.GetRequiredService<IFtpClientAsync>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                await ftp.ConnectAsync(token);

                logger.LogInformation("Deploying from '{Source}' to '{Base}' (staging: {Staging}, backup: {Backup})", sourcePath, baseDir, slot1, slot2);

                // Ensure base + slots exist
                await EnsureDirAsync(ftp, baseDir, token);
                await EnsureDirAsync(ftp, slot1, token);
                await EnsureDirAsync(ftp, slot2, token);

                // Clean staging slot only
                await SafeCleanDirectoryAsync(ftp, slot1, token);

                // Upload to staging with progress
                int parallelism = GetParallelism(config);
                logger.LogInformation("Uploading to staging with parallelism={Parallelism} ...", parallelism);
                var uploadProgress = new Progress<FtpProgress>(p =>
                {
                    if (p.Progress >= 0)
                    {
                        logger.LogInformation("UPLOAD {Percent,6:F2}% {Local}", p.Progress, p.LocalPath);
                    }
                });
                // Using directory upload (FluentFTP internally parallelizes). For explicit parallel control we could enumerate files & use UploadFilesAsync.
                await ftp.UploadDirectoryAsync(sourcePath, slot1, progress: uploadProgress, token: token);

                // Validate staging
                var stagingItems = await ftp.ListAsync(slot1, token);
                if (!stagingItems.Any(i => i.Type == FtpObjectType.File))
                    throw new InvalidOperationException("Staging upload validation failed: no files found.");
                logger.LogInformation("Staging upload validated: {Count} items", stagingItems.Count());

                // Backup current production
                logger.LogInformation("Backing up current production to backup slot...");
                await BackupCurrentAsync(ftp, baseDir, slot1, slot2, token, logger);

                // Promote staging
                logger.LogInformation("Promoting staging content to production...");
                await PromoteStagingAsync(ftp, slot1, baseDir, token, logger);

                // Post-deploy health checks
                logger.LogInformation("Running post-deployment health checks...");
                var healthOk = await RunHealthChecksAsync(ftp, baseDir, config, token, logger);
                if (!healthOk)
                {
                    logger.LogError("Health checks FAILED. Initiating rollback.");
                    await RollbackFromBackupAsync(ftp, baseDir, slot1, slot2, token, logger);
                    logger.LogError("Rollback completed. Deployment marked as failed.");
                    return 1;
                }

                logger.LogInformation("Health checks passed.");

                // Clean staging only after success
                await SafeCleanDirectoryAsync(ftp, slot1, token);

                logger.LogInformation("Deployment succeeded.");
                return 0;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("[CANCELLED] Deployment cancelled by user.");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ERROR] Deployment failed: " + ex.Message);
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static int GetParallelism(IConfiguration config)
        {
            var value = config["Deployment:Parallelism"];
            if (int.TryParse(value, out var p) && p > 0 && p <= 64) return p;
            return 4;
        }

        private static async Task EnsureDirAsync(IFtpClientAsync ftp, string path, CancellationToken token)
        {
            if (!await ftp.DirectoryExistsAsync(path, token))
            {
                await ftp.CreateDirectoryAsync(path, token);
            }
        }

        private static async Task BackupCurrentAsync(IFtpClientAsync ftp, string baseDir, string stagingSlot, string backupSlot, CancellationToken token, ILogger logger)
        {
            await SafeCleanDirectoryAsync(ftp, backupSlot, token);

            var items = await ftp.ListAsync(baseDir, token);
            var movedRecords = new List<(string From, string To, bool Dir)>();

            foreach (var item in items)
            {
                if (IsSlot(item, stagingSlot) || IsSlot(item, backupSlot)) continue;
                var source = item.FullName;
                var dest = CombineRemote(backupSlot, TrimBase(baseDir, item.FullName));
                try
                {
                    if (item.Type == FtpObjectType.Directory)
                    {
                        await ftp.MoveDirectoryAsync(source, dest, token);
                        movedRecords.Add((source, dest, true));
                        logger.LogDebug("Backed up directory {Source} -> {Dest}", source, dest);
                    }
                    else if (item.Type == FtpObjectType.File)
                    {
                        await ftp.MoveFileAsync(source, dest, token);
                        movedRecords.Add((source, dest, false));
                        logger.LogDebug("Backed up file {Source} -> {Dest}", source, dest);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error backing up {Source} -> {Dest}; rolling back partial backup", source, dest);
                    await RollbackMovesAsync(ftp, movedRecords, token, logger);
                    throw;
                }
            }
        }

        private static async Task PromoteStagingAsync(IFtpClientAsync ftp, string stagingSlot, string baseDir, CancellationToken token, ILogger logger)
        {
            var items = await ftp.ListAsync(stagingSlot, token);
            var movedIn = new List<(string From, string To, bool Dir)>();

            foreach (var item in items)
            {
                var source = item.FullName;
                var dest = CombineRemote(baseDir, TrimBase(stagingSlot, item.FullName));
                try
                {
                    if (item.Type == FtpObjectType.Directory)
                    {
                        await ftp.MoveDirectoryAsync(source, dest, token);
                        movedIn.Add((source, dest, true));
                        logger.LogDebug("Promoted directory {Source} -> {Dest}", source, dest);
                    }
                    else if (item.Type == FtpObjectType.File)
                    {
                        await ftp.MoveFileAsync(source, dest, token);
                        movedIn.Add((source, dest, false));
                        logger.LogDebug("Promoted file {Source} -> {Dest}", source, dest);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error promoting {Source} -> {Dest}; attempting rollback to staging", source, dest);
                    foreach (var m in movedIn.AsEnumerable().Reverse())
                    {
                        try
                        {
                            if (m.Dir) await ftp.MoveDirectoryAsync(m.To, m.From, token);
                            else await ftp.MoveFileAsync(m.To, m.From, token);
                        }
                        catch { }
                    }
                    throw;
                }
            }
        }

        private static async Task RollbackMovesAsync(IFtpClientAsync ftp, List<(string From, string To, bool Dir)> moved, CancellationToken token, ILogger logger)
        {
            foreach (var entry in moved.AsEnumerable().Reverse())
            {
                try
                {
                    if (entry.Dir) await ftp.MoveDirectoryAsync(entry.To, entry.From, token);
                    else await ftp.MoveFileAsync(entry.To, entry.From, token);
                    logger.LogDebug("Rollback move {To} -> {From}", entry.To, entry.From);
                }
                catch { }
            }
        }

        private static async Task<bool> RunHealthChecksAsync(IFtpClientAsync ftp, string baseDir, IConfiguration config, CancellationToken token, ILogger logger)
        {
            // Configurable required file list (relative to base)
            var required = config.GetSection("Deployment:HealthCheck:Paths").Get<string[]>() ?? Array.Empty<string>();
            if (required.Length == 0)
            {
                // Default heuristic: ensure index.html exists if typical SPA
                required = new[] { "index.html" };
            }

            foreach (var rel in required)
            {
                var remote = CombineRemote(baseDir, rel);
                var exists = await ftp.FileExistsAsync(remote, token);
                logger.LogInformation("HealthCheck: {Path} exists={Exists}", remote, exists);
                if (!exists) return false;
            }
            return true;
        }

        private static async Task RollbackFromBackupAsync(IFtpClientAsync ftp, string baseDir, string stagingSlot, string backupSlot, CancellationToken token, ILogger logger)
        {
            logger.LogInformation("Starting rollback: restoring backup slot to production.");
            // Move current (failed) production content to staging (clean staging first)
            await SafeCleanDirectoryAsync(ftp, stagingSlot, token);
            var prodItems = await ftp.ListAsync(baseDir, token);
            foreach (var item in prodItems)
            {
                if (IsSlot(item, stagingSlot) || IsSlot(item, backupSlot)) continue;
                var source = item.FullName;
                var dest = CombineRemote(stagingSlot, TrimBase(baseDir, item.FullName));
                try
                {
                    if (item.Type == FtpObjectType.Directory) await ftp.MoveDirectoryAsync(source, dest, token);
                    else if (item.Type == FtpObjectType.File) await ftp.MoveFileAsync(source, dest, token);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed moving failed deployment item {Source} -> {Dest}", source, dest);
                }
            }
            // Move backup back to base
            var backupItems = await ftp.ListAsync(backupSlot, token);
            foreach (var item in backupItems)
            {
                var source = item.FullName;
                var dest = CombineRemote(baseDir, TrimBase(backupSlot, item.FullName));
                try
                {
                    if (item.Type == FtpObjectType.Directory) await ftp.MoveDirectoryAsync(source, dest, token);
                    else if (item.Type == FtpObjectType.File) await ftp.MoveFileAsync(source, dest, token);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed restoring backup item {Source} -> {Dest}", source, dest);
                }
            }
            logger.LogInformation("Rollback restore complete.");
        }

        private static bool IsSlot(FtpListItem item, string slotPath) => item.FullName.Equals(slotPath, StringComparison.OrdinalIgnoreCase);

        private static string TrimBase(string baseDir, string fullPath)
        {
            if (fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                var trimmed = fullPath.Substring(baseDir.Length).Trim('/');
                return trimmed;
            }
            return fullPath.Trim('/');
        }

        private static async Task SafeCleanDirectoryAsync(IFtpClientAsync ftp, string path, CancellationToken token)
        {
            if (!await ftp.DirectoryExistsAsync(path, token))
            {
                await ftp.CreateDirectoryAsync(path, token);
                return;
            }
            await ftp.DeleteDirectoryAsync(path, recursive: true, token);
            await ftp.CreateDirectoryAsync(path, token);
        }

        private static FtpSettings GetFtpSettings(IConfiguration configuration)
        {
            var section = configuration.GetSection("Ftp");
            var remoteFolder = section["remote-folder"] ?? section["RemoteFolder"] ?? "/";
            return new FtpSettings(remoteFolder);
        }

        private static string NormalizeBase(string? path)
        {
            var p = string.IsNullOrWhiteSpace(path) ? "/" : path!.Trim();
            if (!p.StartsWith('/')) p = "/" + p;
            if (p.Length > 1) p = p.TrimEnd('/');
            return p;
        }

        private static string CombineRemote(params string[] parts)
        {
            var combined = string.Join('/', parts.Select(p => p.Trim('/')));
            if (!combined.StartsWith('/')) combined = "/" + combined;
            return combined.Replace("//", "/");
        }

        private static string FindDefaultSourcePath()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "notebookai.client", "dist", "notebookai.client"),
                Path.Combine(Environment.CurrentDirectory, "..", "notebookai.client", "dist", "notebookai.client"),
                Path.Combine(Environment.CurrentDirectory, "notebookai.client", "dist", "notebookai.client"),
                Path.Combine(Environment.CurrentDirectory, "dist", "notebookai.client"),
            };
            foreach (var p in candidates)
            {
                var full = Path.GetFullPath(p);
                if (Directory.Exists(full)) return full;
            }
            throw new DirectoryNotFoundException("Could not find build output folder 'dist\\notebookai.client'. Pass the path as the first argument.");
        }
    }
}
