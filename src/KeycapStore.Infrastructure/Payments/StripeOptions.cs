using System.ComponentModel.DataAnnotations;

namespace KeycapStore.Infrastructure.Payments;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    [Required(ErrorMessage = "Falta a configuração 'Stripe:SecretKey' (chave secreta da Stripe).")]
    [RegularExpression(@"^sk_(test|live)_\S+$", ErrorMessage = "'Stripe:SecretKey' não parece uma chave secreta da Stripe (deve começar com sk_test_ ou sk_live_).")]
    public string SecretKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Falta a configuração 'Stripe:WebhookSecret' (segredo do webhook da Stripe).")]
    [RegularExpression(@"^whsec_\S+$", ErrorMessage = "'Stripe:WebhookSecret' não parece um segredo de webhook da Stripe (deve começar com whsec_).")]
    public string WebhookSecret { get; set; } = string.Empty;
}
