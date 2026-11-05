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

    public static string Description(KitKind kind) => kind switch
    {
        KitKind.Novelties => "Teclas decorativas com ícones, para trocar no lugar de teclas comuns e dar um toque próprio ao teclado.",
        KitKind.Modifiers => "Os modificadores (Tab, Caps, Shift, Ctrl, Alt, Enter e Backspace) nas cores invertidas do conjunto, para combinar com o kit base.",
        KitKind.FunctionKeys => "A fileira de funções (F1 a F12) e as teclas de navegação, como PrtSc, Home e End.",
        _ => "Letras, números e os modificadores principais (Esc, Tab, Shift, Ctrl e Enter) para montar o teclado inteiro nas cores do conjunto.",
    };
}
