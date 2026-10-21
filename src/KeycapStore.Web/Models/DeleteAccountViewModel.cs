using System.ComponentModel.DataAnnotations;

namespace KeycapStore.Web.Models;

public sealed class DeleteAccountViewModel
{
    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha atual")]
    public string Password { get; set; } = string.Empty;
}
