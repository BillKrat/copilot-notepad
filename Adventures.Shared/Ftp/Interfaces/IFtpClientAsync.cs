using FluentFTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adventures.Shared.Ftp.Interfaces
{
    /// <summary>
    /// Abstraction over common FluentFTP operations with cancellation + retry handled by implementation.
    /// Existing minimal methods kept for backward compatibility; prefer newer, more granular methods.
    /// </summary>
    public interface IFtpClientAsync : IAsyncDisposable
    {
        // Connection lifecycle
        Task ConnectAsync(CancellationToken token);
        Task DisconnectAsync();

        // Metadata / existence
        Task<bool> FileExistsAsync(string remotePath, CancellationToken token);
        Task<bool> DirectoryExistsAsync(string remotePath, CancellationToken token);
        Task<long?> GetFileSizeAsync(string remotePath, CancellationToken token);
        Task<DateTime?> GetModifiedTimeAsync(string remotePath, CancellationToken token);

        // Listing
        Task<IEnumerable<FtpListItem>> ListAsync(string path, CancellationToken token);
        Task<IEnumerable<string>> ListDirectoryAsync(string path, CancellationToken token); // legacy simplified listing

        // Create / delete
        Task CreateDirectoryAsync(string path, CancellationToken token);
        Task DeleteDirectoryAsync(string path, CancellationToken token); // legacy non-recursive
        Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken token);
        Task DeleteFileAsync(string path, CancellationToken token);

        // Upload single / directory
        Task UploadFileAsync(
            string localPath,
            string remotePath,
            IProgress<FtpProgress>? progress,
            CancellationToken token,
            FtpRemoteExists existsMode = FtpRemoteExists.Overwrite,
            bool createRemoteDir = true);

        Task UploadDirectoryAsync(
            string localDirectory,
            string remoteDirectory,
            IProgress<FtpProgress>? progress,
            CancellationToken token,
            FtpFolderSyncMode syncMode = FtpFolderSyncMode.Update,
            FtpRemoteExists existsMode = FtpRemoteExists.Overwrite,
            bool recursive = true);

        // Download single / directory
        Task DownloadFileAsync(
            string localPath,
            string remotePath,
            IProgress<FtpProgress>? progress,
            CancellationToken token,
            FtpLocalExists existsMode = FtpLocalExists.Overwrite);

        Task DownloadDirectoryAsync(
            string localDirectory,
            string remoteDirectory,
            IProgress<FtpProgress>? progress,
            CancellationToken token,
            FtpLocalExists existsMode = FtpLocalExists.Overwrite,
            bool recursive = true);

        // Move / rename
        Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken token);
        Task MoveDirectoryAsync(string sourcePath, string destinationPath, CancellationToken token);

        // Batched parallel helpers (existing)
        Task UploadFilesAsync(IEnumerable<string> localPaths, string remotePath, IProgress<FtpProgress> progress, int maxParallel, CancellationToken token);
        Task DownloadFilesAsync(IEnumerable<(string RemotePath, string LocalPath)> files, int maxParallel, CancellationToken token);
    }
}
