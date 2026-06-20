using MediatR;
using UavPms.Core.Enums;

namespace UavPms.Application.Features.Auth.Commands.SendOtp;

public record SendOtpCommand (
    string Email,
    OtpPurpose OtpPurpose,
    bool IsResend = false
) : IRequest;