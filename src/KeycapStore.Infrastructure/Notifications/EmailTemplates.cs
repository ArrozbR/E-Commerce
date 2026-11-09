using System.Globalization;
using System.Net;
using System.Text;

using KeycapStore.Application.Notifications;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Payments;

namespace KeycapStore.Infrastructure.Notifications;

internal static class EmailTemplates
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static EmailMessage Compose(EmailKind kind, Order order, string to, Uri orderLink, DateTimeOffset queuedAt, bool batchCancelled)
    {
        var english = order.Language == StripeCheckoutSessionFactory.EnglishLanguage;
        var culture = CultureInfo.GetCultureInfo(english ? "en-US" : "pt-BR");
        var text = new Writer(english, culture);
        var number = order.Id.ToString()[..8];

        var content = kind switch
        {
            EmailKind.OrderPaid => Paid(text, order, number),
            EmailKind.OrderShipped => Shipped(text, order, number),
            EmailKind.OrderRefunded => Refunded(text, order, number, batchCancelled),
            EmailKind.WithdrawalReceived => Withdrawal(text, order, number, queuedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de e-mail desconhecido."),
        };

        var linkLabel = text.Pick("Ver o pedido", "View the order");
        var footer = text.Pick(
            "KeycapStore é uma loja de portfólio em modo de teste: nenhum valor é cobrado de verdade.",
            "KeycapStore is a portfolio store in test mode: no real money is charged.");

        return new EmailMessage(
            to,
            content.Subject,
            PlainText(content, orderLink, linkLabel, footer),
            Html(content, orderLink, linkLabel, footer));
    }

    private static Content Paid(Writer text, Order order, string number)
    {
        var lines = order.Items
            .Select(i => $"{i.Quantity} × {i.ProductName}: {text.Price(i.LineTotal)}")
            .ToList();
        if (order.ShippingFee > 0)
        {
            lines.Add(order.ShippingService is null
                ? $"{text.Pick("Frete", "Shipping")}: {text.Price(order.ShippingFee)}"
                : $"{text.Pick("Frete", "Shipping")} ({order.ShippingService}): {text.Price(order.ShippingFee)}");
        }

        lines.Add($"Total: {text.Price(order.Total)}");

        var next = order.PreOrderShipsAt is { } shipsAt
            ? text.Pick(
                $"É uma pré-venda: o envio está previsto para {text.Date(shipsAt)}. Avisamos por aqui quando o pedido sair.",
                $"This is a pre-order: shipping is expected on {text.Date(shipsAt)}. We will let you know here when it ships.")
            : text.Pick(
                "Agora vamos separar o pedido. Quando ele sair, você recebe o código de rastreio por aqui.",
                "We will now pack your order. When it ships, you will get the tracking code here.");

        return new Content(
            text.Pick($"Pagamento confirmado: pedido {number}", $"Payment confirmed: order {number}"),
            [text.Pick($"Recebemos o pagamento do seu pedido {number}.", $"We received the payment for your order {number}."), next],
            lines);
    }

    private static Content Shipped(Writer text, Order order, string number)
    {
        var shipped = order.ShippingService is null
            ? text.Pick($"O seu pedido {number} foi enviado.", $"Your order {number} has shipped.")
            : text.Pick($"O seu pedido {number} foi enviado por {order.ShippingService}.", $"Your order {number} has shipped via {order.ShippingService}.");

        return new Content(
            text.Pick($"Pedido {number} enviado", $"Order {number} shipped"),
            [shipped, text.Pick($"Código de rastreio: {order.TrackingCode}", $"Tracking code: {order.TrackingCode}")],
            []);
    }

    private static Content Refunded(Writer text, Order order, string number, bool batchCancelled)
    {
        var why = batchCancelled
            ? text.Pick(
                $"O lote da pré-venda não atingiu o mínimo de vendas, então o pedido {number} foi cancelado.",
                $"The pre-order batch did not reach the minimum number of sales, so order {number} was cancelled.")
            : text.Pick($"O pedido {number} foi cancelado.", $"Order {number} was cancelled.");

        return new Content(
            text.Pick($"Reembolso do pedido {number}", $"Refund for order {number}"),
            [
                why,
                text.Pick(
                    $"O valor de {text.Price(order.Total)} foi devolvido no mesmo meio de pagamento. O prazo para ele aparecer depende do banco ou do cartão.",
                    $"The amount of {text.Price(order.Total)} was refunded to the same payment method. How long it takes to show up depends on your bank or card."),
            ],
            []);
    }

    private static Content Withdrawal(Writer text, Order order, string number, DateTimeOffset withdrawnAt) =>
        new(
            text.Pick($"Desistência do pedido {number} recebida", $"Withdrawal from order {number} received"),
            [
                text.Pick(
                    $"Recebemos a sua desistência do pedido {number} em {text.DateAndTime(withdrawnAt)}.",
                    $"We received your withdrawal from order {number} on {text.DateAndTime(withdrawnAt)}."),
                text.Pick(
                    $"O reembolso de {text.Price(order.Total)} é feito automaticamente, no mesmo meio de pagamento. Depois, devolva o produto à loja.",
                    $"The refund of {text.Price(order.Total)} is made automatically, to the same payment method. Then, please return the product to the store."),
                text.Pick("Guarde este e-mail como comprovante da desistência.", "Keep this e-mail as proof of your withdrawal."),
            ],
            []);

    private static string PlainText(Content content, Uri link, string linkLabel, string footer)
    {
        var body = new StringBuilder();
        foreach (var paragraph in content.Paragraphs)
        {
            body.Append(paragraph).Append("\r\n\r\n");
        }

        foreach (var line in content.Lines)
        {
            body.Append("- ").Append(line).Append("\r\n");
        }

        if (content.Lines.Count > 0)
        {
            body.Append("\r\n");
        }

        body.Append(linkLabel).Append(": ").Append(link).Append("\r\n\r\n");
        body.Append("--\r\n").Append(footer).Append("\r\n");

        return body.ToString();
    }

    private static string Html(Content content, Uri link, string linkLabel, string footer)
    {
        static string Encode(string value) => WebUtility.HtmlEncode(value);

        var body = new StringBuilder("<div style=\"font-family: Arial, sans-serif; font-size: 15px; line-height: 1.5; color: #1c1917;\">");
        foreach (var paragraph in content.Paragraphs)
        {
            body.Append("<p>").Append(Encode(paragraph)).Append("</p>");
        }

        if (content.Lines.Count > 0)
        {
            body.Append("<ul>");
            foreach (var line in content.Lines)
            {
                body.Append("<li>").Append(Encode(line)).Append("</li>");
            }

            body.Append("</ul>");
        }

        body.Append("<p><a href=\"").Append(Encode(link.ToString())).Append("\">").Append(Encode(linkLabel)).Append("</a></p>");
        body.Append("<p style=\"font-size: 12px; color: #78716c;\">").Append(Encode(footer)).Append("</p>");
        body.Append("</div>");

        return body.ToString();
    }

    private sealed record Content(string Subject, IReadOnlyList<string> Paragraphs, IReadOnlyList<string> Lines);

    private sealed class Writer(bool english, CultureInfo culture)
    {
        public string Pick(string portuguese, string englishText) => english ? englishText : portuguese;

        public string Price(decimal amount)
        {
            var format = (NumberFormatInfo)culture.NumberFormat.Clone();
            format.CurrencySymbol = "R$";
            return amount.ToString("C", format);
        }

        public string Date(DateTimeOffset moment) => TimeZoneInfo.ConvertTime(moment, Brasilia).ToString("d", culture);

        public string DateAndTime(DateTimeOffset moment) => TimeZoneInfo.ConvertTime(moment, Brasilia).ToString("g", culture);
    }
}
