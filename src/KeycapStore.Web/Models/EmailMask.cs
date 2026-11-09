namespace KeycapStore.Web.Models;

public static class EmailMask
{
    public static string Mask(string email)
    {
        var at = email.LastIndexOf('@');
        if (at <= 0)
        {
            return "****";
        }

        var local = email[..at];
        var hidden = local.Length <= 2
            ? $"{local[0]}****"
            : $"{local[0]}****{local[^1]}";

        return hidden + email[at..];
    }
}

public sealed record UnsubscribePageViewModel(string Token, string MaskedEmail);
