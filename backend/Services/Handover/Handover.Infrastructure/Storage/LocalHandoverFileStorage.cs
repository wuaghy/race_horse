using Handover.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Handover.Infrastructure.Storage;

public sealed class LocalHandoverFileStorage : IHandoverFileStorage
{
    private readonly string _root;

    public LocalHandoverFileStorage(IConfiguration configuration)
    {
        _root = Path.GetFullPath(configuration["Storage:HandoverDirectory"]
            ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "handover-files"));
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
    {
        var key = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = ResolvePath(key);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(output, cancellationToken);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        Stream? stream = File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        var fileName = Path.GetFileName(storageKey);
        if (string.IsNullOrWhiteSpace(fileName) || fileName != storageKey)
            throw new InvalidOperationException("Invalid handover file storage key.");
        var path = Path.GetFullPath(Path.Combine(_root, fileName));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid handover file storage key.");
        return path;
    }
}
