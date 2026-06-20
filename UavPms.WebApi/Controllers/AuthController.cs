using Microsoft.AspNetCore.Mvc;
using UavPms.Core.Enums;
using Asp.Versioning;
using MediatR;
using UavPms.Application.Features.Auth.Commands.Login;
using UavPms.Application.Features.Auth.Commands.RefreshToken;
using UavPms.Application.Features.Auth.Commands.ResetPassword;
using UavPms.Application.Features.Auth.Commands.SendOtp;
using UavPms.Application.Features.Auth.Commands.VerifyOtp;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[ApiVersion("1.0")]
public class AuthController : ControllerBase
{
    private readonly ISender _mediator;

    public AuthController(ISender mediator)
    {
        _mediator = mediator;
    }
    
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var deviceTrustToken = Request.Cookies["device_trust_token"] 
            ?? Request.Headers["X-Device-Trust-Token"].ToString();
        var userAgent = Request.Headers["User-Agent"].ToString();
        var command = new LoginCommand(request.Email, request.Password, deviceTrustToken, userAgent);
        var result  = await _mediator.Send(command);

        if (result.OtpRequired)
        {
            return Ok(new ApiResponse(true, "OTP required", new { result.Email }));
        }

        return Ok(new ApiResponse(true, "Sucess", new
        {
            result.AccessToken,
            result.RefreshToken,
            result.ExpiresIn,
            result.User
        }));
    }

    [HttpPost("otp/send")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        var command = new SendOtpCommand(request.Email, request.Purpose, request.IsResend);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "OTP sent successfully."));
    }

    [HttpPost("otp/verify")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var userAgent = Request.Headers["User-Agent"].ToString();
        var command = new VerifyOtpCommand(request.Email, request.Code, request.Purpose, userAgent);
        var result  = await _mediator.Send(command);
        
        return Ok(new ApiResponse(true, "Verification success.", result));
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var userAgent = Request.Headers["User-Agent"].ToString();
        var command = new RefreshTokenCommand(request.RefreshToken, userAgent);
        var result = await _mediator.Send(command);

        return Ok(new
        {
            result.AccessToken,
            result.RefreshToken
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var command = new ResetPasswordCommand(request.VerificationToken, request.NewPassword);
        await _mediator.Send(command);
        return Ok(new ApiResponse(true, "Password reset successfully."));
    }

    public record LoginRequest(string Email, string Password);
    public record RefreshTokenRequest(string RefreshToken);
    public record SendOtpRequest(string Email, OtpPurpose Purpose, bool IsResend = false);
    public record VerifyOtpRequest(string Email, string Code, OtpPurpose Purpose);
    public record ResetPasswordRequest(string VerificationToken, string NewPassword);
}