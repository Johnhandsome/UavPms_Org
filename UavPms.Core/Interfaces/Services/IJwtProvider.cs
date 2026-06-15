using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Services;

public interface IJwtProvider
{
    string GenerateToken(User user, IList<string> roles);
}