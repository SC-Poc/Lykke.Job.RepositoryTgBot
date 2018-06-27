using System;
using System.Collections.Generic;
using System.Text;
using Lykke.Job.RepositoryTgBot.Core.Domain.TelegramBotHistory;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Job.RepositoryTgBot.AzureRepositories.TelegramBotHistory
{
    public class TelegramBotHistory:TableEntity, ITelegramBotHistory
    {
        public static string GeneratePartitionKey() => "TBH";

        public static string GenerateRowKey(string telegramBotHistoryId) => telegramBotHistoryId;

        public long ChatId { get; set; }

        public long UserId { get; set; }

        public int TeamId { get; set; }

        public string TelegramUserName { get; set; }

        public string GithubUserName { get; set; }

        public string Question { get; set; }

        public string Answer { get; set; }
    }
}
