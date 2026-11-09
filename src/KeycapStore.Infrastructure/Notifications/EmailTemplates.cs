using System.Globalization;
using System.Net;
using System.Text;

using KeycapStore.Application.Notifications;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Payments;

namespace KeycapStore.Infrastructure.Notifications;

internal static class EmailTemplates
{
    private const string FontFamily = "font-family: -apple-system, 'Segoe UI', Roboto, Arial, sans-serif;";
    private const string Ink = "#0a0a0a";
    private const string Body = "#27272a";
    private const string Muted = "#71717a";
    private const string Line = "#e4e4e7";
    private const string Brand = "#c2410c";
    private const string BrandEdge = "#7c2d12";
    private const string LogoKey = "#f97316";

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

        var frame = new Frame(
            english ? "en" : "pt-BR",
            text.Pick($"Pedido {number}", $"Order {number}"),
            text.Pick("Ver o pedido", "View the order"),
            text.Pick(
                "KeycapStore é uma loja de portfólio em modo de teste: nenhum valor é cobrado de verdade.",
                "KeycapStore is a portfolio store in test mode: no real money is charged."));

        return new EmailMessage(
            to,
            content.Subject,
            PlainText(content, frame, orderLink),
            Html(content, frame, orderLink));
    }

    private static Content Paid(Writer text, Order order, string number)
    {
        var rows = order.Items
            .Select(i => new Row($"{i.Quantity} × {i.ProductName}", text.Price(i.LineTotal)))
            .ToList();
        if (order.ShippingFee > 0)
        {
            var shipping = text.Pick("Frete", "Shipping");
            rows.Add(new Row(order.ShippingService is null ? shipping : $"{shipping} ({order.ShippingService})", text.Price(order.ShippingFee)));
        }

        var next = order.PreOrderShipsAt is { } shipsAt
            ? text.Pick(
                $"É uma pré-venda: o envio está previsto para {text.Date(shipsAt)}. Avisamos por aqui quando o pedido sair.",
                $"This is a pre-order: shipping is expected on {text.Date(shipsAt)}. We will let you know here when it ships.")
            : text.Pick(
                "Agora vamos separar o pedido. Quando ele sair, você recebe o código de rastreio por aqui.",
                "We will now pack your order. When it ships, you will get the tracking code here.");

        return new Content(
            text.Pick($"Pagamento confirmado: pedido {number}", $"Payment confirmed: order {number}"),
            text.Pick("Pagamento confirmado", "Payment confirmed"),
            [text.Pick($"Recebemos o pagamento do seu pedido {number}.", $"We received the payment for your order {number}."), next],
            rows,
            new Row("Total", text.Price(order.Total)),
            null);
    }

    private static Content Shipped(Writer text, Order order, string number)
    {
        var shipped = order.ShippingService is null
            ? text.Pick($"O seu pedido {number} foi enviado.", $"Your order {number} has shipped.")
            : text.Pick($"O seu pedido {number} foi enviado por {order.ShippingService}.", $"Your order {number} has shipped via {order.ShippingService}.");

        return new Content(
            text.Pick($"Pedido {number} enviado", $"Order {number} shipped"),
            text.Pick("Pedido enviado", "Order shipped"),
            [shipped],
            [],
            null,
            new Row(text.Pick("Código de rastreio", "Tracking code"), order.TrackingCode ?? string.Empty));
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
            text.Pick("Reembolso feito", "Refund issued"),
            [
                why,
                text.Pick(
                    $"O valor de {text.Price(order.Total)} foi devolvido no mesmo meio de pagamento. O prazo para ele aparecer depende do banco ou do cartão.",
                    $"The amount of {text.Price(order.Total)} was refunded to the same payment method. How long it takes to show up depends on your bank or card."),
            ],
            [],
            null,
            null);
    }

    private static Content Withdrawal(Writer text, Order order, string number, DateTimeOffset withdrawnAt) =>
        new(
            text.Pick($"Desistência do pedido {number} recebida", $"Withdrawal from order {number} received"),
            text.Pick("Desistência recebida", "Withdrawal received"),
            [
                text.Pick(
                    $"Recebemos a sua desistência do pedido {number} em {text.DateAndTime(withdrawnAt)}.",
                    $"We received your withdrawal from order {number} on {text.DateAndTime(withdrawnAt)}."),
                text.Pick(
                    $"O reembolso de {text.Price(order.Total)} é feito automaticamente, no mesmo meio de pagamento. Depois, devolva o produto à loja.",
                    $"The refund of {text.Price(order.Total)} is made automatically, to the same payment method. Then, please return the product to the store."),
                text.Pick("Guarde este e-mail como comprovante da desistência.", "Keep this e-mail as proof of your withdrawal."),
            ],
            [],
            null,
            null);

    private static string PlainText(Content content, Frame frame, Uri link)
    {
        var body = new StringBuilder();
        body.Append(content.Heading).Append(" · ").Append(frame.OrderLabel).Append("\r\n\r\n");

        foreach (var paragraph in content.Paragraphs)
        {
            body.Append(paragraph).Append("\r\n\r\n");
        }

        var rows = content.Total is null ? content.Rows : [.. content.Rows, content.Total];
        foreach (var row in rows)
        {
            body.Append("- ").Append(row.Label).Append(": ").Append(row.Value).Append("\r\n");
        }

        if (rows.Count > 0)
        {
            body.Append("\r\n");
        }

        if (content.Code is { } code)
        {
            body.Append(code.Label).Append(": ").Append(code.Value).Append("\r\n\r\n");
        }

        body.Append(frame.LinkLabel).Append(": ").Append(link).Append("\r\n\r\n");
        body.Append("--\r\n").Append(frame.Footer).Append("\r\n");

        return body.ToString();
    }

    private static string Html(Content content, Frame frame, Uri link)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"").Append(frame.Language).Append("\"><head>")
            .Append("<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<meta name=\"color-scheme\" content=\"light\"><meta name=\"supported-color-schemes\" content=\"light\">")
            .Append("<title>").Append(E(content.Subject)).Append("</title></head>")
            .Append("<body style=\"margin: 0; padding: 0; background-color: #f4f4f5;\">");

        html.Append("<div style=\"display: none; max-height: 0; overflow: hidden; opacity: 0;\">")
            .Append(E(content.Paragraphs[0])).Append("</div>");

        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background-color: #f4f4f5;\"><tr><td align=\"center\" style=\"padding: 32px 16px;\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width: 560px;\">");

        html.Append("<tr><td style=\"padding: 0 4px 20px;\"><table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr>")
            .Append("<td width=\"36\" height=\"36\" align=\"center\" style=\"width: 36px; height: 36px; background-color: ").Append(LogoKey)
            .Append("; border-radius: 8px; border-bottom: 3px solid ").Append(Brand).Append("; ").Append(FontFamily)
            .Append(" font-size: 18px; font-weight: 700; line-height: 36px; color: ").Append(Ink).Append(";\">K</td>")
            .Append("<td style=\"padding-left: 10px; ").Append(FontFamily).Append(" font-size: 20px; font-weight: 700; color: ").Append(Ink).Append(";\">KeycapStore</td>")
            .Append("</tr></table></td></tr>");

        html.Append("<tr><td style=\"background-color: #ffffff; border: 1px solid ").Append(Line)
            .Append("; border-bottom: 4px solid #d4d4d8; border-radius: 16px; padding: 32px 28px;\">");

        html.Append("<p style=\"margin: 0 0 6px; ").Append(FontFamily).Append(" font-size: 13px; font-weight: 600; letter-spacing: 0.04em; text-transform: uppercase; color: ").Append(Brand).Append(";\">")
            .Append(E(frame.OrderLabel)).Append("</p>");
        html.Append("<h1 style=\"margin: 0 0 20px; ").Append(FontFamily).Append(" font-size: 26px; font-weight: 700; line-height: 1.25; color: ").Append(Ink).Append(";\">")
            .Append(E(content.Heading)).Append("</h1>");

        foreach (var paragraph in content.Paragraphs)
        {
            html.Append("<p style=\"margin: 0 0 16px; ").Append(FontFamily).Append(" font-size: 15px; line-height: 1.6; color: ").Append(Body).Append(";\">")
                .Append(E(paragraph)).Append("</p>");
        }

        if (content.Rows.Count > 0 || content.Total is not null)
        {
            html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin: 8px 0 24px; border-top: 1px solid ").Append(Line).Append(";\">");
            foreach (var row in content.Rows)
            {
                AppendRow(html, row, bold: false);
            }

            if (content.Total is { } total)
            {
                AppendRow(html, total, bold: true);
            }

            html.Append("</table>");
        }

        if (content.Code is { } code)
        {
            html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin: 4px 0 24px;\"><tr>")
                .Append("<td style=\"background-color: #fafafa; border: 1px solid ").Append(Line).Append("; border-bottom: 3px solid #d4d4d8; border-radius: 10px; padding: 12px 18px;\">")
                .Append("<div style=\"").Append(FontFamily).Append(" font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.04em; color: ").Append(Muted).Append(";\">")
                .Append(E(code.Label)).Append("</div>")
                .Append("<div style=\"margin-top: 4px; font-family: 'SFMono-Regular', Consolas, 'Courier New', monospace; font-size: 18px; font-weight: 700; letter-spacing: 0.06em; color: ").Append(Ink).Append(";\">")
                .Append(E(code.Value)).Append("</div>")
                .Append("</td></tr></table>");
        }

        html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin-top: 8px;\"><tr>")
            .Append("<td style=\"background-color: ").Append(Brand).Append("; border-radius: 10px; border-bottom: 4px solid ").Append(BrandEdge).Append(";\">")
            .Append("<a href=\"").Append(E(link.ToString())).Append("\" style=\"display: inline-block; padding: 12px 24px; ").Append(FontFamily)
            .Append(" font-size: 15px; font-weight: 700; color: #ffffff; text-decoration: none;\">")
            .Append(E(frame.LinkLabel)).Append("</a></td></tr></table>");

        html.Append("</td></tr>");

        html.Append("<tr><td align=\"center\" style=\"padding: 20px 12px 0; ").Append(FontFamily).Append(" font-size: 12px; line-height: 1.5; color: ").Append(Muted).Append(";\">")
            .Append(E(frame.Footer)).Append("</td></tr>");

        html.Append("</table></td></tr></table></body></html>");

        return html.ToString();

        static void AppendRow(StringBuilder html, Row row, bool bold)
        {
            var weight = bold ? "700" : "400";
            var size = bold ? "17px" : "15px";
            var border = bold ? "none" : $"1px solid {Line}";
            var cell = $"padding: 10px 0; border-bottom: {border}; {FontFamily} font-size: {size}; font-weight: {weight}; color: {(bold ? Ink : Body)};";

            html.Append("<tr><td style=\"").Append(cell).Append("\">").Append(E(row.Label)).Append("</td>")
                .Append("<td align=\"right\" style=\"").Append(cell).Append(" white-space: nowrap; padding-left: 16px;\">").Append(E(row.Value)).Append("</td></tr>");
        }
    }

    private sealed record Content(string Subject, string Heading, IReadOnlyList<string> Paragraphs, IReadOnlyList<Row> Rows, Row? Total, Row? Code);

    private sealed record Row(string Label, string Value);

    private sealed record Frame(string Language, string OrderLabel, string LinkLabel, string Footer);

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
