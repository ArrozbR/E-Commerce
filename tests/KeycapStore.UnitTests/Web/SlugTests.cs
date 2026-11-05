using KeycapStore.Web.Models;

namespace KeycapStore.UnitTests.Web;

public class SlugTests
{
    [Theory]
    [InlineData("Kit Aurora (base)", "kit-aurora-base")]
    [InlineData("Kit Maré (modificadores)", "kit-mare-modificadores")]
    [InlineData("Kit Zéfiro (teclas de função)", "kit-zefiro-teclas-de-funcao")]
    [InlineData("  Kit -- Teste 2!! ", "kit-teste-2")]
    [InlineData("@@@", "")]
    public void From_KeepsOnlyLowerCaseLettersDigitsAndSingleHyphens(string name, string slug)
    {
        Assert.Equal(slug, Slug.From(name));
    }
}
