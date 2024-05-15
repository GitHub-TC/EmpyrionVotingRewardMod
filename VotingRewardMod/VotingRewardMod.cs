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
using System.Text.RegularExpressions;
using System.Text;
using NameIdMappingTools;

namespace VotingRewardMod
{
    public partial class VotingRewardMod : EmpyrionModBase
    {

        public ModGameAPI DediAPI { get; private set; }

        public ConfigurationManager<RewardModConfiguration> Configuration { get; set; }
        public BlockNameIdMapping Mapping { get; }

        public VotingRewardMod()
        {
            EmpyrionConfiguration.ModName = "VotingReward";
            BlockNameIdMapping.Log = Log;

            Mapping = new BlockNameIdMapping(() => Configuration?.Current?.NameIdMappingFile);
        }

        public override void Initialize(ModGameAPI dediAPI)
        {
            DediAPI  = dediAPI;
            LogLevel = LogLevel.Message;

            Log($"**VotingRewardMod: loaded");

            LoadConfiuration();
            LogLevel = Configuration.Current.LogLevel;
            ChatCommandManager.CommandPrefix = Configuration.Current.CommandPrefix;

            SyncBlockIdMapping();

            ChatCommands.Add(new ChatCommand(@"votereward",  (I, A) => VoteReward(I, A, VoteMode.Voting),  "Vote to get an award"));
            if(Configuration.Current.VotingLottery.Count > 0)
                ChatCommands.Add(new ChatCommand(@"votelottery", (I, A) => VoteReward(I, A, VoteMode.Lottery), "Vote to play in the lottery"));
            Configuration.Current.StatsRewards?.ForEach(R => ChatCommands.Add(new ChatCommand($"voteforstat {R.Type.ToString().ToLower()}", (I, A) => VoteReward(I, A, R.Type), $"Vote to boost your {R.Type} max for another {R.AddCount} (max: {R.MaxCount})")));
            ChatCommands.Add(new ChatCommand(@"vote help",   (I, A) => DisplayHelp(I),   "Votereward help"));
        }

        private void SyncBlockIdMapping()
        {
            Configuration.Current.VotingLottery?                             .ForEach(UpdateToNewConfigurationFormat);
            Configuration.Current.VotingRewards?.ForEach(vote => vote.Rewards.ForEach(UpdateToNewConfigurationFormat));

            if (Mapping.NameId != null)
            {
                Configuration.Current.VotingLottery?.ForEach(SyncId);
                Configuration.Current.VotingRewards?.ForEach(vote => vote.Rewards.ForEach(SyncId));
            }

            if (Configuration.LoadException == null) Configuration.Save();
        }

        private void SyncId(RewardModConfiguration.VoteReward r)
        {
            if (Mapping.NameId == null || Mapping.NameId.Count == 0) return;

            if (string.IsNullOrEmpty(r.Name))
            {
                if (Mapping.IdName.TryGetValue(r.Id, out var name)) r.Name = name;
            }
            else if(Mapping.NameId.TryGetValue(r.Name, out var id))
            {
                r.Id = id;
            }
            else r.Id = 0;
        }

        private static void UpdateToNewConfigurationFormat(RewardModConfiguration.VoteReward r)
        {
            if (!string.IsNullOrEmpty(r.Description)) return;

            r.Description = r.Name; 
            r.Name        = null;
        }

        private async Task DisplayHelp(ChatInfo chatInfo)
        {
            var player = await Request_Player_Info(chatInfo.playerId.ToId());
            var vote = GetPlayerVote(player);

            var voteStats = new StringBuilder($"Since {vote.Statistic.StartAtUtc.ToLocalTime():dd.MM.yyyy} you have the following votes\n");
            AddCount(voteStats, vote.Statistic.VoteForReward,  "rewards");
            AddCount(voteStats, vote.Statistic.VoteForLottery, "lottery");
            AddCount(voteStats, vote.Statistic.VoteForHealth,  "max health");
            AddCount(voteStats, vote.Statistic.VoteForFood,    "max food");
            AddCount(voteStats, vote.Statistic.VoteForOxygen,  "max oxygen");
            AddCount(voteStats, vote.Statistic.VoteForStamina, "max stamina");

            await DisplayHelp(chatInfo.playerId, $"{voteStats}\nVote on [c][cc0000]{Configuration.Current.ServerVotingHomepage}[-][/c] for the server.\n\nRewards (/votereward):" +
                Configuration.Current.VotingRewards.Where(V => V.EveryXVotesGet > 0).Aggregate("\n", (S, R) => {
                    return S + $"every {R.EveryXVotesGet} vote{(R.EveryXVotesGet > 1 ? "s" : "")} get " + R.Rewards.Aggregate("", (s, r) => $"{r.Count} {r.Description}, {s}") + "\n";
                }) +
                Configuration.Current.VotingRewards.Where(V => V.ForVoteNumerXGet > 0).Aggregate("\n", (S, R) => {
                    return S + $"for vote number {R.ForVoteNumerXGet} get " + R.Rewards.Aggregate("", (s, r) => $"{r.Count} {r.Description}, {s}") + "\n";
                }) +
                (Configuration.Current.VotingLottery?.Count == 0 ? "" :
                "\nLottery (/votelottery):" +
                Configuration.Current.VotingLottery.GroupBy(R => R.Id).Aggregate("\n", (S, R) => 
                    R.Key == 0 
                    ? $"{S}{Configuration.Current.VotingLottery.Count(r => r.Id == R.Key)} sorry no win\n"
                    : $"{S}{Configuration.Current.VotingLottery.Count(r => r.Id == R.Key)} times the chance on {R.GroupBy(r => r.Count).Aggregate((string)null, (s, r) => $"{(s == null ? "" : s + ", ")}{r.Key}")} {R.First().Description}\n"
                )) + 
                (string.IsNullOrEmpty(Configuration.Current.RewardTestPlayerName) ? "" : $"\nRewardTestPlayer:{Configuration.Current.RewardTestPlayerName}"));
        }

        private void AddCount(StringBuilder voteStats, int voteCount, string name)
        {
            if (voteCount != 0) voteStats.AppendLine($"{voteCount} votes for {name}");
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
                    default:                await AddStats   (chat, player, GetPlayerVote(player), mode); break;
                }
            }
            else
            {
                Log($"No unclaimed voting reward found for {player.playerName}.");
                MessagePlayer(chat.playerId, "No unclaimed/new voting found.", MessagePriorityType.Alarm);
            }
        }

        private async Task AddStats(ChatInfo chat, PlayerInfo player, RewardModConfiguration.PlayerVote vote, VoteMode voteMode)
        {
            Log($"{player.playerName}/{player.steamId} claimed a voting reward for {voteMode}");

            var found = Configuration.Current.StatsRewards.FirstOrDefault(S => S.Type == voteMode);
            bool getsome = false;

            switch (voteMode)
            {
                case VoteMode.Health :
                    getsome = found?.MaxCount > player.healthMax;
                    if (getsome) await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = player.entityId, healthMax  = (int)player.healthMax    + found.AddCount });
                    break;
                case VoteMode.Food:
                    getsome = found?.MaxCount > player.foodMax;
                    if (getsome) await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = player.entityId, foodMax    = (int)player.foodMax      + found.AddCount });
                    break;
                case VoteMode.Stamina:
                    getsome = found?.MaxCount > player.staminaMax;
                    if (getsome) await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = player.entityId, staminaMax = (int)player.staminaMax   + found.AddCount });
                    break;
                case VoteMode.Oxygen:
                    getsome = found?.MaxCount > player.oxygenMax;
                    if (getsome) await Request_Player_SetPlayerInfo(new PlayerInfoSet() { entityId = player.entityId, oxygenMax  = (int)player.oxygenMax    + found.AddCount });
                    break;
            }


            if (getsome) {
                Log($"{player.playerName}/{player.steamId} boost for {voteMode}");

                switch (voteMode)
                {
                    case VoteMode.Health : vote.Statistic.VoteForHealth ++; break;
                    case VoteMode.Stamina: vote.Statistic.VoteForStamina++; break;
                    case VoteMode.Food   : vote.Statistic.VoteForFood   ++; break;
                    case VoteMode.Oxygen : vote.Statistic.VoteForOxygen ++; break;
                }
                Configuration.Save();

                await MarkRewardClaimed(player);
                await ShowDialog(chat.playerId, player, "Congratulation", $"Your reward has been boosted your {voteMode} stats about {found.AddCount}.");
            }
            else await ShowDialog(chat.playerId, player, "Sorry", $"Sorry your {voteMode} stats is at maxium {found.MaxCount} please choose another reward.");
        }

        private async Task AddVote(ChatInfo chat, PlayerInfo player, RewardModConfiguration.PlayerVote vote)
        {
            vote.Statistic.VoteForReward++;
            Configuration.Save();

            Log($"{player.playerName}/{vote.SteamId} claimed a voting reward for {vote.Statistic.VoteForReward}");

            bool getsome = false;
            if (Configuration.Current.Cumulative)
                Configuration.Current.VotingRewards
                    .Where(R => (R.EveryXVotesGet > 0 && vote.Statistic.VoteForReward % R.EveryXVotesGet == 0) || (R.ForVoteNumerXGet > 0 && vote.Statistic.VoteForReward == R.ForVoteNumerXGet))
                    .ToList()
                    .ForEach(Rs => getsome = GivePlayerRewards(player, Rs.Rewards) || getsome);
            else
            {
                getsome = GivePlayerRewards(player, Configuration.Current.VotingRewards
                    .LastOrDefault(R => (R.EveryXVotesGet > 0 && vote.Statistic.VoteForReward % R.EveryXVotesGet == 0)).Rewards);
                getsome = GivePlayerRewards(player, Configuration.Current.VotingRewards
                    .FirstOrDefault(R => R.ForVoteNumerXGet > 0 && vote.Statistic.VoteForReward == R.ForVoteNumerXGet).Rewards) || getsome;
            }

            await MarkRewardClaimed(player);

            if (getsome) await ShowDialog(chat.playerId, player, "Congratulation", "Your reward has been placed in your inventory");
        }

        private async Task PlayLottery(ChatInfo chat, PlayerInfo player, RewardModConfiguration.PlayerVote vote)
        {
            vote.Statistic.VoteForLottery++;
            Configuration.Save();

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
            if (player.playerName == Configuration.Current.RewardTestPlayerName) return true;

            var uri = string.Format(Configuration.Current.GetUnclaimedVoteUrl, Configuration.Current.VotingApiServerKey, player.steamId, player.playerName);
            var response = await CallRestMethod("GET", uri);
            return Regex.Match(response, Configuration.Current.GetUnclaimedVoteMatch).Success;
        }

        private async Task MarkRewardClaimed(PlayerInfo player)
        {
            if (player.playerName == Configuration.Current.RewardTestPlayerName || string.IsNullOrEmpty(Configuration.Current.ClaimedVoteUrl)) return;

            var uri = string.Format(Configuration.Current.ClaimedVoteUrl, Configuration.Current.VotingApiServerKey, player.steamId, player.playerName);

            await CallRestMethod(Configuration.Current.ClaimedVoteMethod, uri);
        }

        RewardModConfiguration.PlayerVote GetPlayerVote(PlayerInfo player)
        {
            if (Configuration.Current.PlayerVotes == null) Configuration.Current.PlayerVotes = new List<RewardModConfiguration.PlayerVote>();

            var vote = Configuration.Current.PlayerVotes.FirstOrDefault(V => V.SteamId == player.steamId);
            if (vote == null) Configuration.Current.PlayerVotes.Add(vote = new RewardModConfiguration.PlayerVote() { SteamId = player.steamId, Count = 0 });
            vote.PlayerName = player.playerName;
            return vote;
        }

        public async Task<string> CallRestMethod(string method, string url)
        {
            try
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
            catch (Exception error)
            {
                Log($"CallRestMethod:{url} => {error.Message}", LogLevel.Error);
                return string.Empty;
            }
        }

        private void LoadConfiuration()
        {
            ConfigurationManager<RewardModConfiguration>.Log = Log;
            Configuration = new ConfigurationManager<RewardModConfiguration>
            {
                ConfigFilename = Path.Combine(EmpyrionConfiguration.SaveGameModPath, @"Configuration.json")
            };

            Configuration.CreateDefaults = (config) => 
            {
                config.VotingApiServerKey = "Get yours from https://empyrion-servers.com/ or your server voting provider";
                config.Cumulative = true;
                config.VotingRewards = new[] {
                    new RewardModConfiguration.VotingReward() {
                        ForVoteNumerXGet = 1,
                        Rewards = new[]
                        {
                            new RewardModConfiguration.VoteReward() { Id = 4346, Description = "Gold Ingot", Count = 500 },
                            new RewardModConfiguration.VoteReward() { Id = 4421, Description = "Fusion Cell", Count = 500 },
                        }.ToList(),
                    },
                    new RewardModConfiguration.VotingReward() {
                        EveryXVotesGet = 1,
                        Rewards = new[]
                        {
                            new RewardModConfiguration.VoteReward() { Id = 4346, Description = "Gold Ingot", Count = 100 },
                            new RewardModConfiguration.VoteReward() { Id = 4421, Description = "Fusion Cell", Count = 100 },
                        }.ToList(),
                    },
                    new RewardModConfiguration.VotingReward() {
                        EveryXVotesGet = 100,
                        Rewards = new[]
                        {
                            new RewardModConfiguration.VoteReward() { Id = 4136, Description = "Epic Drill", Count = 1 },
                        }.ToList()
                    }
                }.ToList();
                config.StatsRewards = new[]
                {
                    new RewardModConfiguration.VoteStatsReward()
                    {
                        Type = VoteMode.Health,
                        AddCount = 50,
                        MaxCount = 1000
                    }
                }.ToList();
                config.VotingLottery = new[]
                {
                    new RewardModConfiguration.VoteReward(){ Id = 4429, Description= "Rotten Food",  Count = 100 },
                    new RewardModConfiguration.VoteReward(){ Id = 4429, Description= "Rotten Food",  Count = 100 },
                    new RewardModConfiguration.VoteReward(){ Id = 4429, Description= "Rotten Food",  Count = 100 },
                    new RewardModConfiguration.VoteReward(){ Id = 4346, Description= "Gold Ingot",   Count = 100 },
                    new RewardModConfiguration.VoteReward(){ Id = 4136, Description= "Epic Drill",   Count = 1 },
                    new RewardModConfiguration.VoteReward(){ Id = 1110, Description= "T3 AutoMiner", Count = 1 },
                }.ToList();
            };

            Configuration.Load();

            if (Configuration.LoadException == null) Configuration.Save();
        }

    }
}
