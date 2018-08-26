using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureStorage;
using Lykke.Job.RepositoryTgBot.Core.Domain.TelegramBotHistory;

namespace Lykke.Job.RepositoryTgBot.AzureRepositories.TelegramBotHistory
{
    public class TelegramBotHistoryRepository : ITelegramBotHistoryRepository
    {
        private readonly INoSQLTableStorage<TelegramBotHistory> _tableStorage;

        public TelegramBotHistoryRepository(INoSQLTableStorage<TelegramBotHistory> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task<ITelegramBotHistory> GetAsync(string telegramBotHistoryId)
        {
            var pk = TelegramBotHistory.GeneratePartitionKey();
            var rk = TelegramBotHistory.GenerateRowKey(telegramBotHistoryId);

            return await _tableStorage.GetDataAsync(pk, rk);
        }

        public async Task<IEnumerable<ITelegramBotHistory>> GetAllAsync(Func<ITelegramBotHistory, bool> filter = null)
        {
            var pk = TelegramBotHistory.GeneratePartitionKey();
            var list = await _tableStorage.GetDataAsync(pk, filter: filter);
            return list as IEnumerable<ITelegramBotHistory>;
        }

        public async Task<ITelegramBotHistory> GetLatestAsync(Func<ITelegramBotHistory, bool> filter)
        {
            var pk = TelegramBotHistory.GeneratePartitionKey();
            var list = await _tableStorage.GetDataAsync(pk, filter);
            var orderedList = list.OrderByDescending(x => x.Timestamp);
            return orderedList.Where(filter).FirstOrDefault();
        }

        public async Task<bool> SaveAsync(ITelegramBotHistory entity)
        {
            try
            {
                if (!(entity is TelegramBotHistory tbh))
                {
                    tbh = (TelegramBotHistory) await GetAsync(entity.RowKey) ?? new TelegramBotHistory();

                    tbh.ETag = entity.ETag;
                    tbh.ChatId = entity.ChatId;
                    tbh.UserId = entity.UserId;
                    tbh.TelegramUserName = entity.TelegramUserName;
                    tbh.GithubUserName = entity.GithubUserName;
                    tbh.Question = entity.Question;
                    tbh.Answer = entity.Answer;
                }

                tbh.PartitionKey = TelegramBotHistory.GeneratePartitionKey();
                tbh.RowKey = entity.RowKey;
                await _tableStorage.InsertOrMergeAsync(tbh);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task RemoveAsync(string telegramBotHistoryId)
        {
            var pk = TelegramBotHistory.GeneratePartitionKey();
            await _tableStorage.DeleteAsync(pk, telegramBotHistoryId);
        }
    }
}
