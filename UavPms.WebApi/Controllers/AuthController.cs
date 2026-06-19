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
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IEventPublisher _eventPublisher;
    private readonly IMemoryCache _cache;
    private readonly IEnumerable<IOtpPurposeHandler> _otpHandlers;
    private readonly IGenericRepository<PasswordResetToken> _passwordResetTokenRepository;
    private readonly IGenericRepository<TrustedDevice> _trustedDeviceRepository;

    public AuthController(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IOtpService otpService,
        IEmailService emailService,
        IEventPublisher eventPublisher,
        IMemoryCache cache,
        IEnumerable<IOtpPurposeHandler> otpHandlers,
        IGenericRepository<PasswordResetToken> passwordResetTokenRepository,
        IGenericRepository<TrustedDevice> trustedDeviceRepository)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _otpService = otpService;
        _emailService = emailService;
        _eventPublisher = eventPublisher;
        _cache = cache;
        _otpHandlers = otpHandlers;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _trustedDeviceRepository = trustedDeviceRepository;
    }
    
    private static string HashToken(string token){
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }

    private async Task<TrustedDevice?> GetValidTrustedDeviceAsync(Guid userId)
    {
        string? deviceTrustToken = Request.Cookies["device_trust_token"];
        if (string.IsNullOrEmpty(deviceTrustToken))
        {
            deviceTrustToken = Request.Headers["X-Device-Trust-Token"].ToString();
        }

        if (string.IsNullOrEmpty(deviceTrustToken))
        {
            return null;
        }

        var tokenHash = HashToken(deviceTrustToken);
        var devices = await _trustedDeviceRepository.FindAsync(
            d => d.UserId == userId && d.DeviceTokenHash == tokenHash && d.ExpiresAt > DateTime.UtcNow,
            track: true);

        return devices.FirstOrDefault();
    }

    private async Task<IActionResult> IssueAuthenticationResponseAsync(User user, string? deviceTrustToken = null)
    {
        var roles = user.UserRoles.Select(ur => ur.Role!.RoleName).ToList();
        var accessToken = _jwtProvider.GenerateAccessToken(user, roles);
        var refreshToken = _jwtProvider.GenerateRefreshToken();

        var expiryMinutesStr = _configuration["Jwt:ExpiryMinutes"] ?? "60";
        double.TryParse(expiryMinutesStr, out var expiryMinutes);
        if (expiryMinutes <= 0) expiryMinutes = 60;
        var expriesInSeconds = (int)(expiryMinutes * 60);

        user.RefreshToken = HashToken(refreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _unitOfWork.SaveChangesAsync();

        if (!string.IsNullOrEmpty(deviceTrustToken))
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(30)
            };
            Response.Cookies.Append("device_trust_token", deviceTrustToken, cookieOptions);
        }

        return Ok(new ApiResponse(true, "Login successful.", new
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
        }));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new ApiResponse(false, "Email and Password are required."));
        }

        var user = await _userRepository.GetByEmailWithRolesAsync(request.Email);
        if (user == null)
        {
            user = await _userRepository.GetByUsernameWithRolesAsync(request.Email);
        }

        if (user == null || user.Status != "Active")
        {
            return BadRequest(new ApiResponse(false, "Invalid credentials or inactive account."));
        }

        if (!_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return BadRequest(new ApiResponse(false, "Invalid credentials."));
        }

        if (!user.IsEmailVerified)
        {
            return BadRequest(new ApiResponse(false, "Please verify your email address before logging in."));
        }

        // Check trusted device
        var trustedDevice = await GetValidTrustedDeviceAsync(user.Id);
        if (trustedDevice != null)
        {
            // sliding expiry: update ExpiresAt and LastUsedAt
            trustedDevice.LastUsedAt = DateTime.UtcNow;
            trustedDevice.ExpiresAt = DateTime.UtcNow.AddDays(30);
            await _trustedDeviceRepository.UpdateAsync(trustedDevice);
            await _unitOfWork.SaveChangesAsync();

            var deviceTrustToken = Request.Cookies["device_trust_token"];
            if (string.IsNullOrEmpty(deviceTrustToken))
            {
                deviceTrustToken = Request.Headers["X-Device-Trust-Token"].ToString();
            }

            return await IssueAuthenticationResponseAsync(user, deviceTrustToken);
        }

        // Send OTP for Login
        var otpResult = await _otpService.GenerateAndSendOtpAsync(user.Email, OtpPurpose.Login);
        if (!otpResult.Success)
        {
            return BadRequest(new ApiResponse(false, otpResult.Message));
        }

        return Ok(new ApiResponse(true, "OTP verification required.", new
        {
            OtpRequired = true,
            Email = user.Email
        }));
    }

    [HttpPost("otp/send")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        return await SendOtpInternalAsync(request);
    }

    private async Task<IActionResult> SendOtpInternalAsync(SendOtpRequest request)
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
        if (user == null || user.Status != "Active")
        {
            return BadRequest(new ApiResponse(false, "User not found or inactive."));
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

                var dbToken = new PasswordResetToken
                {
                    UserId = user.Id,
                    Token = hashedToken,
                    ExpiryDate = DateTime.UtcNow.AddMinutes(10), // 10 minutes short-lived
                    Used = false
                };

                await _passwordResetTokenRepository.AddAsync(dbToken);
                await _unitOfWork.SaveChangesAsync();

                return Ok(new ApiResponse(true, "OTP verified successfully.", new
                {
                    VerificationToken = verificationToken
                }));

            case OtpPurpose.ChangePassword:
            case OtpPurpose.ChangeEmail:
            case OtpPurpose.DeleteAccount:
                var stepUpToken = _jwtProvider.GenerateStepUpToken(user, request.Purpose.ToString());
                return Ok(new ApiResponse(true, "OTP verified successfully for sensitive action.", new
                {
                    StepUpToken = stepUpToken
                }));

            case OtpPurpose.EmailVerification:
                user.IsEmailVerified = true;
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
        if (request == null || string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new ApiResponse(false, "Refresh Token is required."));
        }

        var hashedToken = HashToken(request.RefreshToken);
        var users = await _userRepository.FindAsync(u => u.RefreshToken == hashedToken, track: true);
        var user = users.FirstOrDefault();

        if (user == null || user.Status != "Active")
        {
            return BadRequest(new ApiResponse(false, "Invalid refresh token."));
        }

        if (user.RefreshTokenExpiryTime == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return BadRequest(new ApiResponse(false, "Refresh token has expired."));
        }

        var userWithRoles = await _userRepository.GetByUsernameWithRolesAsync(user.Username);
        if (userWithRoles == null) 
        {
            return BadRequest(new ApiResponse(false, "User not found."));
        }

        var roles = userWithRoles.UserRoles.Select(ur => ur.Role!.RoleName).ToList();

        var newAccessToken = _jwtProvider.GenerateAccessToken(userWithRoles, roles);
        var newRefreshToken = _jwtProvider.GenerateRefreshToken();

        user.RefreshToken = HashToken(newRefreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _unitOfWork.SaveChangesAsync();

        var expiryMinutesStr = _configuration["Jwt:ExpiryMinutes"] ?? "60";
        double.TryParse(expiryMinutesStr, out var expiryMinutes);
        if (expiryMinutes <= 0) expiryMinutes = 60;
        var expriesInSeconds = (int)(expiryMinutes * 60);

        return Ok(new ApiResponse(true, "Token refreshed successfully.", new
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
        }));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.VerificationToken) || string.IsNullOrEmpty(request.NewPassword))
        {
            return BadRequest(new ApiResponse(false, "VerificationToken and NewPassword are required."));
        }

        var hashedToken = HashToken(request.VerificationToken);
        var tokens = await _passwordResetTokenRepository.FindAsync(t => t.Token == hashedToken && !t.Used && t.ExpiryDate > DateTime.UtcNow, track: true);
        var dbToken = tokens.FirstOrDefault();

        if (dbToken == null)
        {
            return BadRequest(new ApiResponse(false, "Invalid or expired verification token."));
        }

        var user = await _userRepository.GetByIdAsync(dbToken.UserId, track: true);
        if (user == null || user.Status != "Active")
        {
            return BadRequest(new ApiResponse(false, "User not found or inactive."));
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        dbToken.Used = true;

        await _userRepository.UpdateAsync(user);
        await _passwordResetTokenRepository.UpdateAsync(dbToken);
        await _unitOfWork.SaveChangesAsync();

        await _eventPublisher.PublishAsync(new PasswordResetCompleted
        {
            Email = user.Email,
            ResetAt = DateTime.UtcNow
        });

        return Ok(new ApiResponse(true, "Password has been reset successfully."));
    }
}

public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record SendOtpRequest(string? Email, OtpPurpose Purpose);
public record VerifyOtpRequest(string? Email, string Otp, OtpPurpose Purpose);
public record ResetPasswordRequest(string VerificationToken, string NewPassword);
