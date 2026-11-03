using System.ComponentModel.DataAnnotations;

using KeycapStore.Application.Orders;

using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KeycapStore.Web.Models;

public sealed class CheckoutViewModel : AddressViewModel
{
    [Display(Name = "Salvar este endereço na minha conta para as próximas compras")]
    public bool SaveAddress { get; set; }

    [Required(ErrorMessage = "Escolha uma opção de frete.")]
    public int? ShippingServiceId { get; set; }

    [BindNever]
    public IReadOnlyList<ShippingOption> ShippingOptions { get; set; } = [];
}
