using System.ComponentModel.DataAnnotations;

namespace KeycapStore.Infrastructure.Payments;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    [Required(ErrorMessage = "Falta a configuração 'Stripe:SecretKey' (chave secreta da Stripe).")]
    [RegularExpression(@"^sk_(test|live)_\S+$", ErrorMessage = "'Stripe:SecretKey' não parece uma chave secreta da Stripe (deve começar com sk_test_ ou sk_live_).")]
    public string SecretKey { get; set; } = string.Empty;
}
