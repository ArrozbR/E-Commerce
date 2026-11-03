using System.ComponentModel.DataAnnotations;
using System.Globalization;

using KeycapStore.Domain.Catalog;

namespace KeycapStore.Web.Models;

public class ProductFormViewModel
{
    public const decimal MaxPrice = 99_999_999.99m;

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(Product.MaxNameLength, ErrorMessage = "O nome pode ter até {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o preço.")]
    public string Price { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o peso.")]
    public string Weight { get; set; } = string.Empty;

    [Range(1, ProductPackage.MaxSideCm, ErrorMessage = "As medidas vão de {1} a {2} cm.")]
    public int WidthCm { get; set; }

    [Range(1, ProductPackage.MaxSideCm, ErrorMessage = "As medidas vão de {1} a {2} cm.")]
    public int HeightCm { get; set; }

    [Range(1, ProductPackage.MaxSideCm, ErrorMessage = "As medidas vão de {1} a {2} cm.")]
    public int LengthCm { get; set; }

    public bool TryGetPrice(out decimal price) =>
        decimal.TryParse(Price, NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out price)
        && price > 0
        && price <= MaxPrice
        && decimal.Round(price, 2) == price;

    public bool TryGetPackage(out ProductPackage? package)
    {
        package = null;
        if (!decimal.TryParse(Weight, NumberStyles.AllowDecimalPoint, CultureInfo.CurrentCulture, out var weight)
            || weight <= 0
            || weight > ProductPackage.MaxWeightKg
            || decimal.Round(weight, 3) != weight)
        {
            return false;
        }

        package = new ProductPackage(weight, WidthCm, HeightCm, LengthCm);
        return true;
    }

    public static string FormatPrice(decimal price) => price.ToString("0.00", CultureInfo.CurrentCulture);

    public static string FormatWeight(decimal weightKg) => weightKg.ToString("0.###", CultureInfo.CurrentCulture);
}

public sealed class NewProductViewModel : ProductFormViewModel
{
    [Range(0, AdminLimits.MaxStockChange, ErrorMessage = "O estoque inicial vai de {1} a {2}.")]
    public int Stock { get; set; }
}

public static class AdminLimits
{
    public const int MaxStockChange = 100_000;
}
