using MediatR;
using Microsoft.AspNetCore.Http;
using UavPms.Application.Common.Exceptions;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Application.Features.Auth.Commands.SendOtp;

public class SendOtpCommandHandler : IRequestHandler<SendOtpCommand>
{
    private readonly IOtpService _otpService;
    private readonly IEnumerable<IOtpPurposeHandler> _otpHandlers;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SendOtpCommandHandler(
        IOtpService otpService,
        IEnumerable<IOtpPurposeHandler> otpHandlers,
        IHttpContextAccessor httpContextAccessor)
    {
        _otpService = otpService;
        _otpHandlers = otpHandlers;
        _httpContextAccessor = httpContextAccessor;
    }
    
    public async Task Handle(SendOtpCommand request, CancellationToken cancellationToken)
    {
        var handler = _otpHandlers.FirstOrDefault(h => h.Purpose == request.OtpPurpose);
        if (handler == null)
        {
            throw new BusinessRuleException($"Unsupported OTP purpose: {request.OtpPurpose}");
        }
        
        var currentUser = _httpContextAccessor.HttpContext?.User;
        var precondition = await handler.ValidatePreconditionAsync(request.Email, currentUser);

        if (!precondition.IsValid)
        {
            throw new BusinessRuleException(precondition.Message);
        }
        
        var result = await _otpService.GenerateAndSendOtpAsync(request.Email, request.OtpPurpose, request.isResend);
        if (!result.Success)
        {
            throw new BusinessRuleException(result.Message);
        }
    }
}