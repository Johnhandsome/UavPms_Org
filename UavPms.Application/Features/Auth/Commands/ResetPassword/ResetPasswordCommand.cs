using MediatR;
using UavPms.Core.Contracts;

namespace UavPms.Application.Features.Auth.Commands.ResetPassword;

public record ResetPasswordCommand(
    string VerificationToken,
    string NewPassword
) : IRequest;