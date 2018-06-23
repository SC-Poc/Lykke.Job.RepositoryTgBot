using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lykke.Job.RepositoryTgBot.TelegramBot
{
    public class TelegramBotActions
    {
        private static GitHubClient client = new GitHubClient(new ProductHeaderValue("RepositoryTgBot"));

        private static string _organisation;

        public TelegramBotActions(string organisation, string token)
        {
            _organisation = organisation;
            var tokenAuth = new Credentials(token);
            client.Credentials = tokenAuth;
        }

        public async Task<List<string>> GetTeamsAsync()
        {
            var teams = await client.Organization.Team.GetAll(_organisation);

            var teamsList = new List<string>();

            foreach (var team in teams)
            {
                teamsList.Add(team.Name);
                Console.WriteLine(team.Name);
            }

            return teamsList;
        }

        public async Task<bool> OrgMemberCheckAsync(string nickName)
        {
            var inOrganisation = await client.Organization.Member.CheckMember(_organisation, nickName);

            Console.WriteLine("Org check -" + inOrganisation);

            return inOrganisation;
        }

        public async Task CreateRepo(string teamName, string repoName)
        {
            //TODO: CreateRepo
        }

        public async Task AddDescrToRepo(string teamName, string repoName)
        {
            var team = await GetTeamByName(teamName);
            await client.Organization.Team.GetAllRepositories(team.Id);
        }

        /// <summary>
        /// Returns string with result as answer
        /// </summary>
        /// <param name="nickName"></param>
        /// <param name="teamName"></param>
        /// <returns> </returns>
        public async Task<TelegramBotActionResult> AddUserInTeam(string nickName, string teamName)
        {
            var team = await GetTeamByName(teamName);

            if (team == null)
                //return success false with message
                return new TelegramBotActionResult { Success = false, Message = $"There are no team with name: {teamName}." };

            var userInTeam = await TeamMemberCheckAsync(nickName, team);

            if (userInTeam)
                //return success false with message
                return new TelegramBotActionResult { Success = false, Message = $"User {nickName} alrady in team: {team.Name}." };

            await client.Organization.Team.AddOrEditMembership(team.Id, nickName, new UpdateTeamMembership(TeamRole.Member));

            //return success true with message
            return new TelegramBotActionResult { Success = false, Message = $"User {nickName} added in team {team.Name} as a member." };
        }

        public async Task<TelegramBotActionResult> AddRepositoryInTeam(string teamName, string repoName, Permission permission)
        {
            var team = await GetTeamByName(teamName);

            if (team == null)
                //return success false with message
                return new TelegramBotActionResult { Success = false, Message = $"There are no team with name: {teamName}." };

            var repoInTeam = await TeamRepoCheckAsync(repoName, team.Id);

            if(repoInTeam)
                //return success false with message
                return new TelegramBotActionResult { Success = false, Message = $"Repository {repoName} alrady in team: {team.Name}." };
            
            var result = await client.Organization.Team.AddRepository(team.Id, _organisation, repoName, new RepositoryPermissionRequest(permission));

            //return success true with message
            var message = result ? $"Repository {repoName} added in team {team.Name}" : "Error occured, try again later, please";
            return new TelegramBotActionResult{Success = result, Message = message};
        }

        private async Task<Team> GetTeamByName(string teamName)
        {
            var teams = await client.Organization.Team.GetAll(_organisation);
            return teams.FirstOrDefault(team => teamName == team.Name);
        }

        private async Task<bool> TeamMemberCheckAsync(string nickName, Team team)
        {
            var members = await client.Organization.Team.GetAllMembers(team.Id);

            return members.Any(x => x.Name == nickName);
        }

        private async Task<bool> TeamRepoCheckAsync(string repoName, int teamId)
        {
            var repositories = await client.Organization.Team.GetAllRepositories(teamId);

            return repositories.Any(x => x.Name == repoName);
        }
    }

    public class TelegramBotActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
