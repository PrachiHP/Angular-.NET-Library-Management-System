using BackendAPI.Dtos;

namespace BackendAPI.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);

    Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
}
