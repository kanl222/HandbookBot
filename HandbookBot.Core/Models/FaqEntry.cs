namespace HandbookBot.Core.Models;

/// <summary>Запись FAQ (вопрос–ответ).</summary>
public sealed record FaqEntry(int Id, string Question, string Answer);
