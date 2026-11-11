using System.ComponentModel.DataAnnotations;

namespace KeycapStore.Web.Models;

public sealed class ChangeEmailViewModel
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [Display(Name = "E-mail novo")]
    public string NewEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha atual.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha atual")]
    public string CurrentPassword { get; set; } = string.Empty;
}

public sealed record ConfirmEmailChangeViewModel(string UserId, string Token);
