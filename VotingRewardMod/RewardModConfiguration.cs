using Eleon.Modding;
using EmpyrionNetAPIDefinitions;
using System.Collections.Generic;

namespace VotingRewardMod
{
    public class RewardModConfiguration
    {
        public LogLevel LogLevel { get; set; } = LogLevel.Message;
        public string CommandPrefix { get; set; } = "/\\";
        public string VotingApiServerKey { get; set; }
        public bool Cumulative { get; set; }
        public class VoteReward
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Count { get; set; }
        }
        public class VotingReward
        {
            public int EveryXVotesGet { get; set; }
            public List<VoteReward> Rewards { get; set; }
        }
        public class PlayerVote
        {
            public string SteamId { get; set; }
            public string PlayerName { get; set; }
            public int Count { get; set; }
        }
        public List<VotingReward> VotingRewards { get; set; }
        public List<VoteReward> VotingLottery { get; set; }
        public List<PlayerVote> PlayerVotes { get; set; }
    }
}
