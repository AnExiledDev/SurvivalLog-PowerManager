using PowerManagerMod.Core;
using Xunit;

namespace PowerManagerMod.Tests;

/// <summary>Every word the mod shows has an English and a Chinese version, and the game's language picks between them.</summary>
public class TextsTests
{
    public static TheoryData<int> Entries()
    {
        var data = new TheoryData<int>();

        foreach (var index in Enumerable.Range(0, Texts.All.Count)) data.Add(index);

        return data;
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public void Every_text_has_plain_english_and_a_real_chinese_translation(int index)
    {
        var phrase = Texts.All[index];

        Assert.False(string.IsNullOrWhiteSpace(phrase.En));
        Assert.True(phrase.En.All(ch => ch is >= ' ' and <= '~'), phrase.En);

        // A bare format such as "{0}%" has no words to translate.
        if (!phrase.En.Any(char.IsLetter))
        {
            Assert.Equal(phrase.En, phrase.Zh);

            return;
        }

        Assert.Contains(phrase.Zh, ch => ch is >= '一' and <= '鿿');
        Assert.NotEqual(phrase.En, phrase.Zh);
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public void Both_languages_fill_in_the_same_blanks(int index)
    {
        var phrase = Texts.All[index];
        var isSingular = Texts.Counts.Any(counted => counted.One == phrase);

        // English spells out the 1 in "1 generator", so only its count blank may go unused.
        if (!isSingular) Assert.Equal(phrase.En.Contains("{0}"), phrase.Zh.Contains("{0}"));

        Assert.Equal(phrase.En.Contains("{1}"), phrase.Zh.Contains("{1}"));
    }

    [Theory]
    [InlineData(1, "Starting 1 generator.", "正在启动 1 台发电机。")]
    [InlineData(3, "Starting 3 generators.", "正在启动 3 台发电机。")]
    public void A_count_is_said_in_the_singular_or_plural_in_english_and_one_way_in_chinese(int count, string english, string chinese)
    {
        Assert.Equal(english, Texts.StartingAll.Say(isChinese: false, count));
        Assert.Equal(chinese, Texts.StartingAll.Say(isChinese: true, count));
    }

    [Fact]
    public void The_page_words_have_one_entry_per_key()
    {
        var keys = Texts.Page.Select(word => word.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Every_word_the_page_script_asks_for_is_sent_to_it()
    {
        var keys = Texts.Page.Select(word => word.Key).ToHashSet();
        var asked = System.Text.RegularExpressions.Regex.Matches(PageScript.Source, @"say\('([A-Za-z]+)'").Select(match => match.Groups[1].Value).Distinct();
        var mapped = System.Text.RegularExpressions.Regex.Matches(PageScript.Source, @":\s*'((?:tab|fc)[A-Za-z]+)'").Select(match => match.Groups[1].Value);

        Assert.Empty(asked.Concat(mapped).Where(key => !keys.Contains(key)));
    }
}
