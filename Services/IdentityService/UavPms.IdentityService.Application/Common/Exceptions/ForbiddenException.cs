using System;

namespace UavPms.IdentityService.Application.Common.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
