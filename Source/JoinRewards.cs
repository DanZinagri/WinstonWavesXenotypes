using System.Collections.Generic;
using Verse;
using VSEWW;

namespace WinstonWavesXenotypes
{
    // The Winston Waves rewards we take the xenotypes of, read from every JoinRewardListDef in
    // the database (see Defs/JoinRewardLists.xml). The colonist joins are the ones left standing
    // after Patches/JoinRewards.xml drops the parallel xenohuman set; the sanguophage rewards
    // keep their own pool and their own tab.
    public static class JoinRewards
    {
        private static bool resolved;

        private static readonly List<RewardDef> colonistJoins = new List<RewardDef>();

        private static readonly List<RewardDef> sanguophageJoins = new List<RewardDef>();

        public static bool IsColonistJoinReward(RewardDef reward)
        {
            EnsureResolved();
            return colonistJoins.Contains(reward);
        }

        public static bool IsSanguophageReward(RewardDef reward)
        {
            EnsureResolved();
            return sanguophageJoins.Contains(reward);
        }

        private static void EnsureResolved()
        {
            if (resolved || DefDatabase<RewardDef>.DefCount == 0)
            {
                return;
            }
            resolved = true;

            List<JoinRewardListDef> lists = DefDatabase<JoinRewardListDef>.AllDefsListForReading;
            if (lists.Count == 0)
            {
                Log.Warning("[WinstonWavesXenotypes] No JoinRewardListDef loaded, so no reward is being given xenotypes. Is Defs/JoinRewardLists.xml missing?");
                return;
            }

            List<string> missing = new List<string>();
            foreach (JoinRewardListDef list in lists)
            {
                Collect(list.colonistJoinRewards, colonistJoins, missing);
                Collect(list.sanguophageRewards, sanguophageJoins, missing);
            }

            // Usually a mod the player does not have, but just as easily a typo in the XML, so
            // it is worth saying which names went nowhere.
            if (missing.Count > 0)
            {
                Log.Warning("[WinstonWavesXenotypes] These reward defNames were not found and are being skipped: " + string.Join(", ", missing));
            }
        }

        private static void Collect(List<string> defNames, List<RewardDef> into, List<string> missing)
        {
            if (defNames == null)
            {
                return;
            }

            foreach (string defName in defNames)
            {
                RewardDef reward = DefDatabase<RewardDef>.GetNamedSilentFail(defName);
                if (reward == null)
                {
                    missing.Add(defName);
                }
                else if (!into.Contains(reward))
                {
                    into.Add(reward);
                }
            }
        }
    }
}
