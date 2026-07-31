using HandbookBot.Core.Models;

namespace HandbookBot.Core.Entities;

public partial class FaqEntryEntity
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;

    public FaqEntry ToDomain() => new(Id, Question, Answer);
}
