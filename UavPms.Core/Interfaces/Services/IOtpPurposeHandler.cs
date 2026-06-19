using System.Security.Claims;
using System.Threading.Tasks;
using UavPms.Core.Enums;

namespace UavPms.Core.Interfaces.Services;

public class PreconditionResult
{
    public bool IsValid { get; }
    public string Message { get; }
    public bool ShouldSilentSuccess { get; }

    public static PreconditionResult Success() => new(true, string.Empty);
    public static PreconditionResult Failure(string message) => new(false, message);
    public static PreconditionResult SilentSuccess() => new(true, string.Empty, true);

    private PreconditionResult(bool isValid, string message, bool shouldSilentSuccess = false)
    {
        IsValid = isValid;
        Message = message;
        ShouldSilentSuccess = shouldSilentSuccess;
    }
}

public interface IOtpPurposeHandler
{
    OtpPurpose Purpose { get; }
    bool RequiresAuthentication { get; }
    Task<PreconditionResult> ValidatePreconditionAsync(string? email, ClaimsPrincipal? currentUser);
}
