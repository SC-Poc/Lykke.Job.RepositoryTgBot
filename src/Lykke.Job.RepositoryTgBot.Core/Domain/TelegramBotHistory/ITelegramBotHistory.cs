namespace Lykke.Job.RepositoryTgBot.Core.Domain.TelegramBotHistory
{
    public interface ITelegramBotHistory:IEntity
    {
        long ChatId { get; set; }

        long UserId { get; set; }

        int TeamId { get; set; }

        string TelegramUserName { get; set; }

        string GithubUserName { get; set; }

        string Question { get; set; }

        string Answer { get; set; }
    }
}
