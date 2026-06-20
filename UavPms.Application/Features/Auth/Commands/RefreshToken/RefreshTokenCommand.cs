using MediatR;
using UavPms.Application.Features.Auth.DTOs;

namespace UavPms.Application.Features.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(
    string RefreshToken,
    string? UserAgent
) : IRequest<AuthResultDto>;