using System.Globalization;

namespace KeycapStore.Web.Localization;

public static class Display
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static string Price(decimal amount)
    {
        var format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        format.CurrencySymbol = "R$";

        return amount.ToString("C", format);
    }

    public static string DateAndTime(DateTimeOffset moment) =>
        TimeZoneInfo.ConvertTime(moment, Brasilia).ToString("g", CultureInfo.CurrentCulture);

    public static string Date(DateTimeOffset moment) =>
        TimeZoneInfo.ConvertTime(moment, Brasilia).ToString("d", CultureInfo.CurrentCulture);

    public static DateTimeOffset FromBrasilia(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, Brasilia.GetUtcOffset(unspecified)).ToUniversalTime();
    }
}
