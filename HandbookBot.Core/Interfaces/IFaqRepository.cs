using HandbookBot.Core.Models;

namespace HandbookBot.Core.Interfaces;

public interface IFaqRepository
{
    Task<IReadOnlyList<FaqEntry>> GetAllAsync(CancellationToken ct = default);
}
