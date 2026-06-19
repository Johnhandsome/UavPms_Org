using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using Asp.Versioning;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1.0")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IGenericRepository<RefreshToken> _refreshTokenRepository;

    public AuthController(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IGenericRepository<RefreshToken> refreshTokenRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _refreshTokenRepository = refreshTokenRepository;
    }
    
    private static string HashToken(string token){
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Đăng nhập hệ thống và cấp Access Token + Refresh Token.
    /// POST: api/login
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest("Username and Password are required.");
        }

        var user = await _userRepository.GetByUsernameWithRolesAsync(request.Username);
        if (user == null || user.Status != "Active")
        {
            return Unauthorized("Invalid credentials or inactive account.");
        }

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return Unauthorized("Invalid credentials.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role!.RoleName).ToList();
        var accessToken = _jwtProvider.GenerateAccessToken(user, roles);
        var refreshToken = _jwtProvider.GenerateRefreshToken();

        var expiryMinutesStr = _configuration["Jwt:ExpiryMinutes"] ?? "60";
        double.TryParse(expiryMinutesStr, out var expiryMinutes);
        if(expiryMinutes <= 0) expiryMinutes = 60;
        var expriesInSeconds = (int)(expiryMinutes * 60);

        var refreshTokenEntity = new RefreshToken{
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceInfo = Request.Headers["User-Agent"].ToString(),
        };

        await _refreshTokenRepository.AddAsync(refreshTokenEntity);
        await _unitOfWork.SaveChangesAsync();

        return Ok(new
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expriesInSeconds,
            User = new 
            {
                user.Id,
                user.Username,
                user.Email,
                user.FullName,
                Roles = roles
            }
        });
    }

    /// <summary>
    /// Làm mới Access Token sử dụng Refresh Token hợp lệ.
    /// POST: api/refresh-token
    /// </summary>
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest("Refresh Token is required.");
        }

        var hashedToken = HashToken(request.RefreshToken);
        var refreshTokens = await _refreshTokenRepository.FindAsync(t => t.TokenHash == hashedToken && t.RevokedAt == null, track: true);
        var oldRefreshToken = refreshTokens.FirstOrDefault();

        if (oldRefreshToken == null) 
        {
            return Unauthorized("Invalid refresh token.");
        }
        
        if (oldRefreshToken.ExpiresAt <= DateTime.UtcNow)
        {
            return Unauthorized("Refresh token has expired.");
        }
        
        var user = await _userRepository.GetByIdAsync(oldRefreshToken.UserId);

        if (user == null || user.Status != "Active")
        {
            return Unauthorized("Invalid refresh token.");
        }

        oldRefreshToken.RevokedAt = DateTime.UtcNow;

        var userWithRoles = await _userRepository.GetByUsernameWithRolesAsync(user.Username);
        if (userWithRoles == null) 
        {
            return Unauthorized("User not found.");
        }

        var roles = userWithRoles.UserRoles.Select(ur => ur.Role!.RoleName).ToList();

        var newAccessToken = _jwtProvider.GenerateAccessToken(userWithRoles, roles);
        var newRefreshToken = _jwtProvider.GenerateRefreshToken();

        var newRefreshTokenEntity = new RefreshToken{
            UserId = user.Id,
            TokenHash = HashToken(newRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceInfo = Request.Headers["User-Agent"].ToString()
        };

        await _refreshTokenRepository.AddAsync(newRefreshTokenEntity);
        await _unitOfWork.SaveChangesAsync();

        var expiryMinutesStr = _configuration["Jwt:ExpiryMinutes"] ?? "60";
        double.TryParse(expiryMinutesStr, out var expiryMinutes);
        if(expiryMinutes <= 0) expiryMinutes = 60;
        var expriesInSeconds = (int)(expiryMinutes * 60);

        return Ok(new
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = expriesInSeconds,
            User = new
            {
                userWithRoles.Id,
                userWithRoles.Username,
                userWithRoles.Email,
                userWithRoles.FullName,
                Roles = roles
            }
        });
    }
}

public record LoginRequest(string Username, string Password);
public record RefreshTokenRequest(string RefreshToken);
