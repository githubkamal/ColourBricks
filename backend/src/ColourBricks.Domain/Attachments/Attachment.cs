using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Attachments;

/// <summary>
/// A file attached to a financial record (BRD §67). The bytes live on disk outside
/// the web root; this row is the only index into them.
/// </summary>
[Auditable("audit_trail")]
public sealed class Attachment : BaseEntity
{
    /// <summary>The kind of record this belongs to, e.g. <c>VendorPurchase</c>.</summary>
    public required string OwnerType { get; set; }

    public long OwnerId { get; set; }

    public required string OriginalFileName { get; set; }

    /// <summary>Path relative to the storage root: <c>{ownerType}/{yyyy}/{MM}/{guid}{ext}</c>.</summary>
    public required string StoredPath { get; set; }

    public required string ContentType { get; set; }

    public long SizeBytes { get; set; }
}
