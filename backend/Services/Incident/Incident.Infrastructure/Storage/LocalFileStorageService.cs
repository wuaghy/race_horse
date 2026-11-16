using Incident.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Incident.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageDirectory;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(ILogger<LocalFileStorageService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _storageDirectory = configuration["Storage:LocalDirectory"]
            ?? Path.Combine(AppContext.BaseDirectory, "incident-uploads");
        _storageDirectory = Path.GetFullPath(_storageDirectory);
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var filePath = Path.Combine(_storageDirectory, uniqueFileName);

        using (var output = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(output, cancellationToken);
        }

        var fileUrl = $"/storage/incident-uploads/{uniqueFileName}";
        _logger.LogInformation("Saved file {FileName} to {FilePath} (URL: {FileUrl})", fileName, filePath, fileUrl);
        return fileUrl;
    }

    public Task<Stream?> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(fileUrl);
        var filePath = Path.Combine(_storageDirectory, fileName);

        if (!File.Exists(filePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(fileUrl);
        var filePath = Path.Combine(_storageDirectory, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted file at {FilePath}", filePath);
        }

        return Task.CompletedTask;
    }
}
