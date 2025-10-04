using Adventures.Shared.Ftp.Interfaces;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Net;
using FluentFTP.Rules;

namespace Adventures.Shared.Ftp.Client
{
    public sealed class FluentFtpClientAsync : IFtpClientAsync
    {
        private readonly FtpClient _client;
        private readonly ILogger<FluentFtpClientAsync> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private bool _connected;

        public FluentFtpClientAsync(string host, string username, string password, ILogger<FluentFtpClientAsync> logger)
        {
            _logger = logger;
            _client = new FtpClient(host)
            {
                Credentials = new NetworkCredential(username, password)
            };

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(attempt),
                    (ex, ts, attempt, ctx) => _logger.LogWarning(ex, "Retry {Attempt} after {Delay}", attempt, ts));
        }

        // Connection lifecycle -------------------------------------------------
        public async Task ConnectAsync(CancellationToken token)
        {
            if (_connected && _client.IsConnected) return;
            await Task.Run(() => _client.Connect(), token);
            _connected = true;
            _logger.LogInformation("Connected to FTP server {Host}", _client.Host);
        }

        public Task DisconnectAsync()
        {
            try
            {
                if (_client.IsConnected) _client.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error during disconnect");
            }
            _connected = false;
            return Task.CompletedTask;
        }

        private async Task EnsureConnectedAsync(CancellationToken token) => await ConnectAsync(token);

        // Existence / metadata -------------------------------------------------
        public async Task<bool> FileExistsAsync(string remotePath, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            return await _retryPolicy.ExecuteAsync(ct => Task.Run(() => _client.FileExists(remotePath), ct), token);
        }

        public async Task<bool> DirectoryExistsAsync(string remotePath, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            return await _retryPolicy.ExecuteAsync(ct => Task.Run(() => _client.DirectoryExists(remotePath), ct), token);
        }

        public async Task<long?> GetFileSizeAsync(string remotePath, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            return await _retryPolicy.ExecuteAsync(ct => Task.Run(() =>
            {
                try { return (long?)_client.GetFileSize(remotePath); } catch { return null; }
            }, ct), token);
        }

        public async Task<DateTime?> GetModifiedTimeAsync(string remotePath, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            return await _retryPolicy.ExecuteAsync(ct => Task.Run(() =>
            {
                try { return (DateTime?)_client.GetModifiedTime(remotePath); } catch { return null; }
            }, ct), token);
        }

        // Listing --------------------------------------------------------------
        public async Task<IEnumerable<FtpListItem>> ListAsync(string path, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            return await _retryPolicy.ExecuteAsync(ct => Task.Run(() => _client.GetListing(path), ct), token);
        }

        public async Task<IEnumerable<string>> ListDirectoryAsync(string path, CancellationToken token)
        {
            var items = await ListAsync(path, token);
            return items.Select(i => i.FullName);
        }

        // Create / Delete ------------------------------------------------------
        public async Task CreateDirectoryAsync(string path, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            await _retryPolicy.ExecuteAsync(ct => Task.Run(() => _client.CreateDirectory(path, true), ct), token);
            _logger.LogInformation("Created directory {Path}", path);
        }

        public async Task DeleteDirectoryAsync(string path, CancellationToken token)
            => await DeleteDirectoryAsync(path, false, token);

        public async Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            await _retryPolicy.ExecuteAsync(ct => Task.Run(() =>
            {
                if (recursive)
                {
                    if (_client.DirectoryExists(path)) _client.DeleteDirectory(path, FtpListOption.Recursive);
                }
                else
                {
                    if (_client.DirectoryExists(path)) _client.DeleteDirectory(path);
                }
            }, ct), token);
            _logger.LogInformation("Deleted directory {Path} (Recursive={Recursive})", path, recursive);
        }

        public async Task DeleteFileAsync(string path, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            await _retryPolicy.ExecuteAsync(ct => Task.Run(() =>
            {
                if (_client.FileExists(path)) _client.DeleteFile(path);
            }, ct), token);
            _logger.LogInformation("Deleted file {Path}", path);
        }

        // Upload single / directory -------------------------------------------
        public async Task UploadFileAsync(string localPath, string remotePath, IProgress<FtpProgress>? progress, CancellationToken token, FtpRemoteExists existsMode = FtpRemoteExists.Overwrite, bool createRemoteDir = true)
        {
            await EnsureConnectedAsync(token);
            await _retryPolicy.ExecuteAsync(ct => Task.Run(() =>
            {
                _client.UploadFile(localPath, NormalizeRemote(remotePath), existsMode, createRemoteDir, FtpVerify.None, progress == null ? null : new Action<FtpProgress>(p => progress.Report(p)));
            }, ct), token);
            _logger.LogInformation("Uploaded {Local} -> {Remote}", localPath, remotePath);
        }

        public async Task UploadDirectoryAsync(string localDirectory, string remoteDirectory, IProgress<FtpProgress>? progress, CancellationToken token, FtpFolderSyncMode syncMode = FtpFolderSyncMode.Update, FtpRemoteExists existsMode = FtpRemoteExists.Overwrite, bool recursive = true)
        {
            await EnsureConnectedAsync(token);
            // NOTE: FluentFTP's UploadDirectory always processes recursively; to emulate non-recursive we would need rules. For now 'recursive' flag is informational.
            var progressAction = progress == null ? null : new Action<FtpProgress>(p => progress.Report(p));
            await _retryPolicy.ExecuteAsync(ct => Task.Run(() =>
            {
                _client.UploadDirectory(localDirectory, NormalizeRemote(remoteDirectory), syncMode, existsMode, FtpVerify.None, rules: null, progress: progressAction);
            }, ct), token);
            _logger.LogInformation("Uploaded directory {LocalDir} -> {RemoteDir} (Mode={Mode} ExistsMode={ExistsMode})", localDirectory, remoteDirectory, syncMode, existsMode);
        }

        // Download single / directory -----------------------------------------
        public async Task DownloadFileAsync(string localPath, string remotePath, IProgress<FtpProgress>? progress, CancellationToken token, FtpLocalExists existsMode = FtpLocalExists.Overwrite)
        {
            await EnsureConnectedAsync(token);
            await _retryPolicy.ExecuteAsync(ct => Task.Run(() =>
            {
                _client.DownloadFile(localPath, NormalizeRemote(remotePath), existsMode, FtpVerify.None, progress == null ? null : new Action<FtpProgress>(p => progress.Report(p)));
            }, ct), token);
            _logger.LogInformation("Downloaded {Remote} -> {Local}", remotePath, localPath);
        }

        public async Task DownloadDirectoryAsync(string localDirectory, string remoteDirectory, IProgress<FtpProgress>? progress, CancellationToken token, FtpLocalExists existsMode = FtpLocalExists.Overwrite, bool recursive = true)
        {
            await EnsureConnectedAsync(token);
            // NOTE: To limit recursion, rules could be supplied; for simplicity we ignore 'recursive' flag.
            var progressAction = progress == null ? null : new Action<FtpProgress>(p => progress.Report(p));
            await _retryPolicy.ExecuteAsync(ct => Task.Run(() =>
            {
                _client.DownloadDirectory(localDirectory, NormalizeRemote(remoteDirectory), FtpFolderSyncMode.Update, existsMode, FtpVerify.None, rules: null, progress: progressAction);
            }, ct), token);
            _logger.LogInformation("Downloaded directory {RemoteDir} -> {LocalDir}", remoteDirectory, localDirectory);
        }

        // Move / Rename --------------------------------------------------------
        public async Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            await _retryPolicy.ExecuteAsync(ct => Task.Run(() => _client.Rename(NormalizeRemote(sourcePath), NormalizeRemote(destinationPath)), ct), token);
            _logger.LogInformation("Moved file {Source} -> {Destination}", sourcePath, destinationPath);
        }

        public async Task MoveDirectoryAsync(string sourcePath, string destinationPath, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            // FluentFTP does not have a direct MoveDirectory that handles merging; use Rename for simple moves.
            await _retryPolicy.ExecuteAsync(ct => Task.Run(() => _client.Rename(NormalizeRemote(sourcePath), NormalizeRemote(destinationPath)), ct), token);
            _logger.LogInformation("Moved directory {Source} -> {Destination}", sourcePath, destinationPath);
        }

        // Batched parallel helpers (legacy) ------------------------------------
        public async Task UploadFilesAsync(IEnumerable<string> localPaths, string remotePath, IProgress<FtpProgress> progress, int maxParallel, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            using var throttler = new SemaphoreSlim(maxParallel);

            var tasks = localPaths.Select(async file =>
            {
                await throttler.WaitAsync(token);
                try
                {
                    var remoteFile = NormalizeRemote(CombineRemote(remotePath, Path.GetFileName(file)));
                    await _retryPolicy.ExecuteAsync(ct => Task.Run(() =>
                    {
                        _client.UploadFile(file, remoteFile, FtpRemoteExists.Overwrite, true, FtpVerify.None, new Action<FtpProgress>(p => progress?.Report(p)));
                    }, ct), token);
                    _logger.LogInformation("Uploaded {File} -> {Remote}", file, remoteFile);
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        public async Task DownloadFilesAsync(IEnumerable<(string RemotePath, string LocalPath)> files, int maxParallel, CancellationToken token)
        {
            await EnsureConnectedAsync(token);
            using var throttler = new SemaphoreSlim(maxParallel);

            var tasks = files.Select(async file =>
            {
                await throttler.WaitAsync(token);
                try
                {
                    await _retryPolicy.ExecuteAsync(ct => Task.Run(() =>
                    {
                        _client.DownloadFile(file.LocalPath, NormalizeRemote(file.RemotePath), FtpLocalExists.Overwrite, FtpVerify.None, null);
                    }, ct), token);
                    _logger.LogInformation("Downloaded {Remote} -> {Local}", file.RemotePath, file.LocalPath);
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        // Helpers --------------------------------------------------------------
        private static string NormalizeRemote(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "/";
            var p = path.Replace("\\", "/");
            if (!p.StartsWith('/')) p = "/" + p;
            return p.Replace("//", "/");
        }

        private static string CombineRemote(string left, string right)
        {
            left = NormalizeRemote(left);
            right = right.Replace("\\", "/").Trim('/');
            return NormalizeRemote(left.TrimEnd('/') + "/" + right);
        }

        // Disposal -------------------------------------------------------------
        public async ValueTask DisposeAsync()
        {
            try
            {
                await DisconnectAsync();
                _client.Dispose();
            }
            catch { /* ignored */ }
        }
    }
}
