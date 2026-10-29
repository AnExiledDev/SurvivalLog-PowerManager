namespace PowerManagerMod.Core;

/// <summary>
/// Which language's words the page holds. They are handed over ahead of a call only when the page has none yet or holds
/// the other language's, so a switch of the game's language still shows at the next redraw, while every other call goes
/// without them. A page that reloaded has lost them, so they are forgotten whenever the script is put back.
/// </summary>
public sealed class WordsOnce
{
    private bool? _held;

    /// <summary>The statement to run ahead of a call: the words, or nothing when the page already holds them.</summary>
    public string Prefix(bool isChinese) => _held == isChinese ? "" : PageWords.Script(isChinese);

    /// <summary>A call carrying <see cref="Prefix"/> ran in the page.</summary>
    public void Sent(bool isChinese) => _held = isChinese;

    public void Forget() => _held = null;
}
