using System.Text.RegularExpressions;

namespace KeycapStore.UnitTests.Web;

public partial class NoInlineScriptsTests
{
    [GeneratedRegex(@"<script(?![^>]*\bsrc=)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScript();

    [GeneratedRegex(@"\son[a-z]+\s*=\s*[""']", RegexOptions.IgnoreCase)]
    private static partial Regex InlineHandler();

    private static string ViewsFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KeycapStore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "KeycapStore.Web", "Views");
    }

    [Fact]
    public void NoView_HasAnInlineScriptOrEventHandler_SoTheCspNeverBreaksThem()
    {
        var views = Directory.GetFiles(ViewsFolder(), "*.cshtml", SearchOption.AllDirectories);
        Assert.NotEmpty(views);

        var offenders = views
            .Where(file =>
            {
                var text = File.ReadAllText(file);
                return InlineScript().IsMatch(text) || InlineHandler().IsMatch(text);
            })
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0, "Scripts ou eventos escritos dentro da página (o CSP bloqueia): " + string.Join(", ", offenders));
    }
}
