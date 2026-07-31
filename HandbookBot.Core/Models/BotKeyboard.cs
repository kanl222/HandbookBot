namespace HandbookBot.Core.Models;

public record BotKeyboard(IReadOnlyList<IReadOnlyList<BotButton>> Rows)
{
    public static BotKeyboard SingleColumn(params BotButton[] buttons)
        => new(buttons.Select(b => (IReadOnlyList<BotButton>)[b]).ToList());

    public static BotKeyboard Grid(IEnumerable<IEnumerable<BotButton>> rows)
        => new(rows.Select(r => (IReadOnlyList<BotButton>)r.ToList()).ToList());
}