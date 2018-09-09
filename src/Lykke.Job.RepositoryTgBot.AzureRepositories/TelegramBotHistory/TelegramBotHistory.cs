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

        public long? DeveloperId { get; set; }
        
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

            if(properties.TryGetValue("DeveloperId", out var developerId))
            {
                DeveloperId = developerId.Int64Value;
            }
        }

        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var dict = new Dictionary<string, EntityProperty>
            {
                { "Entities", new EntityProperty(JsonConvert.SerializeObject(Entities)) },
                { "ChatId", new EntityProperty(ChatId) },
                { "DeveloperId", new EntityProperty(DeveloperId) }
            };

            return dict;
        }
    }
}
