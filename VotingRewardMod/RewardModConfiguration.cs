﻿using Eleon.Modding;
using System.Collections.Generic;

namespace VotingRewardMod
{
    public class RewardModConfiguration
    {
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
            public int Count { get; set; }
        }
        public List<VotingReward> VotingRewards { get; set; }
        public List<PlayerVote> PlayerVotes { get; set; }
    }
}
