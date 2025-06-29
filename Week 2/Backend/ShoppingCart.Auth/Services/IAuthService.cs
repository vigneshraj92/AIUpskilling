using ShoppingCart.Auth.DTOs;

namespace ShoppingCart.Auth.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
    Task<UserProfileDto> GetUserProfileAsync(int userId);
    Task LogoutAsync(int userId);
} 