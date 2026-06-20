using MediatR;
using UavPms.Application.Features.Auth.DTOs;
using UavPms.Core.Enums;

namespace UavPms.Application.Features.Auth.Commands.VerifyOtp;

public record VerifyOtpCommand(
    string Email,
    string Code,
    OtpPurpose OtpPurpose,
    string? UserAgent
) : IRequest<OtpVerifyResultDto>;