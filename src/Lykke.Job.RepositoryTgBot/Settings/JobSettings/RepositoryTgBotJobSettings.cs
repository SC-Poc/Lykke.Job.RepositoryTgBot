﻿namespace Lykke.Job.RepositoryTgBot.Settings.JobSettings
{
    public class RepositoryTgBotJobSettings
    {
        public DbSettings Db { get; set; }
        public string BotToken { get; set; }
        public string GitToken { get; set; }
        public string OrgainzationName { get; set; }
    }
}