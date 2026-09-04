using ColourBricks.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace ColourBricks.Infrastructure.Identity;

/// <summary>
/// <see cref="IPasswordHasher"/> backed by ASP.NET Core Identity's PBKDF2 hasher
/// (plan.md §9).
/// </summary>
public sealed class IdentityPasswordHasher(IPasswordHasher<User> inner) : IPasswordHasher
{
    // A throwaway instance — Identity's hasher never reads the user object.
    private static readonly User Placeholder = new()
    {
        Name = string.Empty,
        Email = string.Empty,
        PasswordHash = string.Empty,
    };

    public string Hash(string password) => inner.HashPassword(Placeholder, password);

    public PasswordVerification Verify(string hash, string password) =>
        inner.VerifyHashedPassword(Placeholder, hash, password) switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed,
        };
}
