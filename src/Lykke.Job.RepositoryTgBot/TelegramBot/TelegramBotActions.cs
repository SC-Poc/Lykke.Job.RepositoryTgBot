using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;

namespace Lykke.Job.RepositoryTgBot.TelegramBot
{
    public class RepoToCreate
    {
        public long ChatId { get; set; }
        public int UserId { get; set; }
        public int TeamId { get; set; }
        public string RepoName { get; set; }
        public string Description { get; set; }
        public bool AddSecurityTeam { get; set; }
        public bool AddCoreTeam { get; set; } 
    } 


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

        public async Task<List<Team>> UserHasTeamCheckAsync(string nickName)
        {
            var teams = await client.Organization.Team.GetAll(_organisation);

            var listTeams = new List<Team>();

            foreach (var team in teams)
            {
                var teamCheck =  await TeamMemberCheckAsync(nickName, team);
                if (teamCheck)
                    listTeams.Add(team);
            }

            return listTeams;
        }

        public async Task<bool> OrgMemberCheckAsync(string nickName)
        {
            var inOrganisation = await client.Organization.Member.CheckMember(_organisation, nickName);
            return inOrganisation;
        }

        public async Task<TelegramBotActionResult> CreateRepo(RepoToCreate repoToCreate)
        {
            try
            {
                var team = await client.Organization.Team.Get(repoToCreate.TeamId);

                var codeOwnersFile = (team != null) ? $"* @{_organisation}/{team.Name} " : "* ";

                var message = $"Repository \"{repoToCreate.RepoName}\" susessfully created.";
                //Creating new repo
                var newRepo = new NewRepository(repoToCreate.RepoName) { AutoInit = true, TeamId = (team != null) ? team.Id : default, Description = repoToCreate.Description };

                var repositoryToEdit = await client.Repository.Create(_organisation, newRepo);

                var branchTeams = new BranchProtectionTeamCollection();
                if (team != null)
                {
                    branchTeams.Add(team.Name);
                    //Assigning new repo to the team
                    await client.Organization.Team.AddRepository(team.Id, _organisation, repositoryToEdit.Name, new RepositoryPermissionRequest(Permission.Admin));
                    message += $"\n Team - \"{team.Name}\"";
                }


                if (repoToCreate.AddSecurityTeam)
                {
                    var secTeam = await GetTeamByName("Security");
                    await client.Organization.Team.AddRepository(secTeam.Id, _organisation, repositoryToEdit.Name, new RepositoryPermissionRequest(Permission.Push));
                    branchTeams.Add("Security");
                    codeOwnersFile += $"@{_organisation}/Security ";
                    message += $"\n Team - \"Security\"";
                }

                if (repoToCreate.AddCoreTeam)
                {
                    var secTeam = await GetTeamByName("Core");
                    await client.Organization.Team.AddRepository(secTeam.Id, _organisation, repositoryToEdit.Name, new RepositoryPermissionRequest(Permission.Push));
                    branchTeams.Add("Core");
                    codeOwnersFile += $"@{_organisation}/Core ";
                    message += $"\n Team - \"Core\"";
                }

                //creating Code Owners file
                await client.Repository.Content.CreateFile(repositoryToEdit.Id, "CODEOWNERS", new CreateFileRequest("Added CODEOWNERS file", codeOwnersFile));

                //creating "dev" and "test" branches from master
                var masterSha = await client.Repository.Commit.GetSha1(repositoryToEdit.Id, "refs/heads/master");

                await client.Git.Reference.Create(repositoryToEdit.Id, new NewReference("refs/heads/dev", masterSha));
                await client.Git.Reference.Create(repositoryToEdit.Id, new NewReference("refs/heads/test", masterSha));

                //Permitions to "test" and "master" branches
                var masterProtSett = new BranchProtectionSettingsUpdate(new BranchProtectionPushRestrictionsUpdate(branchTeams));
                var testProtSett = new BranchProtectionSettingsUpdate(null, new BranchProtectionRequiredReviewsUpdate(new BranchProtectionRequiredReviewsDismissalRestrictionsUpdate(false), true, true), new BranchProtectionPushRestrictionsUpdate(branchTeams), true);

                await client.Repository.Branch.UpdateBranchProtection(repositoryToEdit.Id, "master", masterProtSett);
                await client.Repository.Branch.UpdateBranchProtection(repositoryToEdit.Id, "test", testProtSett);


                return new TelegramBotActionResult { Success = true, Message = message };
            }
            catch(Exception ex)
            {
                return new TelegramBotActionResult { Success = false, Message = ex.Message };
            }
            
        }

        /// <summary>
        /// Returns string with result as answer
        /// </summary>
        /// <param name="nickName"></param>
        /// <param name="teamId"></param>
        /// <returns> </returns>
        public async Task<TelegramBotActionResult> AddUserInTeam(string nickName, int teamId)
        {
            var team = await client.Organization.Team.Get(teamId);

            if (team == null)
                //return success false with message
                return new TelegramBotActionResult { Success = false, Message = $"There are such no teams." };

            var userInTeam = await TeamMemberCheckAsync(nickName, team);

            if (userInTeam)
                //return success false with message
                return new TelegramBotActionResult { Success = false, Message = $"User {nickName} alrady in team: {team.Name}." };

            await client.Organization.Team.AddOrEditMembership(team.Id, nickName, new UpdateTeamMembership(TeamRole.Member));

            //return success true with message
            return new TelegramBotActionResult { Success = true, Message = $"User {nickName} added in team {team.Name} as a member." };
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
