using MediatR;
using UavPms.Application.Features.Auth.DTOs;

namespace UavPms.Application.Features.Auth.Commands.Login;

public record LoginCommand(
    string Email,
    string Password,
    string? DeviceTrustToken,
    string? UserAgent
) : IRequest<AuthResultDto>;

