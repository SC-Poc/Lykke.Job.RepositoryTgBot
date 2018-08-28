using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lykke.Job.RepositoryTgBot.Core.Domain.TelegramBotHistory
{
    public interface ITelegramBotHistoryRepository
    {
        Task<ITelegramBotHistory> GetAsync(string telegramBotHistoryId);

        Task<IEnumerable<ITelegramBotHistory>> GetAllAsync(Func<ITelegramBotHistory, bool> filter = null);

        Task<ITelegramBotHistory> GetLatestAsync(Func<ITelegramBotHistory, bool> filter = null);

        Task<bool> SaveAsync(ITelegramBotHistory entity);

        Task<bool> SaveRangeAsync(IEnumerable<ITelegramBotHistory> entities);

        Task RemoveAsync(string telegramBotHistoryId);
    }
}
