using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using UavPms.WebApi.Filters;
using UavPms.Core.Contracts;
using MediatR;
using UavPms.Application.Features.Users.Queries.GetMyProfile;

using Asp.Versioning;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/users")]
[ApiVersion("1.0")]
[Authorize] // Require standard JWT authentication first
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOtpService _otpService;
    private readonly ISender _mediator;

    public UserController(
        IUserRepository userRepository, 
        IPasswordHasher passwordHasher, 
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        ISender mediator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _mediator = mediator;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userIdString =  User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new ApiResponse(false, "Invalid user token."));
        }
        
        var result = await _mediator.Send(new GetMyProfileQuery(userId));
        
        return Ok(new ApiResponse(true, "Profile retrieved successfully.", result));
    }

    [HttpPost("change-password")]
    [RequireStepUp("ChangePassword")] // Requires Step-Up Token with purpose "ChangePassword"
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.NewPassword))
        {
            return BadRequest(new ApiResponse(false, "New password is required."));
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized(new ApiResponse(false, "Invalid user token."));
        }

        var user = await _userRepository.GetByIdAsync(userId, track: true);
        if (user == null)
        {
            return NotFound(new ApiResponse(false, "User not found."));
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate step-up token on Redis immediately (single-use)
        await _otpService.DeleteStepUpTokenAsync(userIdString, "ChangePassword");

        return Ok(new ApiResponse(true, "Password changed successfully using Step-Up authentication."));
    }
}

public record ChangePasswordRequest(string NewPassword);
