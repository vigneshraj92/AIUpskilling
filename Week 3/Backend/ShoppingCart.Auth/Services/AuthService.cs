using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ShoppingCart.Auth.DTOs;
using ShoppingCart.Common.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ShoppingCart.Auth.Services;

public class AuthService : IAuthService
{
    private readonly DbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(DbContext context, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        // Check if user already exists
        var existingUser = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted);

        if (existingUser != null)
        {
            throw new ArgumentException("User with this email already exists");
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Create new user
        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PasswordHash = passwordHash,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            Role = "Customer",
            IsActive = true
        };

        _context.Set<User>().Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("New user registered: {Email}", request.Email);

        // Generate tokens
        var (accessToken, refreshToken, expiresAt) = GenerateTokens(user);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = MapToUserProfile(user)
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted && u.IsActive);

        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        _logger.LogInformation("User logged in: {Email}", request.Email);

        // Generate tokens
        var (accessToken, refreshToken, expiresAt) = GenerateTokens(user);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = MapToUserProfile(user)
        };
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
    {
        // In a real implementation, you would validate the refresh token
        // against a stored refresh token in the database
        // For this example, we'll simulate token validation

        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        // Extract user ID from refresh token (in real implementation, validate signature)
        // This is a simplified version
        var userId = ExtractUserIdFromToken(refreshToken);
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted && u.IsActive);

        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        // Generate new tokens
        var (accessToken, newRefreshToken, expiresAt) = GenerateTokens(user);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = expiresAt,
            User = MapToUserProfile(user)
        };
    }

    public async Task<UserProfileDto> GetUserProfileAsync(int userId)
    {
        var user = await _context.Set<User>()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        return MapToUserProfile(user);
    }

    public async Task LogoutAsync(int userId)
    {
        // In a real implementation, you would invalidate the refresh token
        // by storing it in a blacklist or updating its status in the database
        _logger.LogInformation("User logged out: {UserId}", userId);
        await Task.CompletedTask;
    }

    private (string accessToken, string refreshToken, DateTime expiresAt) GenerateTokens(User user)
    {
        var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not found");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "ShoppingCartAPI";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "ShoppingCartClient";
        var jwtExpiryMinutes = int.Parse(_configuration["Jwt:ExpiryInMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("sub", user.Id.ToString()),
            new Claim("email", user.Email)
        };

        var expiresAt = DateTime.UtcNow.AddMinutes(jwtExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateRefreshToken(user.Id);

        return (accessToken, refreshToken, expiresAt);
    }

    private string GenerateRefreshToken(int userId)
    {
        // In a real implementation, you would store refresh tokens in the database
        // with expiration dates and user associations
        var refreshTokenData = $"{userId}:{DateTime.UtcNow:yyyyMMddHHmmss}:{Guid.NewGuid()}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(refreshTokenData));
    }

    private int ExtractUserIdFromToken(string refreshToken)
    {
        try
        {
            var tokenData = Encoding.UTF8.GetString(Convert.FromBase64String(refreshToken));
            var parts = tokenData.Split(':');
            if (parts.Length >= 1 && int.TryParse(parts[0], out var userId))
            {
                return userId;
            }
        }
        catch
        {
            // Token is invalid
        }

        throw new UnauthorizedAccessException("Invalid refresh token");
    }

    private UserProfileDto MapToUserProfile(User user)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }
} 