using System.Globalization;

using KeycapStore.Web.Localization;

namespace KeycapStore.UnitTests.Web;

public class DisplayTests
{
    private static T In<T>(string culture, Func<T> format)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return format();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(349.90, "R$ 349,90")]
    [InlineData(1049.70, "R$ 1.049,70")]
    public void Price_InPortuguese_UsesTheBrazilianFormat(decimal amount, string expected)
    {
        Assert.Equal(expected, In("pt-BR", () => Display.Price(amount)).Replace('\u00A0', ' '));
    }

    [Theory]
    [InlineData(349.90, "R$349.90")]
    [InlineData(1049.70, "R$1,049.70")]
    public void Price_InEnglish_UsesTheAmericanFormat_ButStaysInReais(decimal amount, string expected)
    {
        Assert.Equal(expected, In("en-US", () => Display.Price(amount)));
    }

    [Fact]
    public void DateAndTime_InPortuguese_IsDayMonthYear_InBrasiliaTime()
    {
        var moment = new DateTimeOffset(2026, 10, 27, 19, 23, 0, TimeSpan.Zero);

        Assert.Equal("27/10/2026 16:23", In("pt-BR", () => Display.DateAndTime(moment)));
    }

    [Fact]
    public void DateAndTime_InEnglish_IsMonthDayYear_InBrasiliaTime()
    {
        var moment = new DateTimeOffset(2026, 10, 27, 19, 23, 0, TimeSpan.Zero);

        var text = In("en-US", () => Display.DateAndTime(moment));

        Assert.StartsWith("10/27/2026 4:23", text);
        Assert.EndsWith("PM", text);
    }
}
