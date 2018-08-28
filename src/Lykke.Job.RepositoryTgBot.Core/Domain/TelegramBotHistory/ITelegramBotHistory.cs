using System.Collections.Generic;

namespace Lykke.Job.RepositoryTgBot.Core.Domain.TelegramBotHistory
{
    public interface ITelegramBotHistory:IEntity
    {
        long? ChatId { get; set; }

        long? UserId { get; set; }

        string TelegramUserName { get; set; }

        string GithubUserName { get; set; }

        List<TelegramBotHistoryEntity> Entities { get; set; }
    }
}
