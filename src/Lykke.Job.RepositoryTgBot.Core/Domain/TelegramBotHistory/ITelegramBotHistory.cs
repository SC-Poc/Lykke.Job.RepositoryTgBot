using System.Collections.Generic;

namespace Lykke.Job.RepositoryTgBot.Core.Domain.TelegramBotHistory
{
    public interface ITelegramBotHistory:IEntity
    {
        long? ChatId { get; set; }

        long? DeveloperId { get; set; }

        List<TelegramBotHistoryEntity> Entities { get; set; }
    }
}
