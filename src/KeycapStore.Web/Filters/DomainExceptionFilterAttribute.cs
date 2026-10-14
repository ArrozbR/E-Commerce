using KeycapStore.Domain.Carts;
using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KeycapStore.Web.Filters;

public sealed class DomainExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        int? statusCode = context.Exception switch
        {
            InvalidOrderTransitionException => StatusCodes.Status409Conflict,
            ProductNotFoundException or CartItemNotFoundException or OrderNotFoundException => StatusCodes.Status404NotFound,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => null,
        };

        if (statusCode is null)
        {
            return;
        }

        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<DomainExceptionFilterAttribute>>();
        logger.LogWarning("Requisição recusada com {StatusCode}: {Reason}", statusCode, context.Exception.Message);

        context.Result = new StatusCodeResult(statusCode.Value);
        context.ExceptionHandled = true;
    }
}
