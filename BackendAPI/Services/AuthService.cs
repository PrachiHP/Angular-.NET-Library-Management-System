using BackendAPI.Data;
using BackendAPI.Dtos;
using BackendAPI.Exceptions;
using BackendAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext context, ITokenService tokens, ILogger<AuthService> logger)
    {
        _context = context;
        _tokens = tokens;
        _logger = logger;
    }

    /// <summary>Self-registration. Always creates a Member — never a Librarian.</summary>
    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new BusinessException("An account with that email already exists.");
        }

        var user = new User
        {
            Email = email,
            // BCrypt generates a random salt per password and embeds it in the
            // output, so identical passwords produce different hashes. It is
            // also deliberately slow, which is what makes brute force expensive.
            // Never use MD5 or SHA-256 here — they are built to be fast.
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            // Role is NOT taken from the request. Accepting it would let anyone
            // register themselves as a Librarian — a privilege escalation hole.
            Role = UserRole.Member,
        };

        var member = new Member
        {
            User = user,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Phone = dto.Phone?.Trim(),
            Address = dto.Address?.Trim(),
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Registered member {MemberId} ({Email})", member.Id, email);

        return BuildResponse(user, member);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .Include(u => u.Member)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        // One identical message whether the email is unknown or the password is
        // wrong. Distinguishing them would let an attacker enumerate valid
        // accounts by watching which error comes back.
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for {Email}", email);
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (user.Member is { IsActive: false })
        {
            throw new UnauthorizedException("This account has been deactivated.");
        }

        return BuildResponse(user, user.Member);
    }

    private AuthResponseDto BuildResponse(User user, Member? member)
    {
        var (token, expiresAt) = _tokens.CreateToken(user, member?.Id);

        var name = member is null
            ? user.Email
            : $"{member.FirstName} {member.LastName}";

        return new AuthResponseDto(token, expiresAt, user.Role.ToString(), name, user.Email, member?.Id);
    }
}
