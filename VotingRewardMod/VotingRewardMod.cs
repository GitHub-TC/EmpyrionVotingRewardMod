using Eleon.Modding;
using EmpyrionNetAPIAccess;
using EmpyrionNetAPIDefinitions;
using EmpyrionNetAPITools;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace VotingRewardMod
{
    public class VotingRewardMod : EmpyrionModBase
    {
        public ModGameAPI DediAPI { get; private set; }

        public ConfigurationManager<RewardModConfiguration> Configuration { get; set; }

        public override void Initialize(ModGameAPI dediAPI)
        {
            DediAPI  = dediAPI;
            verbose  = true;
            LogLevel = EmpyrionNetAPIDefinitions.LogLevel.Message;

            LoadConfiuration();

            ChatCommands.Add(new ChatCommand(@"/votereward", (I, A) => VoteRewardAsync(I, A), "Vote for the server to get an award"));
        }

        private async void VoteRewardAsync(ChatInfo chat, Dictionary<string, string> args)
        {
            var player = await Request_Player_Info(new Id(chat.playerId));
            var vote = GetPlayerVote(player);

            log($"{player.playerName} is trying to claim a voting reward.");
            if (await DoesPlayerHaveAUnclaimedVote(player))
            {
                vote.Count++;
                Configuration.Save();

                log($"{player.playerName}/{vote.SteamId} claimed a voting reward for {vote.Count}");

                if (Configuration.Current.Cumulative)
                    Configuration.Current.VotingRewards
                        .Where(R => R.MinVotesNeeded <= vote.Count)
                        .ToList()
                        .ForEach(async R => await Request_Player_AddItem(new IdItemStack(player.entityId, R.Rewards)));
                else await Request_Player_AddItem(new IdItemStack(player.entityId, 
                    Configuration.Current.VotingRewards
                        .Last(R => R.MinVotesNeeded <= vote.Count).Rewards));

                await MarkRewardClaimed(player);
            }
            else
            {
                log($"No unclaimed voting reward found for {player.playerName}.");
                MessagePlayer(player.clientId, "No unclaimed/new voting found.", MessagePriorityType.Alarm);
            }
        }

        private async Task<bool> DoesPlayerHaveAUnclaimedVote(PlayerInfo player)
        {
            var uri = $"https://empyrion-servers.com/api/?object=votes&element=claim&key={Configuration.Current.VotingApiServerKey}&steamid={player.steamId}";
            var response = await CallRestMethod("GET", uri);
            return response == "1";
        }

        private async Task MarkRewardClaimed(PlayerInfo player)
        {
            var uri = $"https://empyrion-servers.com/api/?action=post&object=votes&element=claim&key={Configuration.Current.VotingApiServerKey}&steamid={player.steamId}";

            await CallRestMethod("POST", uri);
        }

        RewardModConfiguration.PlayerVote GetPlayerVote(PlayerInfo player)
        {
            var vote = Configuration.Current.PlayerVotes.FirstOrDefault(V => V.SteamId == player.steamId);
            if (vote == null) Configuration.Current.PlayerVotes.Add(vote = new RewardModConfiguration.PlayerVote() { SteamId = player.steamId, Count = 1 });
            return vote;
        }

        public async static Task<string> CallRestMethod(string method, string url)
        {
            var webrequest = WebRequest.Create(url);
            webrequest.Method = method;
            using (var response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(webrequest.BeginGetResponse, webrequest.EndGetResponse, null))
            {
                var responseStream = new StreamReader(response.GetResponseStream(), System.Text.Encoding.GetEncoding("utf-8"));
                string result = string.Empty;
                result = responseStream.ReadToEnd();

                return result;
            }
        }

        private void LoadConfiuration()
        {
            Configuration = new ConfigurationManager<RewardModConfiguration>() { UseJSON = true };
            Configuration.ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, @"VotingReward\Configuration.json");
            Configuration.Load();
        }
    }
}
