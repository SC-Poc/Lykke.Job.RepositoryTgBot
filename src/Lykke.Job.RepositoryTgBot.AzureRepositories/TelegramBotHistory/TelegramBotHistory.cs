using System;
using System.Collections.Generic;
using System.Text;
using Lykke.Job.RepositoryTgBot.Core.Domain.TelegramBotHistory;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Lykke.Job.RepositoryTgBot.AzureRepositories.TelegramBotHistory
{
    public class TelegramBotHistory:TableEntity, ITelegramBotHistory
    {
        public static string GeneratePartitionKey() => "TBH";

        public static string GenerateRowKey(string telegramBotHistoryId) => telegramBotHistoryId;

        public long? ChatId { get; set; }

        public long? UserId { get; set; }

        public string TelegramUserName { get; set; }

        public string Question { get; set; }

        public string Answer { get; set; }

        public override void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {

            if(properties.TryGetValue("ChatId", out var chatId))
            {
                ChatId = chatId.Int64Value;
            }


            if(properties.TryGetValue("UserId", out var userId))
            {
                UserId = userId.Int64Value;
            }

            if(properties.TryGetValue("TelegramUserName", out var telegramUserName))
            {
                TelegramUserName = telegramUserName.StringValue;
            }

            if (properties.TryGetValue("Question", out var question))
            {
                Question = question.StringValue;
            }

            if (properties.TryGetValue("Answer", out var answer))
            {
                Answer = answer.StringValue;
            }

        }

        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var dict = new Dictionary<string, EntityProperty>
            {
                { "ChatId", new EntityProperty(ChatId) },
                { "UserId", new EntityProperty(UserId) },
                { "TelegramUserName", new EntityProperty(TelegramUserName) },
                { "Question", new EntityProperty(Question) },
                { "Answer", new EntityProperty(Answer) }
            };

            return dict;
        }
    }
}
