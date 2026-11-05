using System;
using ExiledToolkit;
using ExiledToolkit.Game;
using ExiledToolkit.Web;
using PowerManagerMod.Core;

namespace PowerManagerMod.Adapters;

/// <summary>
/// The mod's half that lives in the game's web UI: page.js, run in the root document the game's own pages sit in. It
/// draws the panel and puts the Power Manager button in the HUD's tool column. The web view can reload at any time and
/// lose the script, so Exiled's Toolkit checks for it every couple of seconds and the session puts it back when the
/// page says it is missing.
/// </summary>
internal sealed class PowerPage
{
    private const string ScriptResource = "PowerManager.page.js";
    private const string Global = "window.__slpower";

    // Off the frames of the power pass and the button check (see PanelSession), so the three never land in one frame.
    private const double CheckOffset = 0.6;

    private readonly WebPage _page;

    public PowerPage(Action<string> log)
    {
        // The words go ahead of a call only when the page has none yet or holds the other language's, so a switch of
        // the game's language still shows at the next redraw while every other call goes without them.
        _page = new WebPage(typeof(PowerPage).Assembly, ScriptResource, Global, PageCommand.Prefix + "noscript", log)
        {
            Words = PageWords.Script,
            CheckOffset = CheckOffset,
        };
    }

    public void KeepInstalled(float now) => _page.KeepInstalled(now);

    public void Install() => _page.Install();

    /// <summary>The page says it just installed itself, so it holds no words yet.</summary>
    public void ForgetWords() => _page.ForgetWords();

    public bool Open(string viewJson) => _page.Call($"open({viewJson})");

    public bool Show(string viewJson) => _page.Call($"setData({viewJson})");

    public void Close() => _page.Call("close()");

    /// <summary>
    /// A result line: in the panel's footer while it is open, as an Exiled's Toolkit toast otherwise. The page hears it
    /// either way, as it frees the button that was pressed.
    /// </summary>
    public void Status(string text, bool isPanelOpen, ToastKind kind)
    {
        _page.Call($"status({JsText.Literal(text)})");

        if (isPanelOpen || string.IsNullOrEmpty(text)) return;

        Toasts.Show(new ToastRequest(Texts.Source.Pick(GameLanguage.IsChinese()), text) { Kind = kind });
    }

    /// <summary>Whether a game is loaded (the HUD button shows only then) and the panel key's name, "" for none.</summary>
    public bool Live(bool isLive, string keyName) => _page.Call($"live({(isLive ? "true" : "false")},{JsText.Literal(keyName)})");
}
