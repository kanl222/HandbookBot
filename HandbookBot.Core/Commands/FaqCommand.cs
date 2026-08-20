using HandbookBot.Core.Interfaces;
using HandbookBot.Core.Models;

namespace HandbookBot.Core.Commands;

/// <summary>
/// Команда "faq" — список вопросов кнопками → выбор → ответ.
/// Callback-форматы:
///   "faq:list"   — показать список вопросов
///   "faq:{id}"   — показать ответ на вопрос с указанным Id
/// </summary>
public sealed class FaqCommand : IBotCommand
{
    private readonly IFaqRepository _faqRepo;

    public FaqCommand(IFaqRepository faqRepo) => _faqRepo = faqRepo;

    public string Name => "faq";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        var parts = message.CallbackData?.Split(':', 2) ?? [];
        var action = parts.Length >= 2 ? parts[1] : "list";

        var entries = await _faqRepo.GetAllAsync(ct);

        // Показ ответа на конкретный вопрос
        if (action != "list" && int.TryParse(action, out var entryId))
        {
            var entry = entries.FirstOrDefault(e => e.Id == entryId);
            if (entry is not null)
            {
                await context.ReplyAsync(
                    $"*{entry.Question}*\n\n{entry.Answer}",
                    BotKeyboard.SingleColumn(
                        BotButton.Callback("Назад к вопросам", "faq:list"),
                        BotButton.Callback("В главное меню", "start:menu")));
                return;
            }
        }

        // Список вопросов
        var buttons = entries
            .Select(e => BotButton.Callback(e.Question, $"faq:{e.Id}"))
            .Append(BotButton.Callback("В главное меню", "start:menu"))
            .ToArray();

        await context.ReplyAsync(
            "*Часто задаваемые вопросы (FAQ)*\n\nВыберите интересующий вас вопрос:",
            BotKeyboard.SingleColumn(buttons));
    }
}
