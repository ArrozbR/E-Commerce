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
        var text = WriterFor(order.Language);
        var number = order.Id.ToString()[..8];

        var content = kind switch
        {
            EmailKind.OrderPaid => Paid(text, order, number),
            EmailKind.OrderShipped => Shipped(text, order, number),
            EmailKind.OrderRefunded => Refunded(text, order, number, batchCancelled),
            EmailKind.WithdrawalReceived => Withdrawal(text, order, number, queuedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de e-mail desconhecido."),
        };

        return Render(text, to, content, text.Pick($"Pedido {number}", $"Order {number}"), text.Pick("Ver o pedido", "View the order"), orderLink);
    }

    public static EmailMessage PasswordReset(string language, string to, Uri resetLink, TimeSpan lifetime)
    {
        var text = WriterFor(language);
        var minutes = (int)lifetime.TotalMinutes;
        var content = new Content(
            text.Pick("Trocar a senha da sua conta", "Reset your password"),
            text.Pick("Trocar a senha", "Reset your password"),
            [
                text.Pick("Recebemos um pedido para trocar a senha da sua conta na KeycapStore.", "We received a request to reset the password of your KeycapStore account."),
                text.Pick(
                    $"Use o botão abaixo para escolher uma senha nova. O link vale por {minutes} minutos e só funciona uma vez.",
                    $"Use the button below to choose a new password. The link is valid for {minutes} minutes and works only once."),
                text.Pick("Se não foi você que pediu, ignore este e-mail: a sua senha continua a mesma.", "If you did not ask for this, ignore this e-mail: your password stays the same."),
            ],
            [],
            null,
            null);

        return Render(text, to, content, AccountLabel(text), text.Pick("Escolher a senha nova", "Choose a new password"), resetLink);
    }

    public static EmailMessage PasswordResetForGoogle(string language, string to, Uri loginLink)
    {
        var text = WriterFor(language);
        var content = new Content(
            text.Pick("Trocar a senha da sua conta", "Reset your password"),
            text.Pick("Sua conta entra pelo Google", "Your account signs in with Google"),
            [
                text.Pick(
                    "Recebemos um pedido para trocar a senha da sua conta, mas ela não tem senha: você entra pelo botão \"Entrar com o Google\" na página de entrar.",
                    "We received a request to reset the password of your account, but it has no password: you sign in with the \"Sign in with Google\" button on the sign-in page."),
                text.Pick("Se não foi você que pediu, ignore este e-mail.", "If you did not ask for this, ignore this e-mail."),
            ],
            [],
            null,
            null);

        return Render(text, to, content, AccountLabel(text), text.Pick("Ir para a página de entrar", "Go to the sign-in page"), loginLink);
    }

    public static EmailMessage PasswordResetForAdmin(string language, string to)
    {
        var text = WriterFor(language);
        var content = new Content(
            text.Pick("Trocar a senha da sua conta", "Reset your password"),
            text.Pick("Conta de administrador", "Administrator account"),
            [
                text.Pick(
                    "Recebemos um pedido para trocar a senha desta conta pelo e-mail. Contas de administrador não trocam a senha assim: use o comando reset-admin-password no servidor.",
                    "We received a request to reset the password of this account by e-mail. Administrator accounts cannot do that: use the reset-admin-password command on the server."),
                text.Pick(
                    "Se não foi você que pediu, alguém digitou o e-mail desta conta na página \"Esqueci minha senha\". Nada foi mudado.",
                    "If you did not ask for this, someone typed the e-mail of this account on the \"Forgot your password\" page. Nothing was changed."),
            ],
            [],
            null,
            null);

        return Render(text, to, content, AccountLabel(text), null, null);
    }

    public static EmailMessage PasswordChanged(string language, string to, Uri loginLink, DateTimeOffset changedAt)
    {
        var text = WriterFor(language);
        var content = new Content(
            text.Pick("A senha da sua conta foi trocada", "Your password was changed"),
            text.Pick("Senha trocada", "Password changed"),
            [
                text.Pick(
                    $"A senha da sua conta na KeycapStore foi trocada em {text.DateAndTime(changedAt)}. As sessões abertas em outros aparelhos foram encerradas.",
                    $"The password of your KeycapStore account was changed on {text.DateAndTime(changedAt)}. Sessions open on other devices were signed out."),
                text.Pick(
                    "Se não foi você, peça uma senha nova em \"Esqueci minha senha\" agora e confira quem tem acesso à sua caixa de e-mail.",
                    "If this was not you, ask for a new password on \"Forgot your password\" right away and check who has access to your mailbox."),
            ],
            [],
            null,
            null);

        return Render(text, to, content, AccountLabel(text), text.Pick("Entrar", "Sign in"), loginLink);
    }

    public static EmailMessage BackInStock(string language, string to, string productName, decimal price, Uri productLink)
    {
        var text = WriterFor(language);
        var content = new Content(
            text.Pick($"{productName} voltou ao estoque", $"{productName} is back in stock"),
            text.Pick("Voltou ao estoque", "Back in stock"),
            [
                text.Pick($"O {productName} que você pediu para acompanhar voltou ao estoque.", $"The {productName} you asked us to watch is back in stock."),
                text.Pick(
                    "As unidades são limitadas: avisamos todo mundo que pediu ao mesmo tempo, e elas ficam com quem finalizar a compra primeiro.",
                    "Units are limited: everyone who asked was told at the same time, and they go to whoever checks out first."),
                text.Pick(
                    "Este foi o único aviso. Se ele esgotar de novo, peça outro na página do kit.",
                    "This was the only alert. If it sells out again, ask for another one on the kit page."),
            ],
            [new Row(productName, text.Price(price))],
            null,
            null);

        return Render(text, to, content, text.Pick("Avise-me quando voltar", "Back-in-stock alert"), text.Pick("Ver o kit", "View the kit"), productLink);
    }

    public static EmailMessage NewKit(string language, string to, string productName, decimal price, Uri productLink, Uri unsubscribeLink)
    {
        var text = WriterFor(language);
        var content = new Content(
            text.Pick($"Kit novo na loja: {productName}", $"New kit in the store: {productName}"),
            text.Pick("Kit novo na loja", "New kit in the store"),
            [
                text.Pick($"O {productName} acabou de entrar à venda.", $"The {productName} is now on sale."),
                text.Pick("Os lotes são limitados: quando acabam, só voltam se houver reposição.", "Batches are limited: once they sell out, they only come back if restocked."),
                NewsReason(text),
            ],
            [new Row(productName, text.Price(price))],
            null,
            null);

        return Render(text, to, content, NewsLabel(text), text.Pick("Ver o kit", "View the kit"), productLink, unsubscribeLink);
    }

    public static EmailMessage PreOrderOpened(string language, string to, string productName, decimal price, DateTimeOffset endsAt, DateTimeOffset shipsAt, Uri productLink, Uri unsubscribeLink)
    {
        var text = WriterFor(language);
        var content = new Content(
            text.Pick($"Pré-venda aberta: {productName}", $"Pre-order open: {productName}"),
            text.Pick("Pré-venda aberta", "Pre-order open"),
            [
                text.Pick(
                    $"A pré-venda do {productName} está aberta até {text.DateAndTime(endsAt)}, com envio previsto para {text.Date(shipsAt)}.",
                    $"The pre-order for {productName} is open until {text.DateAndTime(endsAt)}, with shipping expected on {text.Date(shipsAt)}."),
                text.Pick(
                    "O pagamento é na hora. Se o lote não atingir o mínimo de vendas, todo mundo recebe o valor de volta.",
                    "You pay right away. If the batch does not reach the minimum number of sales, everyone gets their money back."),
                NewsReason(text),
            ],
            [new Row(productName, text.Price(price))],
            null,
            null);

        return Render(text, to, content, NewsLabel(text), text.Pick("Ver a pré-venda", "View the pre-order"), productLink, unsubscribeLink);
    }

    public static EmailMessage EmailChangeConfirm(string language, string to, Uri confirmLink, TimeSpan lifetime)
    {
        var text = WriterFor(language);
        var minutes = (int)lifetime.TotalMinutes;
        var content = new Content(
            text.Pick("Confirme o seu e-mail novo", "Confirm your new e-mail"),
            text.Pick("Confirme o e-mail novo", "Confirm the new e-mail"),
            [
                text.Pick("Pediram para usar este endereço como o e-mail de uma conta na KeycapStore.", "Someone asked to use this address as the e-mail of a KeycapStore account."),
                text.Pick(
                    $"Use o botão abaixo para confirmar. A troca só acontece depois disso. O link vale por {minutes} minutos e só funciona uma vez.",
                    $"Use the button below to confirm. The change only happens after that. The link is valid for {minutes} minutes and works only once."),
                text.Pick("Se não foi você, ignore este e-mail: nada muda.", "If this was not you, ignore this e-mail: nothing changes."),
            ],
            [],
            null,
            null);

        return Render(text, to, content, AccountLabel(text), text.Pick("Confirmar o e-mail novo", "Confirm the new e-mail"), confirmLink);
    }

    public static EmailMessage EmailChanged(string language, string to, string maskedNewEmail, DateTimeOffset changedAt)
    {
        var text = WriterFor(language);
        var content = new Content(
            text.Pick("O e-mail da sua conta foi trocado", "The e-mail of your account was changed"),
            text.Pick("E-mail trocado", "E-mail changed"),
            [
                text.Pick(
                    $"O e-mail da sua conta na KeycapStore foi trocado para {maskedNewEmail} em {text.DateAndTime(changedAt)}. As sessões abertas foram encerradas.",
                    $"The e-mail of your KeycapStore account was changed to {maskedNewEmail} on {text.DateAndTime(changedAt)}. Open sessions were signed out."),
                text.Pick(
                    "A partir de agora, este endereço não recebe mais os e-mails da conta.",
                    "From now on, this address no longer gets the account e-mails."),
                text.Pick(
                    "Se não foi você, responda este e-mail para falar com a loja.",
                    "If this was not you, reply to this e-mail to contact the store."),
            ],
            [],
            null,
            null);

        return Render(text, to, content, AccountLabel(text), null, null);
    }

    private static string NewsLabel(Writer text) => text.Pick("Novidades", "News");

    private static string NewsReason(Writer text) => text.Pick(
        "Você recebe este e-mail porque ligou as novidades na Minha conta. Para desligar, use o link no fim deste e-mail ou a Minha conta.",
        "You get this e-mail because you turned on news in My account. To turn it off, use the link at the end of this e-mail or My account.");

    private static Writer WriterFor(string language)
    {
        var english = language == StripeCheckoutSessionFactory.EnglishLanguage;
        return new Writer(english, CultureInfo.GetCultureInfo(english ? "en-US" : "pt-BR"));
    }

    private static string AccountLabel(Writer text) => text.Pick("Sua conta", "Your account");

    private static EmailMessage Render(Writer text, string to, Content content, string eyebrow, string? linkLabel, Uri? link, Uri? unsubscribe = null)
    {
        var frame = new Frame(
            text.HtmlLanguage,
            eyebrow,
            linkLabel,
            text.Pick(
                "KeycapStore é uma loja de portfólio em modo de teste: nenhum valor é cobrado de verdade.",
                "KeycapStore is a portfolio store in test mode: no real money is charged."),
            unsubscribe,
            text.Pick("Não quero mais receber estes e-mails", "Stop receiving these e-mails"));

        return new EmailMessage(to, content.Subject, PlainText(content, frame, link), Html(content, frame, link), unsubscribe);
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

    private static string PlainText(Content content, Frame frame, Uri? link)
    {
        var body = new StringBuilder();
        body.Append(content.Heading).Append(" · ").Append(frame.Eyebrow).Append("\r\n\r\n");

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

        if (link is not null && frame.LinkLabel is not null)
        {
            body.Append(frame.LinkLabel).Append(": ").Append(link).Append("\r\n\r\n");
        }

        body.Append("--\r\n").Append(frame.Footer).Append("\r\n");
        if (frame.Unsubscribe is { } unsubscribe)
        {
            body.Append(frame.UnsubscribeLabel).Append(": ").Append(unsubscribe).Append("\r\n");
        }

        return body.ToString();
    }

    private static string Html(Content content, Frame frame, Uri? link)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"").Append(frame.HtmlLanguage).Append("\"><head>")
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
            .Append(E(frame.Eyebrow)).Append("</p>");
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

        if (link is not null && frame.LinkLabel is not null)
        {
            html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin-top: 8px;\"><tr>")
                .Append("<td style=\"background-color: ").Append(Brand).Append("; border-radius: 10px; border-bottom: 4px solid ").Append(BrandEdge).Append(";\">")
                .Append("<a href=\"").Append(E(link.ToString())).Append("\" style=\"display: inline-block; padding: 12px 24px; ").Append(FontFamily)
                .Append(" font-size: 15px; font-weight: 700; color: #ffffff; text-decoration: none;\">")
                .Append(E(frame.LinkLabel)).Append("</a></td></tr></table>");
        }

        html.Append("</td></tr>");

        html.Append("<tr><td align=\"center\" style=\"padding: 20px 12px 0; ").Append(FontFamily).Append(" font-size: 12px; line-height: 1.5; color: ").Append(Muted).Append(";\">")
            .Append(E(frame.Footer));
        if (frame.Unsubscribe is { } unsubscribe)
        {
            html.Append("<br /><a href=\"").Append(E(unsubscribe.ToString())).Append("\" style=\"color: ").Append(Muted).Append("; text-decoration: underline;\">")
                .Append(E(frame.UnsubscribeLabel)).Append("</a>");
        }

        html.Append("</td></tr>");

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

    private sealed record Frame(string HtmlLanguage, string Eyebrow, string? LinkLabel, string Footer, Uri? Unsubscribe, string UnsubscribeLabel);

    private sealed class Writer(bool english, CultureInfo culture)
    {
        public string HtmlLanguage => english ? "en" : "pt-BR";

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
