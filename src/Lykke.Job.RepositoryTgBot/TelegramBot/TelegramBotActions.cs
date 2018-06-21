using Octokit;
using System;
using System.Collections.Generic;
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
        public async Task<string> AddUserInTeam(string nickName, string teamName)
        {
            var team = await GetTeamByName(teamName);

            if (team == null)
                return $"There are no team with name: {teamName}.";

            var userInTeam = await TeamMemberCheckAsync(nickName, team);

            if (userInTeam)
                return $"User {nickName} alrady in team: {team.Name}.";

            await client.Organization.Team.AddOrEditMembership(team.Id, nickName, new UpdateTeamMembership(TeamRole.Member));
            return $"User {nickName} added in team {team.Name} as a member."; ;
        }

        private async Task<Team> GetTeamByName(string teamName)
        {
            var teams = await client.Organization.Team.GetAll(_organisation);
            foreach (var team in teams)
            {
                if (teamName == team.Name)
                {
                    return team;
                }
            }
            return null;
        }

        private async Task<bool> TeamMemberCheckAsync(string nickName, Team team)
        {

            var members = await client.Organization.Team.GetAllMembers(team.Id);

            foreach (var member in members)
            {
                if (nickName == member.Name)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
