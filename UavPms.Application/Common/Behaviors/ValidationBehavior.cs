using FluentValidation;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UavPms.Application.Common.Behaviors;

public class ValidationBehavior<TRequest, TResonse> : IPipelineBehavior<TRequest, TResonse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validator;
    
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validator)
    {
        _validator = validator;
    }
    
    public async Task<TResonse> Handle(TRequest request, RequestHandlerDelegate<TResonse> next, CancellationToken cancellationToken)
    {
        if (_validator.Any())
        {
            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(
                _validator.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count != 0)
            {
                throw new ValidationException(failures);
            }
        }
        
        return await next();
    }
}