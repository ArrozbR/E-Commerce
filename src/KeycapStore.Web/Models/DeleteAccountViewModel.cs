using System.ComponentModel.DataAnnotations;

namespace KeycapStore.Web.Models;

public sealed class DeleteAccountViewModel
{
    [Required(ErrorMessage = "Preencha a confirmação.")]
    public string Confirmation { get; set; } = string.Empty;
}
