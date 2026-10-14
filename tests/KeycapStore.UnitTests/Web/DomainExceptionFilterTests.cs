using KeycapStore.Domain.Carts;
using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Web.Filters;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KeycapStore.UnitTests.Web;

public class DomainExceptionFilterTests
{
    private static ExceptionContext Run(Exception exception)
    {
        var services = new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var context = new ExceptionContext(new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), [])
        {
            Exception = exception,
        };

        new DomainExceptionFilterAttribute().OnException(context);

        return context;
    }

    [Fact]
    public void InvalidOrderTransition_Becomes409()
    {
        var context = Run(new InvalidOrderTransitionException(OrderStatus.Paid, OrderStatus.Expired));

        Assert.Equal(409, Assert.IsType<StatusCodeResult>(context.Result).StatusCode);
        Assert.True(context.ExceptionHandled);
    }

    [Fact]
    public void ProductOrCartItemNotFound_Becomes404()
    {
        Assert.Equal(404, Assert.IsType<StatusCodeResult>(Run(new ProductNotFoundException(Guid.NewGuid())).Result).StatusCode);
        Assert.Equal(404, Assert.IsType<StatusCodeResult>(Run(new CartItemNotFoundException(Guid.NewGuid())).Result).StatusCode);
    }

    [Fact]
    public void InvalidArgument_Becomes400()
    {
        var context = Run(new ArgumentOutOfRangeException("quantity"));

        Assert.Equal(400, Assert.IsType<StatusCodeResult>(context.Result).StatusCode);
    }

    [Fact]
    public void AnyOtherException_IsLeftAlone_AndBecomes500()
    {
        var context = Run(new InvalidOperationException("erro de verdade"));

        Assert.Null(context.Result);
        Assert.False(context.ExceptionHandled);
    }
}
