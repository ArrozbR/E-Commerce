using KeycapStore.Application.Orders;

namespace KeycapStore.Web.Models;

public sealed record PaymentPageViewModel(OrderView Order, PaymentForm Form);
