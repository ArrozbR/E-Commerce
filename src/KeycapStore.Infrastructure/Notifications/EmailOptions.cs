using System.ComponentModel.DataAnnotations;

namespace KeycapStore.Infrastructure.Notifications;

public sealed class EmailOptions : IValidatableObject
{
    public const string SectionName = "Email";

    [Required(ErrorMessage = "Falta a configuração 'Email:Host' (servidor de e-mail).")]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "'Email:Port' precisa ser uma porta entre 1 e 65535.")]
    public int Port { get; set; }

    public bool UseStartTls { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    [Required(ErrorMessage = "Falta a configuração 'Email:FromAddress' (endereço do remetente).")]
    [EmailAddress(ErrorMessage = "'Email:FromAddress' não é um e-mail válido.")]
    public string FromAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Falta a configuração 'Email:FromName' (nome do remetente).")]
    public string FromName { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password))
        {
            yield return new ValidationResult(
                "Falta a configuração 'Email:Password' (senha de app da conta de e-mail).",
                [nameof(Password)]);
        }

        if (!string.IsNullOrEmpty(Username) && !UseStartTls)
        {
            yield return new ValidationResult(
                "'Email:UseStartTls' precisa ser verdadeiro quando há login: a senha não pode viajar sem criptografia.",
                [nameof(UseStartTls)]);
        }
    }
}
