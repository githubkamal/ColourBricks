namespace ColourBricks.Application.Attachments;

public sealed record AttachmentDto(
    long Id,
    string OwnerType,
    long OwnerId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc,
    long? UploadedByUserId);

/// <summary>Abstracts where attachment bytes are stored (disk today, S3/MinIO later).</summary>
public interface IFileStorage
{
    /// <summary>Saves the stream and returns the path relative to the storage root.</summary>
    Task<string> SaveAsync(
        string ownerType, string extension, Stream content, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken);
}

public interface IAttachmentService
{
    Task<AttachmentDto> UploadAsync(
        string ownerType,
        long ownerId,
        string fileName,
        string contentType,
        long declaredLength,
        Stream content,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AttachmentDto>> ListAsync(
        string ownerType, long ownerId, CancellationToken cancellationToken);

    Task<(AttachmentDto Meta, Stream Content)?> DownloadAsync(long id, CancellationToken cancellationToken);
}

/// <summary>Extension not allowed, or the bytes don't match the claimed type. Maps to 400.</summary>
public sealed class AttachmentRejectedException(string reason) : Exception(reason);

/// <summary>File exceeds the configured size cap. Maps to 413.</summary>
public sealed class AttachmentTooLargeException(long limit)
    : Exception($"The file exceeds the {limit}-byte upload limit.");
