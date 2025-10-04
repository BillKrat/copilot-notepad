using Adventures.Shared.Ftp.Interfaces;
using FluentFTP;

namespace Adventures.Shared.Ftp.Client
{
    public class MockFtpClientAsync : IFtpClientAsync
    {
        private bool _connected;

        public List<string> CreatedDirectories = new();
        public List<string> DeletedDirectories = new();
        public List<(string Source, string Destination)> MovedFiles = new();
        public List<(string Remote, string Local)> Downloads = new();
        public List<string> UploadedFiles = new();
        public List<string> DeletedFiles = new();

        // Simple in-memory sets for existence simulation
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase) { "/" };
        private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);

        private static string Normalize(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "/";
            var x = p.Replace("\\", "/");
            if (!x.StartsWith('/')) x = "/" + x;
            return x.Replace("//", "/");
        }

        public Task ConnectAsync(CancellationToken token)
        {
            _connected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            _connected = false;
            return Task.CompletedTask;
        }

        public Task<bool> FileExistsAsync(string remotePath, CancellationToken token)
            => Task.FromResult(_files.Contains(Normalize(remotePath)));

        public Task<bool> DirectoryExistsAsync(string remotePath, CancellationToken token)
            => Task.FromResult(_directories.Contains(Normalize(remotePath)));

        public Task<long?> GetFileSizeAsync(string remotePath, CancellationToken token)
            => Task.FromResult<long?>(_files.Contains(Normalize(remotePath)) ? 0 : null);

        public Task<DateTime?> GetModifiedTimeAsync(string remotePath, CancellationToken token)
            => Task.FromResult<DateTime?>(_files.Contains(Normalize(remotePath)) ? DateTime.UtcNow : null);

        public Task<IEnumerable<FtpListItem>> ListAsync(string path, CancellationToken token)
        {
            // Minimal mock: return empty listing (could be extended later)
            return Task.FromResult<IEnumerable<FtpListItem>>(Array.Empty<FtpListItem>());
        }

        public Task<IEnumerable<string>> ListDirectoryAsync(string path, CancellationToken token)
        {
            return Task.FromResult<IEnumerable<string>>(new[] { $"{Normalize(path)}/file1.txt", $"{Normalize(path)}/file2.txt" });
        }

        public Task CreateDirectoryAsync(string path, CancellationToken token)
        {
            var n = Normalize(path);
            CreatedDirectories.Add(n);
            _directories.Add(n);
            return Task.CompletedTask;
        }

        public Task DeleteDirectoryAsync(string path, CancellationToken token)
            => DeleteDirectoryAsync(path, false, token);

        public Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken token)
        {
            var n = Normalize(path);
            DeletedDirectories.Add(n);
            if (recursive)
            {
                var toRemove = _directories.Where(d => d.StartsWith(n, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var d in toRemove) _directories.Remove(d);
                var fileRemove = _files.Where(f => f.StartsWith(n + "/", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var f in fileRemove) _files.Remove(f);
            }
            _directories.Remove(n);
            return Task.CompletedTask;
        }

        public Task DeleteFileAsync(string path, CancellationToken token)
        {
            var n = Normalize(path);
            DeletedFiles.Add(n);
            _files.Remove(n);
            return Task.CompletedTask;
        }

        public Task UploadFileAsync(string localPath, string remotePath, IProgress<FtpProgress>? progress, CancellationToken token, FtpRemoteExists existsMode = FtpRemoteExists.Overwrite, bool createRemoteDir = true)
        {
            var n = Normalize(remotePath);
            UploadedFiles.Add(localPath);
            _files.Add(n);
            progress?.Report(FtpProgress.Generate(0, 0, 0, TimeSpan.Zero, localPath, n, null));
            return Task.CompletedTask;
        }

        public Task UploadDirectoryAsync(string localDirectory, string remoteDirectory, IProgress<FtpProgress>? progress, CancellationToken token, FtpFolderSyncMode syncMode = FtpFolderSyncMode.Update, FtpRemoteExists existsMode = FtpRemoteExists.Overwrite, bool recursive = true)
        {
            var n = Normalize(remoteDirectory);
            _directories.Add(n);
            CreatedDirectories.Add(n);
            progress?.Report(FtpProgress.Generate(0, 0, 0, TimeSpan.Zero, localDirectory, n, null));
            return Task.CompletedTask;
        }

        public Task DownloadFileAsync(string localPath, string remotePath, IProgress<FtpProgress>? progress, CancellationToken token, FtpLocalExists existsMode = FtpLocalExists.Overwrite)
        {
            Downloads.Add((Normalize(remotePath), localPath));
            progress?.Report(FtpProgress.Generate(0, 0, 0, TimeSpan.Zero, localPath, remotePath, null));
            return Task.CompletedTask;
        }

        public Task DownloadDirectoryAsync(string localDirectory, string remoteDirectory, IProgress<FtpProgress>? progress, CancellationToken token, FtpLocalExists existsMode = FtpLocalExists.Overwrite, bool recursive = true)
        {
            Downloads.Add((Normalize(remoteDirectory), localDirectory));
            progress?.Report(FtpProgress.Generate(0, 0, 0, TimeSpan.Zero, localDirectory, remoteDirectory, null));
            return Task.CompletedTask;
        }

        public Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken token)
        {
            var src = Normalize(sourcePath);
            var dst = Normalize(destinationPath);
            MovedFiles.Add((src, dst));
            if (_files.Remove(src)) _files.Add(dst);
            return Task.CompletedTask;
        }

        public Task MoveDirectoryAsync(string sourcePath, string destinationPath, CancellationToken token)
        {
            var src = Normalize(sourcePath);
            var dst = Normalize(destinationPath);
            if (_directories.Contains(src))
            {
                _directories.Remove(src);
                _directories.Add(dst);
            }
            return Task.CompletedTask;
        }

        public Task UploadFilesAsync(IEnumerable<string> localPaths, string remotePath, IProgress<FtpProgress> progress, int maxParallel, CancellationToken token)
        {
            foreach (var file in localPaths)
            {
                UploadedFiles.Add(file);
                progress?.Report(FtpProgress.Generate(0, 0, 0, TimeSpan.Zero, file, remotePath, null));
            }
            return Task.CompletedTask;
        }

        public Task DownloadFilesAsync(IEnumerable<(string RemotePath, string LocalPath)> files, int maxParallel, CancellationToken token)
        {
            Downloads.AddRange(files.Select(f => (Normalize(f.RemotePath), f.LocalPath)));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _connected = false;
            return ValueTask.CompletedTask;
        }
    }
}
