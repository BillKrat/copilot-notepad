using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace NotebookAI.Triples.Files;

public sealed class AzureFileShareFileStore : IFileStore
{
    private readonly ShareClient _share;

    public AzureFileShareFileStore(string connectionString, string shareName)
    {
        _share = new ShareClient(connectionString, shareName);
        _share.CreateIfNotExists();
    }

    public async Task<FileEntry> CreateAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var fileClient = await EnsureFileAsync(path, create: true, overwrite: false, ct);
        await UploadAsync(fileClient, content, contentType, ct);
        var props = await fileClient.GetPropertiesAsync(cancellationToken: ct);
        return ToEntry(path, props.Value);
    }

    public async Task<FileEntry?> GetAsync(string path, CancellationToken ct = default)
    {
        var (dirPath, fileName) = Split(path);
        var dirClient = _share.GetDirectoryClient(dirPath);
        var fileClient = dirClient.GetFileClient(fileName);
        if (!await fileClient.ExistsAsync(ct)) return null;
        var props = await fileClient.GetPropertiesAsync(cancellationToken: ct);
        return ToEntry(path, props.Value);
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken ct = default)
    {
        var (dirPath, fileName) = Split(path);
        var dirClient = _share.GetDirectoryClient(dirPath);
        var fileClient = dirClient.GetFileClient(fileName);
        var resp = await fileClient.DeleteIfExistsAsync(cancellationToken: ct);
        return resp.Value;
    }

    public async Task<IReadOnlyList<FileEntry>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var dir = _share.GetDirectoryClient(prefix.Trim('/'));
        if (!await dir.ExistsAsync(ct)) return Array.Empty<FileEntry>();
        var results = new List<FileEntry>();
        await foreach (var item in dir.GetFilesAndDirectoriesAsync(cancellationToken: ct))
        {
            if (item.IsDirectory) continue;
            var fileClient = dir.GetFileClient(item.Name);
            var props = await fileClient.GetPropertiesAsync(cancellationToken: ct);
            results.Add(ToEntry(prefix.TrimEnd('/') + "/" + item.Name, props.Value));
        }
        return results;
    }

    public async Task<FileEntry> UpsertAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var fileClient = await EnsureFileAsync(path, create: true, overwrite: true, ct);
        await UploadAsync(fileClient, content, contentType, ct);
        var props = await fileClient.GetPropertiesAsync(cancellationToken: ct);
        return ToEntry(path, props.Value);
    }

    private async Task<ShareFileClient> EnsureFileAsync(string path, bool create, bool overwrite, CancellationToken ct)
    {
        var (dirPath, fileName) = Split(path);
        var dirClient = _share.GetDirectoryClient(dirPath);
        await dirClient.CreateIfNotExistsAsync(cancellationToken: ct);
        var fileClient = dirClient.GetFileClient(fileName);
        if (create)
        {
            if (!overwrite && await fileClient.ExistsAsync(ct)) throw new InvalidOperationException("File exists");
            if (!await fileClient.ExistsAsync(ct))
            {
                await fileClient.CreateAsync(maxSize: 0, cancellationToken: ct); // placeholder
            }
        }
        return fileClient;
    }

    private static (string dir, string file) Split(string path)
    {
        path = path.Replace('\\','/').Trim('/');
        var idx = path.LastIndexOf('/');
        if (idx < 0) return (string.Empty, path);
        return (path[..idx], path[(idx+1)..]);
    }

    private static async Task UploadAsync(ShareFileClient fileClient, Stream content, string contentType, CancellationToken ct)
    {
        content.Position = 0;
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;
        var length = ms.Length;
        await fileClient.DeleteIfExistsAsync(cancellationToken: ct);
        await fileClient.CreateAsync(maxSize: length, cancellationToken: ct);
        ms.Position = 0;
        await fileClient.UploadAsync(ms, cancellationToken: ct);
    }

    private static FileEntry ToEntry(string path, ShareFileProperties props)
        => new(path, props.ContentType ?? "application/octet-stream", props.ContentLength, props.LastModified, null);
}
