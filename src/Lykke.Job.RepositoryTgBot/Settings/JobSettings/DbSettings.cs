using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.RepositoryTgBot.Settings.JobSettings
{
    public class DbSettings
    {
        [AzureTableCheck]
        public string LogsConnString { get; set; }
    }
}
