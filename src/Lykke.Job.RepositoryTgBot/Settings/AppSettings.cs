using Lykke.Job.RepositoryTgBot.Settings.JobSettings;
using Lykke.Job.RepositoryTgBot.Settings.SlackNotifications;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.RepositoryTgBot.Settings
{
    public class AppSettings
    {
        public RepositoryTgBotJobSettings RepositoryTgBotJob { get; set; }

        public SlackNotificationsSettings SlackNotifications { get; set; }
    }
}
