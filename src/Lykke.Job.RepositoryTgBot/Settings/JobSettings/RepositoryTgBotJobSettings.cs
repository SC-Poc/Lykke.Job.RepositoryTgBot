namespace Lykke.Job.RepositoryTgBot.Settings.JobSettings
{
    public class RepositoryTgBotJobSettings
    {
        public DbSettings Db { get; set; }
        public static string BotName { get; set; }
        public string BotToken { get; set; }
        public string GitToken { get; set; }
        public string OrganizationName { get; set; }
        public string CommonDevelopersTeam { get; set; }
        public static string ArchitectureTeam { get; set; }
        public static int TimeoutPeriodSeconds { get; set; }
        public static long AllowedGroupId { get; set; }
        public static int TotalTimeLimitInMinutes { get; set; }
        public long CommandChatId { get; set; }
    }
}
