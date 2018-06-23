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

        public async Task<List<Team>> GetTeamsAsync()
        {
            var teams = await client.Organization.Team.GetAll(_organisation);

            var teamsList = new List<Team>();

            foreach (var team in teams)
            {
                teamsList.Add(team);
            }

            return teamsList;
        }

        public async Task<Team> UserHasTeamCheckAsync(string nickName)
        {
            var teams = await client.Organization.Team.GetAll(_organisation);

            foreach (var team in teams)
            {
                var teamCheck =  await TeamMemberCheckAsync(nickName, team);
                if (teamCheck)
                    return team;
            }

            return null;
        }

        public async Task<bool> OrgMemberCheckAsync(string nickName)
        {
            var inOrganisation = await client.Organization.Member.CheckMember(_organisation, nickName);
            return inOrganisation;
        }

        public async Task CreateRepo(int teamId, string repoName, string description, bool addSecurityTeam = false, bool addCoreTeam = false)
        {           
            //TODO: CreateRepo
            var team = await client.Organization.Team.Get(teamId);

            var codeOwnersFile = $"* @{_organisation}/{team.Name} ";

            var newRepo = new NewRepository(repoName) { AutoInit = true, TeamId = team.Id, Description = description };
            newRepo.TeamId = team.Id;

            var repositoryToEdit = await client.Repository.Create(_organisation, newRepo);

            var branchTeams = new BranchProtectionTeamCollection();

            branchTeams.Add(team.Name);

            await client.Organization.Team.AddRepository(team.Id, _organisation, repositoryToEdit.Name, new RepositoryPermissionRequest(Permission.Admin));
            
            if (addSecurityTeam)
            {
                var secTeam = await GetTeamByName("Security");
                await client.Organization.Team.AddRepository(secTeam.Id, _organisation, repositoryToEdit.Name, new RepositoryPermissionRequest(Permission.Push));
                branchTeams.Add("Security");
                codeOwnersFile += $"@{_organisation}/Security ";
            }

            if (addCoreTeam)
            {
                var secTeam = await GetTeamByName("Core");
                await client.Organization.Team.AddRepository(secTeam.Id, _organisation, repositoryToEdit.Name,new RepositoryPermissionRequest(Permission.Push));
                branchTeams.Add("Core");
                codeOwnersFile += $"@{_organisation}/Core ";
            }

            await client.Repository.Content.CreateFile(repositoryToEdit.Id, "CODEOWNERS", new CreateFileRequest("Added CODEOWNERS file", codeOwnersFile));

            //creating "dev" and "test" branches from master
            var masterSha = await client.Repository.Commit.GetSha1(repositoryToEdit.Id, "refs/heads/master");

            await client.Git.Reference.Create(repositoryToEdit.Id, new NewReference("refs/heads/dev", masterSha));
            await client.Git.Reference.Create(repositoryToEdit.Id, new NewReference("refs/heads/test", masterSha));
            
            var masterProtSett = new BranchProtectionSettingsUpdate(new BranchProtectionPushRestrictionsUpdate(branchTeams));
            var testProtSett = new BranchProtectionSettingsUpdate( null, new BranchProtectionRequiredReviewsUpdate(new BranchProtectionRequiredReviewsDismissalRestrictionsUpdate(false), true, true),new BranchProtectionPushRestrictionsUpdate(branchTeams), true);

            await client.Repository.Branch.UpdateBranchProtection(repositoryToEdit.Id, "master", masterProtSett);
            await client.Repository.Branch.UpdateBranchProtection(repositoryToEdit.Id, "test", testProtSett);

        }

        /// <summary>
        /// Returns string with result as answer
        /// </summary>
        /// <param name="nickName"></param>
        /// <param name="teamId"></param>
        /// <returns> </returns>
        public async Task<string> AddUserInTeam(string nickName, int teamId)
        {
            var team = await client.Organization.Team.Get(teamId);

            if (team == null)
                return "There are no such team.";

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
