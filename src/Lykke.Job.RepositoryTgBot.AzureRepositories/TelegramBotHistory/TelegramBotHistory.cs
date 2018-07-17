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

        public string GithubUserName { get; set; }
        
        public List<TelegramBotHistoryEntity> Entities { get; set; }

        public override void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            if(properties.TryGetValue("Entities", out var entities))
            {
                var json = entities.StringValue;
                if (!string.IsNullOrEmpty(json))
                {
                    Entities = JsonConvert.DeserializeObject<List<TelegramBotHistoryEntity>>(json);
                }
            }

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

            if(properties.TryGetValue("GithubUserName", out var githubUserName))
            {
                GithubUserName = githubUserName.StringValue;
            }


        }

        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var dict = new Dictionary<string, EntityProperty>
            {
                { "Entities", new EntityProperty(JsonConvert.SerializeObject(Entities)) },
                { "ChatId", new EntityProperty(ChatId) },
                { "UserId", new EntityProperty(UserId) },
                { "TelegramUserName", new EntityProperty(TelegramUserName) },
                { "GithubUserName", new EntityProperty(GithubUserName) }
            };

            return dict;
        }
    }
}
