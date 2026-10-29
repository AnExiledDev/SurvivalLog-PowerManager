using System;
using System.IO;
using PowerManagerMod.Core;
using ExiledToolkit;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using Vuplex.WebView;

namespace PowerManagerMod.Adapters;

/// <summary>
/// The mod's half that lives in the game's web UI: page.js, run in the root document the game's own pages sit in. It
/// draws the panel and puts the Power Manager button in the HUD's tool column. The web view can reload at any time and lose the script, so it is checked for every couple of
/// seconds and put back when the page says it is missing.
/// </summary>
internal sealed class WebPage
{
    private const float CheckSeconds = 2f;
    private const float CheckOffset = 0.6f;
    private const string ScriptResource = "PowerManager.page.js";
    private const string NoScriptReply = "window.vuplex&&window.vuplex.postMessage('" + PageCommand.Prefix + "noscript')";

    private readonly Action<string> _log;
    private readonly WordsOnce _words = new();
    private string? _script;
    private float _nextCheckAt = CheckOffset;
    private bool _hasReportedRunFailure;

    public WebPage(Action<string> log)
    {
        _log = log;
    }

    public void KeepInstalled(float now)
    {
        if (now < _nextCheckAt) return;

        // On its own beat, off the frames of the power pass and the button check (see PanelSession), so the three
        // never land in one frame; a long stall starts the beat again.
        _nextCheckAt += CheckSeconds;

        if (_nextCheckAt <= now) _nextCheckAt = now + CheckSeconds;

        Run("window.__slpower||" + NoScriptReply);
    }

    /// <summary>A page that needs the script put back has reloaded and lost the words with it.</summary>
    public void Install()
    {
        _words.Forget();

        if (Run(Script())) _log("page script sent to the web UI");
    }

    /// <summary>The page says it just installed itself, so it holds no words yet.</summary>
    public void ForgetWords() => _words.Forget();

    public bool Open(string viewJson) => Call($"open({viewJson})");

    public bool Show(string viewJson) => Call($"setData({viewJson})");

    public void Close() => Call("close()");

    /// <summary>
    /// A result line: in the panel's footer while it is open, as an Exiled's Toolkit toast otherwise. The page hears it
    /// either way, as it frees the button that was pressed.
    /// </summary>
    public void Status(string text, bool isPanelOpen, ToastKind kind)
    {
        Call($"status({JsText.Literal(text)})");

        if (isPanelOpen || string.IsNullOrEmpty(text)) return;

        Toasts.Show(new ToastRequest(Texts.Source.Pick(GameLanguage.IsChinese()), text) { Kind = kind });
    }

    /// <summary>Whether a game is loaded (the HUD button shows only then) and the panel key's name, "" for none.</summary>
    public bool Live(bool isLive, string keyName) => Call($"live({(isLive ? "true" : "false")},{JsText.Literal(keyName)})");

    /// <summary>
    /// A call hands the page its words first when it lacks them or holds the other language's, so a switch of the game's
    /// language shows at the next redraw. The words are set even while the script is missing, as they run first.
    /// </summary>
    private bool Call(string expression)
    {
        var isChinese = GameLanguage.IsChinese();

        if (!Run($"{_words.Prefix(isChinese)}window.__slpower?window.__slpower.{expression}:{NoScriptReply}")) return false;

        _words.Sent(isChinese);

        return true;
    }

    private bool Run(string javaScript)
    {
        var webView = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(javaScript, null);

            return true;
        }
        catch (Exception ex)
        {
            if (!_hasReportedRunFailure) _log("could not run script in the web UI (reported once): " + ex.Message);

            _hasReportedRunFailure = true;

            return false;
        }
    }

    private string Script()
    {
        if (_script != null) return _script;

        using var stream = typeof(WebPage).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return _script = reader.ReadToEnd();
    }
}
