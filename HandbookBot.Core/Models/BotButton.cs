namespace HandbookBot.Core.Models;

public enum BotButtonType
{
    Callback,
    Url,
    RequestGeo,
    RequestText
}

public sealed record BotButton
{
    public string Text { get; init; }
    public BotButtonType Type { get; init; }
    public string? Payload { get; init; }
    public string? Url { get; init; }

    private BotButton(string text, BotButtonType type, string? payload = null, string? url = null)
    {
        Text = text;
        Type = type;
        Payload = payload;
        Url = url;
    }

    public static BotButton Callback(string text, string payload)
        => new(text, BotButtonType.Callback, payload: payload);

    public static BotButton Link(string text, string url)
        => new(text, BotButtonType.Url, url: url);

    public static BotButton RequestGeo(string text)
        => new(text, BotButtonType.RequestGeo);
}
