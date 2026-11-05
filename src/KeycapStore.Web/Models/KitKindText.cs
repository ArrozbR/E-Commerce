using KeycapStore.Domain.Catalog;

namespace KeycapStore.Web.Models;

public static class KitKindText
{
    public static string For(KitKind kind) => kind switch
    {
        KitKind.Novelties => "Novelties",
        KitKind.Modifiers => "Modificadores",
        KitKind.FunctionKeys => "Teclas de função",
        _ => "Base",
    };
}
