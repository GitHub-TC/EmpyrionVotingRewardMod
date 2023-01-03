using Eleon.Modding;
using EmpyrionNetAPIDefinitions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace VotingRewardMod
{
    public class RewardModConfiguration
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel LogLevel { get; set; } = LogLevel.Message;
        public string CommandPrefix { get; set; } = "/\\";
        public string VotingApiServerKey { get; set; }
        public bool Cumulative { get; set; }
        public string RewardTestPlayerName { get; set; } = "";
        public string ServerVotingHomepage { get; set; } = "https://empyrion-servers.com";
        public string GetUnclaimedVoteUrl { get; set; } = "https://empyrion-servers.com/api/?object=votes&element=claim&key={0}&steamid={1}";
        public string GetUnclaimedVoteMatch { get; set; } = "1";
        public string ClaimedVoteUrl { get; set; } = "https://empyrion-servers.com/api/?action=post&object=votes&element=claim&key={0}&steamid={1}";
        public string ClaimedVoteMethod { get; set; } = "POST";
        public string NameIdMappingFile { get; set; } = "filepath to the NameIdMapping.json e.g. from EmpyrionScripting for cross savegame support";

        public class VoteReward
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public int Count { get; set; }
        }

        public class VoteStatsReward
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public VoteMode Type { get; set; }
            public int AddCount { get; set; }
            public int MaxCount { get; set; }
        }
        public class VotingReward
        {
            public int EveryXVotesGet { get; set; }
            public List<VoteReward> Rewards { get; set; }
        }
        public class Statistic
        {
            public DateTime StartAtUtc { get; set; }
            public int VoteForReward { get; set; }
            public int VoteForLottery { get; set; }
            public int VoteForHealth { get; set; }
            public int VoteForFood { get; set; }
            public int VoteForStamina { get; set; }
            public int VoteForOxygen { get; set; }
        }
        public class PlayerVote
        {
            public string SteamId { get; set; }
            public string PlayerName { get; set; }
            public int Count { set { Statistic.VoteForReward = value; } }
            public Statistic Statistic { get; set; } = new Statistic { StartAtUtc = DateTime.UtcNow };
        }
        public List<VoteStatsReward> StatsRewards { get; set; }
        public List<VotingReward> VotingRewards { get; set; }
        public List<VoteReward> VotingLottery { get; set; }
        public List<PlayerVote> PlayerVotes { get; set; }
    }
}
