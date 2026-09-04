using ColourBricks.Application.Attachments;
using ColourBricks.Domain.Attachments;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ColourBricks.Infrastructure.Attachments;

public sealed class AttachmentService(
    AppDbContext db,
    IFileStorage storage,
    IConfiguration configuration) : IAttachmentService
{
    private long MaxBytes => long.TryParse(configuration["Storage:MaxBytes"], out long v) ? v : 10 * 1024 * 1024;

    public async Task<AttachmentDto> UploadAsync(
        string ownerType,
        long ownerId,
        string fileName,
        string contentType,
        long declaredLength,
        Stream content,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(fileName);
        if (!FileValidation.IsAllowedExtension(extension))
        {
            throw new AttachmentRejectedException($"Files of type '{extension}' are not allowed.");
        }

        // Buffer to memory so we can check the size and magic bytes before persisting.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        byte[] bytes = buffer.ToArray();

        if (bytes.LongLength > MaxBytes || (declaredLength > 0 && declaredLength > MaxBytes))
        {
            throw new AttachmentTooLargeException(MaxBytes);
        }

        if (!FileValidation.MatchesSignature(extension, bytes.AsSpan(0, Math.Min(bytes.Length, 32))))
        {
            throw new AttachmentRejectedException(
                "The file's contents do not match its extension. It may have been renamed.");
        }

        string storedPath = await storage.SaveAsync(
            ownerType, extension.ToLowerInvariant(), new MemoryStream(bytes), cancellationToken);

        var attachment = new Attachment
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            OriginalFileName = Path.GetFileName(fileName),
            StoredPath = storedPath,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = bytes.LongLength,
        };
        db.Attachments.Add(attachment);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(attachment);
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(
        string ownerType, long ownerId, CancellationToken cancellationToken)
    {
        List<Attachment> rows = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerType == ownerType && a.OwnerId == ownerId)
            .OrderByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<(AttachmentDto Meta, Stream Content)?> DownloadAsync(
        long id, CancellationToken cancellationToken)
    {
        Attachment? attachment = await db.Attachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (attachment is null)
        {
            return null;
        }

        Stream content = await storage.OpenReadAsync(attachment.StoredPath, cancellationToken);
        return (ToDto(attachment), content);
    }

    private static AttachmentDto ToDto(Attachment a) => new(
        a.Id, a.OwnerType, a.OwnerId, a.OriginalFileName, a.ContentType, a.SizeBytes,
        a.CreatedAtUtc, a.CreatedByUserId);
}
