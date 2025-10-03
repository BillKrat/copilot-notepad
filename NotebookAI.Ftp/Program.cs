using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace NotebookAI.Ftp
{
    internal class Program
    {
        private record FtpSettings(string Host, int Port, string Username, string Password, string RemoteFolder);

        static int Main(string[] args)
        {
            try
            {
                var sourcePath = args?.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                    ? args[0]
                    : FindDefaultSourcePath();

                var ftp = GetFtpSettings();
                var baseDir = NormalizeBase(ftp.RemoteFolder);

                Console.WriteLine($"[INFO] Deploying from '{sourcePath}' to ftp://{ftp.Host}:{ftp.Port}{baseDir} ...");

                // Ensure base directory exists
                EnsureDirectoryExists(ftp, baseDir);

                var slot1 = CombineRemote(baseDir, "slot1");
                var slot2 = CombineRemote(baseDir, "slot2");

                // Ensure slot directories are present and empty
                EnsureDirectoryExists(ftp, slot1);
                CleanDirectory(ftp, slot1);

                EnsureDirectoryExists(ftp, slot2);
                CleanDirectory(ftp, slot2);

                // Upload dist to slot1
                UploadDirectoryRecursive(ftp, sourcePath, slot1);

                // Move existing baseDir content to slot2
                MoveContentsTo(ftp, baseDir, slot2, exclude: new[] { "slot1", "slot2" });

                // Move from slot1 to baseDir (finalize)
                MoveDirectoryContents(ftp, slot1, baseDir);

                Console.WriteLine("[SUCCESS] Deployment completed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[ERROR] " + ex.Message);
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        private static FtpSettings GetFtpSettings()
        {
            var config = new ConfigurationBuilder()
                .AddUserSecrets(typeof(Program).Assembly, optional: false)
                .Build();

            var section = config.GetSection("Ftp");
            var host = section["host"] ?? throw new InvalidOperationException("User secrets missing: Ftp:host");
            var portStr = section["port"] ?? "21";
            var user = section["username"] ?? throw new InvalidOperationException("User secrets missing: Ftp:username");
            var pass = section["password"] ?? throw new InvalidOperationException("User secrets missing: Ftp:password");
            var remoteFolder = section["remote-folder"] ?? "/";
            if (!int.TryParse(portStr, out var port)) port = 21;
            return new FtpSettings(host, port, user, pass, remoteFolder);
        }

        private static string NormalizeBase(string? path)
        {
            var p = string.IsNullOrWhiteSpace(path) ? "/" : path!.Trim();
            // allow either "/global" or "global" in settings
            if (!p.StartsWith('/')) p = "/" + p;
            // remove trailing slash (except root)
            if (p.Length > 1) p = p.TrimEnd('/');
            return p;
        }

        private static string FindDefaultSourcePath()
        {
            var candidates = new []
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

        private static string CombineRemote(params string[] parts)
        {
            var combined = string.Join('/', parts.Select(p => p.Trim('/')));
            if (!combined.StartsWith('/')) combined = "/" + combined;
            return combined.Replace("//", "/");
        }

        private static FtpWebRequest CreateRequest(FtpSettings ftp, string method, string remotePath)
        {
            var uriBuilder = new UriBuilder("ftp", ftp.Host, ftp.Port, remotePath);
            var request = (FtpWebRequest)WebRequest.Create(uriBuilder.Uri);
            request.Method = method;
            request.Credentials = new NetworkCredential(ftp.Username, ftp.Password);
            request.EnableSsl = false; // change if FTPS is required
            request.UseBinary = true;
            request.KeepAlive = false;
            request.ReadWriteTimeout = 30000;
            request.Timeout = 30000;
            return request;
        }

        private static bool DirectoryExists(FtpSettings ftp, string remotePath)
        {
            try
            {
                var req = CreateRequest(ftp, WebRequestMethods.Ftp.ListDirectory, remotePath);
                using var resp = (FtpWebResponse)req.GetResponse();
                return true;
            }
            catch (WebException ex)
            {
                if (ex.Response is FtpWebResponse ftpResp &&
                    (ftpResp.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable ||
                     ftpResp.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailableOrBusy))
                {
                    return false;
                }
                return false;
            }
        }

        private static void EnsureDirectoryExists(FtpSettings ftp, string remotePath)
        {
            if (DirectoryExists(ftp, remotePath)) return;
            var req = CreateRequest(ftp, WebRequestMethods.Ftp.MakeDirectory, remotePath);
            using var resp = (FtpWebResponse)req.GetResponse();
        }

        private static IEnumerable<(string Name, string FullPath, bool IsDirectory)> ListDirectoryDetails(FtpSettings ftp, string remotePath)
        {
            var req = CreateRequest(ftp, WebRequestMethods.Ftp.ListDirectoryDetails, remotePath);
            using var resp = (FtpWebResponse)req.GetResponse();
            using var stream = resp.GetResponseStream()!;
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                lines.Add(reader.ReadLine()!);
            }

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Try Windows/IIS format first: 06-16-24  03:25PM       <DIR>  wwwroot OR 06-16-24  03:25PM            123 file.txt
                var winMatch = Regex.Match(line, @"^(?<date>\d{2}-\d{2}-\d{2,4})\s+(?<time>\d{2}:\d{2}(AM|PM))\s+(?<dir><DIR>|\d+)\s+(?<name>.+)$");
                if (winMatch.Success)
                {
                    var name = winMatch.Groups["name"].Value.Trim();
                    var isDir = string.Equals(winMatch.Groups["dir"].Value, "<DIR>", StringComparison.OrdinalIgnoreCase);
                    var full = CombineRemote(remotePath, name);
                    yield return (name, full, isDir);
                    continue;
                }

                // Fallback to Unix format: drwxr-xr-x 1 owner group 0 Jan 01 00:00 dirname
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 9)
                {
                    bool isDir = parts[0][0] == 'd';
                    var name = string.Join(' ', parts.Skip(8));
                    var full = CombineRemote(remotePath, name);
                    yield return (name, full, isDir);
                    continue;
                }
            }
        }

        private static void DeleteFile(FtpSettings ftp, string remotePath)
        {
            var req = CreateRequest(ftp, WebRequestMethods.Ftp.DeleteFile, remotePath);
            using var resp = (FtpWebResponse)req.GetResponse();
        }

        private static void DeleteDirectoryRecursive(FtpSettings ftp, string remotePath)
        {
            foreach (var item in SafeList(ftp, remotePath))
            {
                if (item.IsDirectory)
                {
                    DeleteDirectoryRecursive(ftp, item.FullPath);
                }
                else
                {
                    Try(() => DeleteFile(ftp, item.FullPath));
                }
            }
            var req = CreateRequest(ftp, WebRequestMethods.Ftp.RemoveDirectory, remotePath);
            using var resp = (FtpWebResponse)req.GetResponse();
        }

        private static IEnumerable<(string Name, string FullPath, bool IsDirectory)> SafeList(FtpSettings ftp, string remotePath)
        {
            try { return ListDirectoryDetails(ftp, remotePath).ToArray(); }
            catch { return Array.Empty<(string, string, bool)>(); }
        }

        private static void CleanDirectory(FtpSettings ftp, string remotePath)
        {
            foreach (var item in SafeList(ftp, remotePath))
            {
                try
                {
                    if (item.IsDirectory)
                    {
                        DeleteDirectoryRecursive(ftp, item.FullPath);
                    }
                    else
                    {
                        DeleteFile(ftp, item.FullPath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Failed to delete '{item.FullPath}': {ex.Message}");
                }
            }
        }

        private static void EnsureRemoteDirectoryTree(FtpSettings ftp, string remoteDir)
        {
            var parts = remoteDir.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var path = "/";
            foreach (var part in parts)
            {
                path = CombineRemote(path, part);
                if (!DirectoryExists(ftp, path))
                {
                    Try(() => EnsureDirectoryExists(ftp, path));
                }
            }
        }

        private static void UploadFile(FtpSettings ftp, string localFile, string remoteFile)
        {
            EnsureRemoteDirectoryTree(ftp, Path.GetDirectoryName(remoteFile)?.Replace('\\', '/') ?? "/");
            var req = CreateRequest(ftp, WebRequestMethods.Ftp.UploadFile, remoteFile);
            using var reqStream = req.GetRequestStream();
            using var fs = new FileStream(localFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.CopyTo(reqStream);
            using var resp = (FtpWebResponse)req.GetResponse();
        }

        private static void UploadBytes(FtpSettings ftp, byte[] bytes, string remoteFile)
        {
            EnsureRemoteDirectoryTree(ftp, Path.GetDirectoryName(remoteFile)?.Replace('\\', '/') ?? "/");
            var req = CreateRequest(ftp, WebRequestMethods.Ftp.UploadFile, remoteFile);
            using var reqStream = req.GetRequestStream();
            reqStream.Write(bytes, 0, bytes.Length);
            using var resp = (FtpWebResponse)req.GetResponse();
        }

        private static byte[] DownloadBytes(FtpSettings ftp, string remoteFile)
        {
            var req = CreateRequest(ftp, WebRequestMethods.Ftp.DownloadFile, remoteFile);
            using var resp = (FtpWebResponse)req.GetResponse();
            using var stream = resp.GetResponseStream()!;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static void CopyFile(FtpSettings ftp, string sourceFile, string destFile)
        {
            var bytes = DownloadBytes(ftp, sourceFile);
            UploadBytes(ftp, bytes, destFile);
        }

        private static void CopyDirectoryRecursive(FtpSettings ftp, string sourceDir, string destDir)
        {
            EnsureRemoteDirectoryTree(ftp, destDir);
            foreach (var item in SafeList(ftp, sourceDir))
            {
                var destPath = CombineRemote(destDir, item.Name);
                if (item.IsDirectory)
                {
                    CopyDirectoryRecursive(ftp, item.FullPath, destPath);
                }
                else
                {
                    CopyFile(ftp, item.FullPath, destPath);
                }
            }
        }

        private static void UploadDirectoryRecursive(FtpSettings ftp, string localDir, string remoteDir)
        {
            foreach (var dir in Directory.GetDirectories(localDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(localDir, dir).Replace('\\', '/');
                EnsureRemoteDirectoryTree(ftp, CombineRemote(remoteDir, rel));
            }
            foreach (var file in Directory.GetFiles(localDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(localDir, file).Replace('\\', '/');
                var remoteFile = CombineRemote(remoteDir, rel);
                Console.WriteLine($"[INFO] Uploading {rel}");
                Try(() => UploadFile(ftp, file, remoteFile));
            }
        }

        private static void Rename(FtpSettings ftp, string from, string to)
        {
            var req = CreateRequest(ftp, WebRequestMethods.Ftp.Rename, from);
            req.RenameTo = to;
            using var resp = (FtpWebResponse)req.GetResponse();
        }

        private static void MoveWithFallback(FtpSettings ftp, string sourcePath, string destPath, bool isDirectory)
        {
            try
            {
                Rename(ftp, sourcePath, destPath);
            }
            catch
            {
                // Fallback: copy then delete
                if (isDirectory)
                {
                    CopyDirectoryRecursive(ftp, sourcePath, destPath);
                    Try(() => DeleteDirectoryRecursive(ftp, sourcePath));
                }
                else
                {
                    CopyFile(ftp, sourcePath, destPath);
                    Try(() => DeleteFile(ftp, sourcePath));
                }
            }
        }

        private static void MoveContentsTo(FtpSettings ftp, string sourceDir, string targetDir, IEnumerable<string>? exclude = null)
        {
            exclude ??= Array.Empty<string>();
            var excludeSet = new HashSet<string>(exclude, StringComparer.OrdinalIgnoreCase);

            foreach (var item in SafeList(ftp, sourceDir))
            {
                var name = item.Name.Trim('/');
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (excludeSet.Contains(name)) continue;

                var sourcePath = item.FullPath;
                var destPath = CombineRemote(targetDir, name);

                try
                {
                    if (item.IsDirectory)
                    {
                        EnsureDirectoryExists(ftp, destPath);
                    }
                    MoveWithFallback(ftp, sourcePath, destPath, item.IsDirectory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Failed to move '{sourcePath}' -> '{destPath}': {ex.Message}");
                }
            }
            Console.WriteLine($"[INFO] Moved contents from {sourceDir} to {targetDir}");
        }

        private static void MoveDirectoryContents(FtpSettings ftp, string sourceDir, string targetDir)
        {
            foreach (var item in SafeList(ftp, sourceDir))
            {
                var name = item.Name.Trim('/');
                if (string.IsNullOrWhiteSpace(name)) continue;

                var sourcePath = item.FullPath;
                var destPath = CombineRemote(targetDir, name);

                try
                {
                    if (item.IsDirectory)
                    {
                        EnsureDirectoryExists(ftp, destPath);
                    }
                    MoveWithFallback(ftp, sourcePath, destPath, item.IsDirectory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Failed to move '{sourcePath}' -> '{destPath}': {ex.Message}");
                }
            }
        }

        private static void Try(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                Console.WriteLine("[WARN] " + ex.Message);
            }
        }
    }
}
