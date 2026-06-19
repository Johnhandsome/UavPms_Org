using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Services;

public interface IJwtProvider
{
    string GenerateAccessToken(User user, IList<string> roles);
    string GenerateRefreshToken();
    string GenerateStepUpToken(User user, string purpose);
}