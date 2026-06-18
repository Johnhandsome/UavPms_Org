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

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AuthController(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
        _unitOfWork = unitOfWork;
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
        if (user == null || user.IsDeleted || user.Status != "Active")
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

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7); // Hạn Refresh Token là 7 ngày
        await _unitOfWork.SaveChangesAsync();

        return Ok(new
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
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
        var users = await _userRepository.FindAsync(u => u.RefreshToken == hashedToken, track: true);
        var user = users.FirstOrDefault();

        if (user == null || user.IsDeleted || user.Status != "Active")
        {
            return Unauthorized("Invalid refresh token.");
        }

        if (user.RefreshTokenExpiryTime == null ||user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return Unauthorized("Refresh token has expired.");
        }

        var userWithRoles = await _userRepository.GetByUsernameWithRolesAsync(user.Username);
        var roles = userWithRoles?.UserRoles.Select(ur => ur.Role!.RoleName).ToList() ?? new List<string>();

        var newAccessToken = _jwtProvider.GenerateAccessToken(user, roles);
        var newRefreshToken = _jwtProvider.GenerateRefreshToken();

        user.RefreshToken = HashToken(newRefreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _unitOfWork.SaveChangesAsync();

        return Ok(new
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken
        });
    }
}

public record LoginRequest(string Username, string Password);
public record RefreshTokenRequest(string RefreshToken, string? AccessToken);
