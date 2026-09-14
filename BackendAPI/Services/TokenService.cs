using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BackendAPI.Models;
using Microsoft.IdentityModel.Tokens;

namespace BackendAPI.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(User user, int? memberId)
    {
        var jwt = _config.GetSection("Jwt");

        var key = jwt["Key"]
                  ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        // HMAC-SHA256 requires a key of at least 256 bits. Failing loudly here
        // beats a confusing runtime error on the first login attempt.
        if (Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 bytes for HS256.");
        }

        var expiryHours = jwt.GetValue<int?>("ExpiryHours") ?? 24;
        var expiresAt = DateTime.UtcNow.AddHours(expiryHours);

        // Claims are the facts the token asserts about the caller. They are
        // base64-ENCODED, not encrypted — anyone can read them. Never put a
        // secret in a claim. The signature guarantees integrity, not privacy.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            // ClaimTypes.Role is what [Authorize(Roles = "...")] reads.
            new(ClaimTypes.Role, user.Role.ToString()),
        };

        if (memberId.HasValue)
        {
            // Saves a database lookup on every member-scoped request.
            claims.Add(new Claim("memberId", memberId.Value.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
