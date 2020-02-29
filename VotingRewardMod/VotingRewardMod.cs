using Eleon.Modding;
using EmpyrionNetAPIAccess;
using EmpyrionNetAPITools;
using EmpyrionNetAPIDefinitions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System;

namespace VotingRewardMod
{
    public partial class VotingRewardMod : EmpyrionModBase
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
            LogLevel = LogLevel.Message;

            Log($"**VotingRewardMod: loaded");

            LoadConfiuration();
            LogLevel = Configuration.Current.LogLevel;
            ChatCommandManager.CommandPrefix = Configuration.Current.CommandPrefix;

            ChatCommands.Add(new ChatCommand(@"votereward",  (I, A) => VoteReward(I, A, VoteMode.Voting),  "Vote to get an award"));
            if(Configuration.Current.VotingLottery.Count > 0)
                ChatCommands.Add(new ChatCommand(@"votelottery", (I, A) => VoteReward(I, A, VoteMode.Lottery), "Vote to play in the lottery"));
            Configuration.Current.StatsRewards?.ForEach(R => ChatCommands.Add(new ChatCommand($"voteforstat {R.Type.ToString().ToLower()}", (I, A) => VoteReward(I, A, R.Type), $"Vote to boost your {R.Type} for another {R.AddCount} (max: {R.MaxCount})")));
            ChatCommands.Add(new ChatCommand(@"vote help",   (I, A) => DisplayHelp(I),   "Votereward help"));
        }

        private async Task DisplayHelp(ChatInfo chatInfo)
        {
            var player = await Request_Player_Info(chatInfo.playerId.ToId());
            var vote = GetPlayerVote(player);

            await DisplayHelp(chatInfo.playerId, $"You have {vote.Count} votes.\nVote on [c][cc0000]https://empyrion-servers.com[-][/c] for the server.\n\nRewards:" +
                Configuration.Current.VotingRewards.Aggregate("\n", (S, R) => {
                    return S + $"every {R.EveryXVotesGet} vote{(R.EveryXVotesGet > 1 ? "s" : "")} get " + R.Rewards.Aggregate("", (s, r) => $"{r.Count} {r.Name}, {s}") + "\n";
                }) + 
                (Configuration.Current.VotingLottery?.Count == 0 ? "" :
                "\n\n Lottery:" +
                Configuration.Current.VotingLottery.GroupBy(R => R.Id).Aggregate("\n", (S, R) => 
                    R.Key == 0 
                    ? $"{S}{Configuration.Current.VotingLottery.Count(r => r.Id == R.Key)} sorry no win\n"
                    : $"{S}{Configuration.Current.VotingLottery.Count(r => r.Id == R.Key)} times the change on {R.GroupBy(r => r.Count).Aggregate((string)null, (s, r) => $"{(s == null ? "" : s + ", ")}{r.Key}")} {R.First().Name}\n"
                )));
        }

        private async Task VoteReward(ChatInfo chat, Dictionary<string, string> args, VoteMode mode)
        {
            var player = await Request_Player_Info(new Id(chat.playerId));

            Log($"{player.playerName} is trying to claim a voting reward.");
            if (await DoesPlayerHaveAUnclaimedVote(player))
            {
                switch (mode)
                {
                    case VoteMode.Voting  : await AddVote    (chat, player, GetPlayerVote(player)); break;
                    case VoteMode.Lottery : await PlayLottery(chat, player, GetPlayerVote(player)); break;
                    default:                await AddStats   (chat, player, mode); break;
                }
            }
            else
            {
                Log($"No unclaimed voting reward found for {player.playerName}.");
                MessagePlayer(chat.playerId, "No unclaimed/new voting found.", MessagePriorityType.Alarm);
            }
        }

        private async Task AddStats(ChatInfo chat, PlayerInfo player, VoteMode vote)
        {
            Log($"{player.playerName}/{player.steamId} claimed a voting reward for {vote}");

            var found = Configuration.Current.StatsRewards.FirstOrDefault(S => S.Type == vote);
            bool getsome = false;

            switch (vote)
            {
                case VoteMode.Health :
                    getsome = found?.MaxCount > player.healthMax;
                    if (getsome) await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = player.entityId, healthMax = (int)player.healthMax + found.AddCount });
                    break;
                case VoteMode.Food:
                    getsome = found?.MaxCount > player.foodMax;
                    if (getsome) await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = player.entityId, foodMax = (int)player.foodMax + found.AddCount });
                    break;
                case VoteMode.Stamina:
                    getsome = found?.MaxCount > player.staminaMax;
                    if (getsome) await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = player.entityId, staminaMax = (int)player.staminaMax + found.AddCount });
                    break;
            }

            if (getsome) {
                Log($"{player.playerName}/{player.steamId} boost for {vote}");

                await MarkRewardClaimed(player);
                await ShowDialog(chat.playerId, player, "Congratulation", $"Your reward has been boosted your {vote} stats about {found.AddCount}.");
            }
            else await ShowDialog(chat.playerId, player, "Sorry", $"Sorry your {vote} stats is at maxium {found.MaxCount} please choose another reward.");
        }

        private async Task AddVote(ChatInfo chat, PlayerInfo player, RewardModConfiguration.PlayerVote vote)
        {
            vote.Count++;
            Configuration.Save();

            Log($"{player.playerName}/{vote.SteamId} claimed a voting reward for {vote.Count}");

            bool getsome = false;
            if (Configuration.Current.Cumulative)
                Configuration.Current.VotingRewards
                    .Where(R => vote.Count % R.EveryXVotesGet == 0)
                    .ToList()
                    .ForEach(Rs => getsome = GivePlayerRewards(player, Rs.Rewards) || getsome);
            else getsome = GivePlayerRewards(player, Configuration.Current.VotingRewards
                    .Last(R => vote.Count % R.EveryXVotesGet == 0).Rewards);

            await MarkRewardClaimed(player);

            if (getsome) await ShowDialog(chat.playerId, player, "Congratulation", "Your reward has been placed in your inventory");
        }

        private async Task PlayLottery(ChatInfo chat, PlayerInfo player, RewardModConfiguration.PlayerVote vote)
        {
            Log($"{player.playerName}/{vote.SteamId} claimed a voting for lottery");

            var reward = Configuration.Current.VotingLottery[new Random().Next(0, Configuration.Current.VotingLottery.Count - 1)];

            var getsome = GivePlayerRewards(player, new[] { reward });

            await MarkRewardClaimed(player);

            if (getsome) await ShowDialog(chat.playerId, player, "Congratulation", "Your win has been placed in your inventory");
            else         await ShowDialog(chat.playerId, player, "Sorry",          "No luck this time - next time new game new luck");
        }


        private bool GivePlayerRewards(PlayerInfo player, IEnumerable<RewardModConfiguration.VoteReward> rewards)
        {
            if (rewards == null) return false;

            var give = rewards
                .Where(R => R.Id != 0)
                .ToList();

            if (give.Count == 0) return false;

            give.ForEach(async R => await Request_Player_AddItem(new IdItemStack(player.entityId, new ItemStack(R.Id, R.Count))));

            return true;
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
            vote.PlayerName = player.playerName;
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
            ConfigurationManager<RewardModConfiguration>.Log = Log;
            Configuration = new ConfigurationManager<RewardModConfiguration>
            {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, @"Configuration.json")
            };

            var DemoInit = !File.Exists(Configuration.ConfigFilename);

            Configuration.Load();

            if (DemoInit) DemoInitConfiguration();
        }

        private void DemoInitConfiguration()
        {
            Configuration.Current = new RewardModConfiguration() {
                VotingApiServerKey = "Get yours from https://empyrion-servers.com/",
                Cumulative = true,
                VotingRewards = new[] {
                    new RewardModConfiguration.VotingReward() {
                        EveryXVotesGet = 1,
                        Rewards = new[]
                        {
                            new RewardModConfiguration.VoteReward() { Id = 2298, Name = "Gold Ingot", Count = 100 },
                            new RewardModConfiguration.VoteReward() { Id = 2373, Name = "Fusion Cell", Count = 100 },
                        }.ToList(),
                    },
                    new RewardModConfiguration.VotingReward() {
                        EveryXVotesGet = 100,
                        Rewards = new[]
                        {
                            new RewardModConfiguration.VoteReward() { Id = 2088, Name = "Epic Drill", Count = 1 },
                        }.ToList()
                    }
                }.ToList(),
                StatsRewards = new[]
                {
                    new RewardModConfiguration.VoteStatsReward()
                    {
                        Type = VoteMode.Health,
                        AddCount = 50,
                        MaxCount = 1000
                    }
                }.ToList(),
                VotingLottery = new[]
                {
                    new RewardModConfiguration.VoteReward(){ Id = 2381, Name= "Rotten Food",  Count = 100 },
                    new RewardModConfiguration.VoteReward(){ Id = 2381, Name= "Rotten Food",  Count = 100 },
                    new RewardModConfiguration.VoteReward(){ Id = 2381, Name= "Rotten Food",  Count = 100 },
                    new RewardModConfiguration.VoteReward(){ Id = 2298, Name= "Gold Ingot",   Count = 100 },
                    new RewardModConfiguration.VoteReward(){ Id = 2088, Name= "Epic Drill",   Count = 1 },
                    new RewardModConfiguration.VoteReward(){ Id = 1110, Name= "T3 AutoMiner", Count = 1 },
                }.ToList()
            };

            Configuration.Save();
        }
    }
}
