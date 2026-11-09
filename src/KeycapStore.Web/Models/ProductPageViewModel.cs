using KeycapStore.Application.Catalog;

namespace KeycapStore.Web.Models;

public sealed record ProductPageViewModel(ProductSummary Product, IReadOnlyList<ProductSummary> Related, bool? AlertRequested = null)
{
    public bool OffersAlert => !Product.CanBeBought && Product.PreOrder is null;
}

public sealed record KitCard(ProductSummary Product, int HeadingLevel = 2)
{
    public bool OffersAlert => !Product.CanBeBought && Product.PreOrder is null;
}
