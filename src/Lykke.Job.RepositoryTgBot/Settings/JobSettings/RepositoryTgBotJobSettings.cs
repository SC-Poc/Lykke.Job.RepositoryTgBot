namespace Lykke.Job.RepositoryTgBot.Settings.JobSettings
{
    public class RepositoryTgBotJobSettings
    {
        public DbSettings Db { get; set; }
        public string BotToken { get; set; }
        public static string GitToken { get; set; }
        public static string OrgainzationName { get; set; }
    }
}
