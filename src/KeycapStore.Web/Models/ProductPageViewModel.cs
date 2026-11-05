using KeycapStore.Application.Catalog;

namespace KeycapStore.Web.Models;

public sealed record ProductPageViewModel(ProductSummary Product, IReadOnlyList<ProductSummary> Related);

public sealed record KitCard(ProductSummary Product, int HeadingLevel = 2);
