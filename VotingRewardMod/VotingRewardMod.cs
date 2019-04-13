using Eleon.Modding;
using EmpyrionNetAPIAccess;
using EmpyrionNetAPIDefinitions;
using EmpyrionNetAPITools;
using System;
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

        public VotingRewardMod()
        {
            EmpyrionConfiguration.ModName = "VotingReward";
        }

        public override void Initialize(ModGameAPI dediAPI)
        {
            DediAPI  = dediAPI;
            verbose  = true;
            LogLevel = LogLevel.Message;

            log($"**VotingRewardMod: loaded");

            LoadConfiuration();

            ChatCommands.Add(new ChatCommand(@"/votereward",  (I, A) => VoteReward(I, A), "Vote on [c][cc0000]https://empyrion-servers.com[-][/c] for the server to get an award"));
            ChatCommands.Add(new ChatCommand(@"/vote help",   (I, A) => DisplayHelp(I),   "Votereward help"));
        }

        private async Task DisplayHelp(ChatInfo chatInfo)
        {
            var player = await Request_Player_Info(chatInfo.playerId.ToId());
            var vote = GetPlayerVote(player);

            await DisplayHelp(chatInfo.playerId, $"You have {vote.Count} votes.\n\n Rewards:" +
                Configuration.Current.VotingRewards.Aggregate("\n", (S, R) => {
                    return S + $"every {R.EveryXVotesGet} vote{(R.EveryXVotesGet > 1 ? "s" : "")} get " + R.Rewards.Aggregate("", (s, r) => $"{r.Count} {r.Name}, {s}") + "\n";
                })
            );
        }

        private async Task VoteReward(ChatInfo chat, Dictionary<string, string> args)
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
                        .Where(R => vote.Count % R.EveryXVotesGet == 0)
                        .ToList()
                        .ForEach(Rs => GivePlayerRewards(player, Rs.Rewards));
                else GivePlayerRewards(player, Configuration.Current.VotingRewards
                        .Last(R => vote.Count % R.EveryXVotesGet == 0).Rewards);

                await MarkRewardClaimed(player);
            }
            else
            {
                log($"No unclaimed voting reward found for {player.playerName}.");
                MessagePlayer(chat.playerId, "No unclaimed/new voting found.", MessagePriorityType.Alarm);
            }
        }

        private void GivePlayerRewards(PlayerInfo player, List<RewardModConfiguration.VoteReward> rewards)
        {
            if (rewards == null) return;

            rewards.ForEach(async R => await Request_Player_AddItem(new IdItemStack(player.entityId, new ItemStack(R.Id, R.Count))));
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
            if (Configuration.Current.PlayerVotes == null) Configuration.Current.PlayerVotes = new List<RewardModConfiguration.PlayerVote>();

            var vote = Configuration.Current.PlayerVotes.FirstOrDefault(V => V.SteamId == player.steamId);
            if (vote == null) Configuration.Current.PlayerVotes.Add(vote = new RewardModConfiguration.PlayerVote() { SteamId = player.steamId, Count = 0 });
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
            Configuration.ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, @"Configuration.json");

            var DemoInit = !File.Exists(Configuration.ConfigFilename);

            Configuration.Load();

            if (DemoInit) DemoInitConfiguration();
        }

        private void DemoInitConfiguration()
        {
            Configuration.Current = new RewardModConfiguration() {
                VotingApiServerKey = "Get yours from https://empyrion-servers.com/",
                Cumulative = true,
                VotingRewards = new [] {
                    new RewardModConfiguration.VotingReward() {
                        EveryXVotesGet = 1,
                        Rewards = new[]
                        {
                            new RewardModConfiguration.VoteReward(){ Id = 2298, Name= "Gold Ingot",  Count = 100 },
                            new RewardModConfiguration.VoteReward(){ Id = 2373, Name= "Fusion Cell", Count = 100 },
                        }.ToList(),
                    },
                    new RewardModConfiguration.VotingReward() {
                        EveryXVotesGet = 100,
                        Rewards = new[]
                        {
                            new RewardModConfiguration.VoteReward(){ Id = 2088, Name= "Epic Drill", Count = 1 },
                        }.ToList()
                    }
                }.ToList(),
            };

            Configuration.Save();
        }
    }
}
