namespace KeycapStore.Domain.Orders;

public static class ShippingPolicy
{
    private static readonly HashSet<string> FreeShippingStates = ["DF", "GO", "MT", "MS", "SP", "RJ"];
    public const decimal StandardFee = 15.00m;

    public static decimal FeeFor(string state)
    {
        if (FreeShippingStates.Contains(state))
        {
            return 0m;
        }
        return StandardFee;
    }
}
