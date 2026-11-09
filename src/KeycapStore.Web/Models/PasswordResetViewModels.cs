using System.ComponentModel.DataAnnotations;

namespace KeycapStore.Web.Models;

public sealed class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordViewModel
{
    public string UserId { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nova senha")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a nova senha.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "As senhas não conferem.")]
    [Display(Name = "Confirmar a nova senha")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
