using System.ComponentModel.DataAnnotations;

namespace KeycapStore.Infrastructure.Orders;

public sealed class MelhorEnvioOptions
{
    public const string SectionName = "MelhorEnvio";

    [Required(ErrorMessage = "Falta a configuração 'MelhorEnvio:Token' (token pessoal do Melhor Envio).")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Falta a configuração 'MelhorEnvio:BaseUrl' (endereço da API do Melhor Envio).")]
    [Url(ErrorMessage = "'MelhorEnvio:BaseUrl' não é um endereço válido.")]
    public string BaseUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Falta a configuração 'MelhorEnvio:OriginPostalCode' (CEP de onde os pacotes saem).")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "'MelhorEnvio:OriginPostalCode' precisa ter 8 dígitos, sem traço.")]
    public string OriginPostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Falta a configuração 'MelhorEnvio:ContactEmail' (e-mail de contato exigido pelo Melhor Envio).")]
    [EmailAddress(ErrorMessage = "'MelhorEnvio:ContactEmail' não é um e-mail válido.")]
    public string ContactEmail { get; set; } = string.Empty;
}
