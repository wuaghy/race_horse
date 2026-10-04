using Compliance.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Compliance.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageDirectory;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        _storageDirectory = configuration["Storage:LocalDirectory"]
            ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "compliance-files");

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var safeFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var targetPath = Path.Combine(_storageDirectory, safeFileName);

        await using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(output, cancellationToken);

        _logger.LogInformation("Stored file {OriginalName} as {SafeFileName} at {Path}", fileName, safeFileName, targetPath);
        return $"/storage/compliance-files/{safeFileName}";
    }

    public Task<Stream?> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(fileUrl);
        var targetPath = Path.Combine(_storageDirectory, fileName);

        if (!File.Exists(targetPath))
        {
            _logger.LogWarning("File not found at {Path}", targetPath);
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(fileUrl);
        var targetPath = Path.Combine(_storageDirectory, fileName);

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
            _logger.LogInformation("Deleted file {Path}", targetPath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
