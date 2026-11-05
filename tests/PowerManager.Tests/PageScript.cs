using ExiledToolkit.Web;
using PowerManagerMod.Core;
using Jint;

namespace PowerManagerMod.Tests;

/// <summary>
/// page.js exactly as the plugin embeds it, run in Jint with Exiled's Toolkit's page API (api.js, from the Toolkit's
/// source) on top of FakePage.js (Root.html, the game's HUD page and the vuplex bridge). The calls into the script are
/// the ones PowerPage makes, and what the script posts is parsed with the real PageCommand, so a test sees the commands
/// the plugin would act on.
/// </summary>
internal sealed class PageScript
{
    private readonly Engine _engine = new();

    /// <param name="isToolkitFirst">
    /// The game runs the Toolkit's script first, or with false Power Manager's first; then the Toolkit's runs only at
    /// <see cref="LoadToolkit"/>.
    /// </param>
    public PageScript(bool isToolkitFirst = true)
    {
        _engine.Execute(Resource("PowerManager.Tests.FakePage.js"));

        if (isToolkitFirst) _engine.Execute(ToolkitApi);

        _engine.Execute(Source);
        Hear();
    }

    /// <summary>page.js as the plugin embeds it.</summary>
    public static string Source { get; } = Resource("PowerManager.page.js");

    /// <summary>The Toolkit's page API, from its source folder.</summary>
    private static string ToolkitApi { get; } = Resource("ExiledToolkit.api.js");

    /// <summary>Every command the script sent the plugin, its ready message and diag notes aside.</summary>
    public List<PageCommand> Sent { get; } = new();

    /// <summary>The script's "diag|" notes, as the plugin logs them.</summary>
    public List<string> Notes { get; } = new();

    /// <summary>Whether the script told the plugin it is ready, which has the plugin send it everything again.</summary>
    public bool HasSaidReady { get; private set; }

    /// <summary>The Toolkit's page API arriving after Power Manager's script.</summary>
    public void LoadToolkit() => Run(ToolkitApi);

    public void Run(string statements)
    {
        _engine.Execute(statements);
        Hear();
    }

    public string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

    public bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    /// <summary>One run of the Toolkit's 250 ms page check.</summary>
    public void Poll() => Run("poll()");

    public void Click(string expression) => Run($"click({expression})");

    public void Live(bool isLive, string keyName) => Call($"live({(isLive ? "true" : "false")},{JsText.Literal(keyName)})");

    public void Open(string viewJson) => Call($"open({viewJson})");

    /// <summary>A refresh of the open panel, as PowerPage.Show sends it.</summary>
    public void Show(string viewJson) => Call($"setData({viewJson})");

    public void Status(string text) => Call($"status({JsText.Literal(text)})");

    private void Call(string expression) => Run("window.__slpower." + expression);

    /// <summary>Takes what the script posted since the last look.</summary>
    private void Hear()
    {
        var count = (int)_engine.Evaluate("posted.length").AsNumber();
        var messages = Enumerable.Range(0, count).Select(i => _engine.Evaluate($"posted[{i}]").AsString()).ToList();

        _engine.Execute("posted.length = 0");

        foreach (var message in messages) Take(PageCommand.Parse(message));
    }

    private void Take(PageCommand command)
    {
        switch (command)
        {
            case PageCommand.Ready:
                HasSaidReady = true;
                break;
            case PageCommand.Unknown unknown:
                Notes.Add(unknown.Text);
                break;
            default:
                Sent.Add(command);
                break;
        }
    }

    private static string Resource(string name)
    {
        using var stream = typeof(PageScript).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
