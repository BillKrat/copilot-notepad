using System.Collections.Concurrent;
using Adventures.Shared.Ftp.Client;
using Adventures.Shared.Ftp.Interfaces;
using Adventures.Shared.Ftp.Extensions; // for FtpClientOptions
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Adventures.Shared.Ftp.Pooling;

internal sealed class FtpClientPool
{
    private readonly ConcurrentBag<FluentFtpClientAsync> _clients = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxSize;
    private readonly IOptions<FtpClientOptions> _options;
    private readonly ILoggerFactory _loggerFactory;

    public FtpClientPool(IOptions<FtpClientOptions> options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _maxSize = Math.Max(1, options.Value.PoolSize);
        _semaphore = new SemaphoreSlim(_maxSize, _maxSize);
    }

    public async Task<FluentFtpClientAsync> AcquireAsync(CancellationToken token)
    {
        await _semaphore.WaitAsync(token).ConfigureAwait(false);
        if (_clients.TryTake(out var client))
        {
            return client; // reuse existing instance
        }
        var o = _options.Value;
        var logger = _loggerFactory.CreateLogger<FluentFtpClientAsync>();
        return new FluentFtpClientAsync(o.Host, o.Username, o.Password, logger);
    }

    public void Release(FluentFtpClientAsync client)
    {
        _clients.Add(client);
        _semaphore.Release();
    }
}

internal sealed class PooledFtpClientAsync : IFtpClientAsync
{
    private readonly FtpClientPool _pool;
    private FluentFtpClientAsync? _inner;
    private int _disposed;

    public PooledFtpClientAsync(FtpClientPool pool) => _pool = pool;

    private async ValueTask<FluentFtpClientAsync> GetInnerAsync(CancellationToken token)
    {
        if (_inner != null) return _inner;
        _inner = await _pool.AcquireAsync(token).ConfigureAwait(false);
        return _inner;
    }

    private FluentFtpClientAsync RequireInner() => _inner ?? throw new InvalidOperationException("FTP client not yet acquired. Call ConnectAsync first or any method with CancellationToken.");

    public async Task ConnectAsync(CancellationToken token) => await (await GetInnerAsync(token)).ConnectAsync(token);
    public Task DisconnectAsync() => RequireInner().DisconnectAsync();

    public async Task<bool> FileExistsAsync(string remotePath, CancellationToken token) => await (await GetInnerAsync(token)).FileExistsAsync(remotePath, token);
    public async Task<bool> DirectoryExistsAsync(string remotePath, CancellationToken token) => await (await GetInnerAsync(token)).DirectoryExistsAsync(remotePath, token);
    public async Task<long?> GetFileSizeAsync(string remotePath, CancellationToken token) => await (await GetInnerAsync(token)).GetFileSizeAsync(remotePath, token);
    public async Task<DateTime?> GetModifiedTimeAsync(string remotePath, CancellationToken token) => await (await GetInnerAsync(token)).GetModifiedTimeAsync(remotePath, token);

    public async Task<IEnumerable<FluentFTP.FtpListItem>> ListAsync(string path, CancellationToken token) => await (await GetInnerAsync(token)).ListAsync(path, token);
    public async Task<IEnumerable<string>> ListDirectoryAsync(string path, CancellationToken token) => await (await GetInnerAsync(token)).ListDirectoryAsync(path, token);

    public async Task CreateDirectoryAsync(string path, CancellationToken token) => await (await GetInnerAsync(token)).CreateDirectoryAsync(path, token);
    public async Task DeleteDirectoryAsync(string path, CancellationToken token) => await (await GetInnerAsync(token)).DeleteDirectoryAsync(path, token);
    public async Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken token) => await (await GetInnerAsync(token)).DeleteDirectoryAsync(path, recursive, token);
    public async Task DeleteFileAsync(string path, CancellationToken token) => await (await GetInnerAsync(token)).DeleteFileAsync(path, token);

    public async Task UploadFileAsync(string localPath, string remotePath, IProgress<FluentFTP.FtpProgress>? progress, CancellationToken token, FluentFTP.FtpRemoteExists existsMode = FluentFTP.FtpRemoteExists.Overwrite, bool createRemoteDir = true)
        => await (await GetInnerAsync(token)).UploadFileAsync(localPath, remotePath, progress, token, existsMode, createRemoteDir);
    public async Task UploadDirectoryAsync(string localDirectory, string remoteDirectory, IProgress<FluentFTP.FtpProgress>? progress, CancellationToken token, FluentFTP.FtpFolderSyncMode syncMode = FluentFTP.FtpFolderSyncMode.Update, FluentFTP.FtpRemoteExists existsMode = FluentFTP.FtpRemoteExists.Overwrite, bool recursive = true)
        => await (await GetInnerAsync(token)).UploadDirectoryAsync(localDirectory, remoteDirectory, progress, token, syncMode, existsMode, recursive);

    public async Task DownloadFileAsync(string localPath, string remotePath, IProgress<FluentFTP.FtpProgress>? progress, CancellationToken token, FluentFTP.FtpLocalExists existsMode = FluentFTP.FtpLocalExists.Overwrite)
        => await (await GetInnerAsync(token)).DownloadFileAsync(localPath, remotePath, progress, token, existsMode);
    public async Task DownloadDirectoryAsync(string localDirectory, string remoteDirectory, IProgress<FluentFTP.FtpProgress>? progress, CancellationToken token, FluentFTP.FtpLocalExists existsMode = FluentFTP.FtpLocalExists.Overwrite, bool recursive = true)
        => await (await GetInnerAsync(token)).DownloadDirectoryAsync(localDirectory, remoteDirectory, progress, token, existsMode, recursive);

    public async Task MoveFileAsync(string sourcePath, string destinationPath, CancellationToken token) => await (await GetInnerAsync(token)).MoveFileAsync(sourcePath, destinationPath, token);
    public async Task MoveDirectoryAsync(string sourcePath, string destinationPath, CancellationToken token) => await (await GetInnerAsync(token)).MoveDirectoryAsync(sourcePath, destinationPath, token);

    public async Task UploadFilesAsync(IEnumerable<string> localPaths, string remotePath, IProgress<FluentFTP.FtpProgress> progress, int maxParallel, CancellationToken token)
        => await (await GetInnerAsync(token)).UploadFilesAsync(localPaths, remotePath, progress, maxParallel, token);
    public async Task DownloadFilesAsync(IEnumerable<(string RemotePath, string LocalPath)> files, int maxParallel, CancellationToken token)
        => await (await GetInnerAsync(token)).DownloadFilesAsync(files, maxParallel, token);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        if (_inner != null)
        {
            _pool.Release(_inner);
            _inner = null;
        }
        await Task.CompletedTask;
    }
}
