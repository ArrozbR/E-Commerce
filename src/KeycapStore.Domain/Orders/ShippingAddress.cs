namespace KeycapStore.Domain.Orders;

public sealed class ShippingAddress
{
    private static readonly HashSet<string> States =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    ];

    public string RecipientName { get; private set; }
    public string Street { get; private set; }
    public string Number { get; private set; }
    public string? Complement { get; private set; }
    public string District { get; private set; }
    public string City { get; private set; }
    public string State { get; private set; }
    public string PostalCode { get; private set; }

    public ShippingAddress(
        string recipientName,
        string street,
        string number,
        string? complement,
        string district,
        string city,
        string state,
        string postalCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(district);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);

        state = state.Trim().ToUpperInvariant();
        if (!States.Contains(state))
        {
            throw new ArgumentException("UF inválida", nameof(state));
        }

        postalCode = postalCode.Replace("-", "").Replace(" ", "");
        if (postalCode.Length != 8 || !postalCode.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("CEP inválido", nameof(postalCode));
        }

        RecipientName = recipientName;
        Street = street;
        Number = number;
        Complement = string.IsNullOrWhiteSpace(complement) ? null : complement;
        District = district;
        City = city;
        State = state;
        PostalCode = postalCode;
    }
}
