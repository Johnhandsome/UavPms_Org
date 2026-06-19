using System;
using System.Threading.Tasks;
using UavPms.Core.Enums;

namespace UavPms.Core.Interfaces.Services;

public interface IOtpService
{
    Task<(bool Success, string Message)> GenerateAndSendOtpAsync(string email, OtpPurpose purpose, bool isResend = false);
    Task<(bool IsValid, string Message)> VerifyOtpAsync(string email, string code, OtpPurpose purpose);
}
