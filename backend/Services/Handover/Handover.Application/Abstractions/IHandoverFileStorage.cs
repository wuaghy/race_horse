namespace Handover.Application.Abstractions;

public sealed record StoredHandoverFile(
    string StorageKey,
    string OriginalFileName,
    string ContentType,
    long SizeBytes);

public interface IHandoverFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
