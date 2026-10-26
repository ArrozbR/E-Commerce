using System.ComponentModel.DataAnnotations;

namespace KeycapStore.Web.Models;

public sealed class CheckoutViewModel : AddressViewModel
{
    [Display(Name = "Salvar este endereço na minha conta para as próximas compras")]
    public bool SaveAddress { get; set; }
}
