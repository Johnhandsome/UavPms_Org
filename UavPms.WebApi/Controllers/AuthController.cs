using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using UavPms.Core.Entities;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using UavPms.Core.Contracts;
using Microsoft.Extensions.Configuration;
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
    private readonly IOtpService _otpService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IEnumerable<IOtpPurposeHandler> _otpHandlers;
    private readonly IGenericRepository<TrustedDevice> _trustedDeviceRepository;

    public AuthController(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IGenericRepository<RefreshToken> refreshTokenRepository,
        IOtpService otpService,
        IEventPublisher eventPublisher,
        IEnumerable<IOtpPurposeHandler> otpHandlers,
        IGenericRepository<TrustedDevice> trustedDeviceRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _refreshTokenRepository = refreshTokenRepository;
        _otpService = otpService;
        _eventPublisher = eventPublisher;
        _otpHandlers = otpHandlers;
        _trustedDeviceRepository = trustedDeviceRepository;
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    private async Task<TrustedDevice?> GetValidTrustedDeviceAsync(Guid userId)
    {
        var deviceToken = Request.Cookies["device_trust_token"] ?? Request.Headers["X-Device-Trust-Token"].ToString();

        if (string.IsNullOrEmpty(deviceToken)) return null;

        var hash = HashToken(deviceToken);

        var devices = await _trustedDeviceRepository.FindAsync(
            d => d.UserId == userId && d.DeviceTokenHash == hash && d.ExpiresAt > DateTime.UtcNow,
            track: true);

        return devices.FirstOrDefault();
    }

    private async Task<IActionResult> IssueAuthenticationResponseAsync(User user, string? deviceTrustToken = null)
    {
        var roles = user.UserRoles.Select(r => r.Role!.RoleName).ToList();

        var accessToken = _jwtProvider.GenerateAccessToken(user, roles);
        var refreshToken = _jwtProvider.GenerateRefreshToken();

        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var m) ? m : 60;

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceInfo = Request.Headers["User-Agent"].ToString(),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.AddAsync(refreshTokenEntity);
        await _unitOfWork.SaveChangesAsync();

        if (!string.IsNullOrEmpty(deviceTrustToken))
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var isDev = string.IsNullOrEmpty(env) || env.Equals("Development", StringComparison.OrdinalIgnoreCase);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = !isDev,
                SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None,
                Expires = DateTime.UtcNow.AddDays(30)
            };
            Response.Cookies.Append("device_trust_token", deviceTrustToken, cookieOptions);
        }

        return Ok(new ApiResponse(true, "Success", new
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = expiryMinutes * 60,
            DeviceTrustToken = deviceTrustToken,
            User = new
            {
                user.Id,
                user.Email,
                user.Username,
                user.FullName,
                Roles = roles
            }
        }));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userRepository.GetByEmailWithRolesAsync(request.Email)
                   ?? await _userRepository.GetByUsernameWithRolesAsync(request.Email);

        if (user == null || user.Status != "Active")
            return BadRequest(new ApiResponse(false, "Invalid credentials"));

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
            return BadRequest(new ApiResponse(false, "Invalid credentials"));

        if (!user.IsEmailVerified)
            return BadRequest(new ApiResponse(false, "Email not verified"));

        var trustedDevice = await GetValidTrustedDeviceAsync(user.Id);

        if (trustedDevice != null)
        {
            trustedDevice.LastUsedAt = DateTime.UtcNow;
            trustedDevice.ExpiresAt = DateTime.UtcNow.AddDays(30);

            await _trustedDeviceRepository.UpdateAsync(trustedDevice);
            await _unitOfWork.SaveChangesAsync();

            var deviceTrustToken = Request.Cookies["device_trust_token"] ?? Request.Headers["X-Device-Trust-Token"].ToString();
            return await IssueAuthenticationResponseAsync(user, deviceTrustToken);
        }

        var otp = await _otpService.GenerateAndSendOtpAsync(user.Email, OtpPurpose.Login);

        if (!otp.Success)
            return BadRequest(new ApiResponse(false, otp.Message));

        return Ok(new ApiResponse(true, "OTP required", new { user.Email }));
    }

    [HttpPost("otp/send")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        if (request == null)
        {
            return BadRequest(new ApiResponse(false, "Request body is required."));
        }

        var handler = _otpHandlers.FirstOrDefault(h => h.Purpose == request.Purpose);
        if (handler == null)
        {
            return BadRequest(new ApiResponse(false, "Unsupported OTP purpose."));
        }

        string? email = request.Email;

        if (handler.RequiresAuthentication)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new ApiResponse(false, "Authentication is required for this OTP purpose."));
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new ApiResponse(false, "Invalid user token claims."));
            }

            var user = await _userRepository.GetByIdAsync(userId, track: false);
            if (user == null || user.Status != "Active")
            {
                return BadRequest(new ApiResponse(false, "User not found or inactive."));
            }

            email = user.Email;
        }

        var validationResult = await handler.ValidatePreconditionAsync(email, User);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiResponse(false, validationResult.Message));
        }

        if (validationResult.ShouldSilentSuccess)
        {
            return Ok(new ApiResponse(true, "If the email is associated with an account, an OTP code has been sent."));
        }

        var result = await _otpService.GenerateAndSendOtpAsync(email!, request.Purpose);
        if (!result.Success)
        {
            return BadRequest(new ApiResponse(false, result.Message));
        }

        if (request.Purpose == OtpPurpose.ForgotPassword)
        {
            await _eventPublisher.PublishAsync(new PasswordResetRequested
            {
                Email = email!,
                Token = "",
                ExpiryTime = DateTime.UtcNow.AddMinutes(3)
            });

            return Ok(new ApiResponse(true, "If the email is associated with an account, an OTP code has been sent."));
        }

        return Ok(new ApiResponse(true, result.Message));
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Otp))
        {
            return BadRequest(new ApiResponse(false, "OTP is required."));
        }

        var handler = _otpHandlers.FirstOrDefault(h => h.Purpose == request.Purpose);
        if (handler == null)
        {
            return BadRequest(new ApiResponse(false, "Unsupported OTP purpose."));
        }

        string? email = request.Email;

        if (handler.RequiresAuthentication)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized(new ApiResponse(false, "Authentication is required for this OTP purpose."));
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new ApiResponse(false, "Invalid user token claims."));
            }

            var userFromDb = await _userRepository.GetByIdAsync(userId, track: false);
            if (userFromDb == null || userFromDb.Status != "Active")
            {
                return BadRequest(new ApiResponse(false, "User not found or inactive."));
            }

            email = userFromDb.Email;
        }
        else
        {
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest(new ApiResponse(false, "Email is required."));
            }
        }

        var validationResult = await handler.ValidatePreconditionAsync(email, User);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ApiResponse(false, validationResult.Message));
        }

        var otpVerification = await _otpService.VerifyOtpAsync(email!, request.Otp, request.Purpose);
        if (!otpVerification.IsValid)
        {
            return BadRequest(new ApiResponse(false, otpVerification.Message));
        }

        var user = await _userRepository.GetByEmailWithRolesAsync(email!);
        if (user == null)
        {
            return BadRequest(new ApiResponse(false, "User not found."));
        }

        if (request.Purpose != OtpPurpose.EmailVerification && user.Status != "Active")
        {
            return BadRequest(new ApiResponse(false, "User account is inactive."));
        }

        switch (request.Purpose)
        {
            case OtpPurpose.Login:
                var newDeviceTrustToken = Guid.NewGuid().ToString("N");
                var tokenHash = HashToken(newDeviceTrustToken);
                var trustedDevice = new TrustedDevice
                {
                    UserId = user.Id,
                    DeviceTokenHash = tokenHash,
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    LastUsedAt = DateTime.UtcNow,
                    UserAgent = Request.Headers["User-Agent"].ToString()
                };
                await _trustedDeviceRepository.AddAsync(trustedDevice);
                return await IssueAuthenticationResponseAsync(user, newDeviceTrustToken);

            case OtpPurpose.ForgotPassword:
                var verificationToken = Guid.NewGuid().ToString("N");
                var hashedToken = HashToken(verificationToken);

                // Save verificationToken in Redis (10 minutes)
                await _otpService.SaveVerificationTokenAsync(hashedToken, user.Email, TimeSpan.FromMinutes(10));

                return Ok(new ApiResponse(true, "OTP verified successfully.", new
                {
                    VerificationToken = verificationToken
                }));

            case OtpPurpose.ChangePassword:
            case OtpPurpose.ChangeEmail:
            case OtpPurpose.DeleteAccount:
                var stepUpToken = _jwtProvider.GenerateStepUpToken(user, request.Purpose.ToString());
                // Save step-up token in Redis (5 minutes)
                await _otpService.SaveStepUpTokenAsync(user.Id.ToString(), request.Purpose.ToString(), stepUpToken, TimeSpan.FromMinutes(5));
                
                return Ok(new ApiResponse(true, "OTP verified successfully for sensitive action.", new
                {
                    StepUpToken = stepUpToken
                }));

            case OtpPurpose.EmailVerification:
                user.IsEmailVerified = true;
                user.Status = "Active";
                await _userRepository.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();
                return Ok(new ApiResponse(true, "Email has been verified successfully."));

            default:
                return BadRequest(new ApiResponse(false, "Unsupported OTP purpose."));
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var hash = HashToken(request.RefreshToken);

        var token = (await _refreshTokenRepository.FindAsync(
            x => x.TokenHash == hash && x.RevokedAt == null,
            track: true)).FirstOrDefault();

        if (token == null || token.ExpiresAt <= DateTime.UtcNow)
            return BadRequest(new ApiResponse(false, "Invalid refresh token"));

        var user = await _userRepository.GetByIdAsync(token.UserId);

        if (user == null || user.Status != "Active")
            return Unauthorized();

        token.RevokedAt = DateTime.UtcNow;

        var roles = user.UserRoles.Select(r => r.Role!.RoleName).ToList();

        var newAccess = _jwtProvider.GenerateAccessToken(user, roles);
        var newRefresh = _jwtProvider.GenerateRefreshToken();

        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(newRefresh),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceInfo = Request.Headers["User-Agent"].ToString(),
            CreatedAt = DateTime.UtcNow
        });

        await _unitOfWork.SaveChangesAsync();

        return Ok(new
        {
            AccessToken = newAccess,
            RefreshToken = newRefresh
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var hash = HashToken(request.VerificationToken);

        var email = await _otpService.GetVerificationTokenEmailAsync(hash);

        if (string.IsNullOrEmpty(email))
            return BadRequest("Invalid token");

        var user = await _userRepository.GetByEmailWithRolesAsync(email);

        if (user == null)
            return BadRequest("User not found");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        await _otpService.DeleteVerificationTokenAsync(hash);

        return Ok(new ApiResponse(true, "Success"));
    }

    public record LoginRequest(string Email, string Password);
    public record RefreshTokenRequest(string RefreshToken);
    public record SendOtpRequest(string? Email, OtpPurpose Purpose);
    public record VerifyOtpRequest(string? Email, string Otp, OtpPurpose Purpose);
    public record ResetPasswordRequest(string VerificationToken, string NewPassword);
}