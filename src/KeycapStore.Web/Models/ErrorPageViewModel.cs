using System.Globalization;

namespace KeycapStore.Web.Models;

public sealed record ErrorPageViewModel(int StatusCode, string? SupportCode)
{
    public string Digits => StatusCode.ToString(CultureInfo.InvariantCulture);

    public string Title => StatusCode switch
    {
        400 => "Não foi possível entender o pedido",
        403 => "Você não tem acesso a esta página",
        404 => "Página não encontrada",
        409 => "Essa ação não pode mais ser feita",
        429 => "Muitas tentativas seguidas",
        < 500 => "Não foi possível continuar",
        _ => "Algo deu errado do nosso lado",
    };

    public string Message => StatusCode switch
    {
        400 => "Algo na requisição veio diferente do esperado. Volte e tente de novo.",
        403 => "Esta página é restrita. Se você deveria ter acesso, entre com outra conta.",
        404 => "O endereço pode ter mudado ou o item não existe mais.",
        409 => "A situação mudou enquanto você estava na página, por exemplo, o pedido já avançou. Recarregue para ver como está agora.",
        429 => "Espere um minuto e tente de novo.",
        < 500 => "Volte e tente de novo.",
        _ => "Tente de novo em alguns minutos. Se continuar, fale com a loja informando o código abaixo.",
    };
}
