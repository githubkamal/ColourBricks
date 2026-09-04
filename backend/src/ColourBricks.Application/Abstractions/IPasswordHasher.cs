namespace ColourBricks.Application.Abstractions;

/// <summary>
/// Hashes and verifies user passwords (plan.md §9 — PBKDF2 via ASP.NET Core Identity's
/// hasher, or Argon2id). Implemented in Infrastructure.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string hash, string password);
}

public enum PasswordVerification
{
    Failed = 0,
    Success = 1,

    /// <summary>Password is correct but the stored hash uses outdated parameters.</summary>
    SuccessRehashNeeded = 2,
}
