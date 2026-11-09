using System.ComponentModel.DataAnnotations;

namespace KeycapStore.Infrastructure.Notifications;

public sealed class PublicSiteOptions
{
    public const string SectionName = "Store";

    [Required(ErrorMessage = "Falta a configuração 'Store:PublicUrl' (endereço público da loja, usado nos links dos e-mails).")]
    [Url(ErrorMessage = "'Store:PublicUrl' não é um endereço válido.")]
    public string PublicUrl { get; set; } = string.Empty;
}
