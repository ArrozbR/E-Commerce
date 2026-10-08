using System.ComponentModel.DataAnnotations;

namespace KeycapStore.Web.Models;

public sealed class CheckoutViewModel
{
    [Required(ErrorMessage = "Informe o nome de quem vai receber.")]
    [StringLength(150, ErrorMessage = "Use no máximo 150 caracteres.")]
    [Display(Name = "Nome de quem recebe")]
    public string RecipientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a rua.")]
    [StringLength(200, ErrorMessage = "Use no máximo 200 caracteres.")]
    [Display(Name = "Rua")]
    public string Street { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o número.")]
    [StringLength(20, ErrorMessage = "Use no máximo 20 caracteres.")]
    [Display(Name = "Número")]
    public string Number { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Use no máximo 100 caracteres.")]
    [Display(Name = "Complemento (opcional)")]
    public string? Complement { get; set; }

    [Required(ErrorMessage = "Informe o bairro.")]
    [StringLength(100, ErrorMessage = "Use no máximo 100 caracteres.")]
    [Display(Name = "Bairro")]
    public string District { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a cidade.")]
    [StringLength(100, ErrorMessage = "Use no máximo 100 caracteres.")]
    [Display(Name = "Cidade")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha o estado.")]
    [RegularExpression("^(AC|AL|AP|AM|BA|CE|DF|ES|GO|MA|MT|MS|MG|PA|PB|PR|PE|PI|RJ|RN|RS|RO|RR|SC|SP|SE|TO)$", ErrorMessage = "Escolha um estado da lista.")]
    [Display(Name = "Estado")]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o CEP.")]
    [RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "CEP inválido. Use 8 números, como 01310-100.")]
    [Display(Name = "CEP")]
    public string PostalCode { get; set; } = string.Empty;
}
