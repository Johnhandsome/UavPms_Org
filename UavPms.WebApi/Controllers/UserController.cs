using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Core.Interfaces.Services;
using UavPms.WebApi.Controllers;
using UavPms.WebApi.Filters;
using UavPms.Core.Contracts;

namespace UavPms.WebApi.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize] // Require standard JWT authentication first
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public UserController(IUserRepository userRepository, IPasswordHasher passwordHasher, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
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

        return Ok(new ApiResponse(true, "Password changed successfully using Step-Up authentication."));
    }
}

public record ChangePasswordRequest(string NewPassword);
