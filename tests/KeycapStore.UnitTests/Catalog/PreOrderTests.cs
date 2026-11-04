using KeycapStore.Domain.Catalog;

namespace KeycapStore.UnitTests.Catalog;

public class PreOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidPreOrder_KeepsTheValues_InUtc()
    {
        var brasilia = TimeSpan.FromHours(-3);

        var preOrder = new PreOrder(new DateTimeOffset(2026, 11, 20, 23, 59, 0, brasilia), new DateTimeOffset(2026, 12, 15, 0, 0, 0, brasilia), 20);

        Assert.Equal(new DateTimeOffset(2026, 11, 21, 2, 59, 0, TimeSpan.Zero), preOrder.EndsAt);
        Assert.Equal(TimeSpan.Zero, preOrder.EndsAt.Offset);
        Assert.Equal(TimeSpan.Zero, preOrder.ShipsAt.Offset);
        Assert.Equal(20, preOrder.MinimumUnits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ShipsAtNotAfterTheEnd_Throws(int daysAfterTheEnd)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreOrder(Now.AddDays(10), Now.AddDays(10 + daysAfterTheEnd), 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void MinimumOfZeroOrLess_Throws(int minimum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreOrder(Now.AddDays(10), Now.AddDays(30), minimum));
    }

    [Fact]
    public void IsOpen_UntilTheEnd()
    {
        var preOrder = new PreOrder(Now.AddDays(10), Now.AddDays(30), 5);

        Assert.True(preOrder.IsOpenAt(Now));
        Assert.True(preOrder.IsOpenAt(Now.AddDays(10).AddSeconds(-1)));
        Assert.False(preOrder.IsOpenAt(Now.AddDays(10)));
    }
}
