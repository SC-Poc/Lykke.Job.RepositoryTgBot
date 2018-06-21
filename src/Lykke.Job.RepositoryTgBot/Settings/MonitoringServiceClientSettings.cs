using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.RepositoryTgBot.Settings
{
    public class MonitoringServiceClientSettings
    {
        [HttpCheck("api/isalive", false)]
        public string MonitoringServiceUrl { get; set; }
    }
}
