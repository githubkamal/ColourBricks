using ColourBricks.Application.Attachments;
using Microsoft.Extensions.Configuration;

namespace ColourBricks.Infrastructure.Attachments;

/// <summary>
/// Stores attachment bytes on local disk under <c>Storage:Root</c>, which lives
/// outside the web root (plan.md P2-T08). Swap this registration for S3/MinIO later.
/// </summary>
public sealed class DiskFileStorage : IFileStorage
{
    private readonly string _root;

    public DiskFileStorage(IConfiguration configuration)
    {
        _root = configuration["Storage:Root"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "storage");
        _root = Path.GetFullPath(_root);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(
        string ownerType, string extension, Stream content, CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string relativeDir = Path.Combine(Sanitise(ownerType), now.ToString("yyyy"), now.ToString("MM"));
        string relativePath = Path.Combine(relativeDir, $"{Guid.NewGuid():N}{extension}");

        string absoluteDir = Path.Combine(_root, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        string absolutePath = Path.Combine(_root, relativePath);
        await using (FileStream file = File.Create(absolutePath))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        return relativePath.Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        string absolutePath = ResolveInsideRoot(relativePath);
        Stream stream = File.OpenRead(absolutePath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken)
    {
        string absolutePath = ResolveInsideRoot(relativePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    private string ResolveInsideRoot(string relativePath)
    {
        string absolutePath = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!absolutePath.StartsWith(_root, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Path escapes the storage root.");
        }

        return absolutePath;
    }

    private static string Sanitise(string segment) =>
        string.Concat(segment.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
}
