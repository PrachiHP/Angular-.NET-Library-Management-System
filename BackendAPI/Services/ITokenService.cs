using BackendAPI.Models;

namespace BackendAPI.Services;

public interface ITokenService
{
    /// <summary>Builds a signed JWT carrying the user's identity and role.</summary>
    (string Token, DateTime ExpiresAt) CreateToken(User user, int? memberId);
}
