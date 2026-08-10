using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using VSEWW;

namespace WinstonWavesXenotypes
{
    // Winston Waves generates every humanlike reward pawn with a forced xenotype, and that
    // forced xenotype falls back to Baseliner whenever the RewardDef does not list any.
    public static class Patch_RewardXenotypes
    {
        private class Restore
        {
            public PawnReward pawnReward;
            public RPawnReward randomPawnReward;
            public List<XenotypeDef> original;
        }

        public static void Apply(Harmony harmony)
        {
            Type rewardCreator = AccessTools.TypeByName("VSEWW.RewardCreator");
            MethodInfo sendReward = rewardCreator == null ? null : AccessTools.Method(rewardCreator, "SendReward");

            if (sendReward == null)
            {
                Log.Warning("[WinstonWavesXenotypes] Could not find VSEWW.RewardCreator.SendReward - the xenohuman join rewards will keep their built-in xenotype list.");
                return;
            }

            harmony.Patch(sendReward,
                prefix: new HarmonyMethod(typeof(Patch_RewardXenotypes), nameof(Prefix)),
                finalizer: new HarmonyMethod(typeof(Patch_RewardXenotypes), nameof(Finalizer)));
        }

        public static void Prefix(RewardDef reward, out object __state)
        {
            __state = null;

            WinstonWavesXenotypesSettings settings = WinstonWavesXenotypesMod.Settings;
            if (reward == null || settings == null)
            {
                return;
            }

            List<XenotypeDef> xenotypes;
            if (JoinRewards.IsColonistJoinReward(reward))
            {
                xenotypes = settings.colonistEnabled ? settings.ResolvedColonistXenotypes : null;
            }
            else if (JoinRewards.IsSanguophageReward(reward))
            {
                xenotypes = settings.sanguophageEnabled ? settings.ResolvedSanguophageXenotypes : null;
            }
            else
            {
                return;
            }

            if (xenotypes.NullOrEmpty())
            {
                return;
            }

            List<Restore> restores = new List<Restore>();

            if (reward.pawns != null)
            {
                foreach (PawnReward pawnReward in reward.pawns)
                {
                    if (pawnReward?.pawnkind?.race != ThingDefOf.Human)
                    {
                        continue;
                    }

                    restores.Add(new Restore { pawnReward = pawnReward, original = pawnReward.randomXenotypeFrom });
                    pawnReward.randomXenotypeFrom = xenotypes;
                }
            }

            if (reward.randomPawns != null)
            {
                foreach (RPawnReward randomPawnReward in reward.randomPawns)
                {
                    if (randomPawnReward == null || randomPawnReward.intelligence != Intelligence.Humanlike)
                    {
                        continue;
                    }

                    restores.Add(new Restore { randomPawnReward = randomPawnReward, original = randomPawnReward.randomXenotypeFrom });
                    randomPawnReward.randomXenotypeFrom = xenotypes;
                }
            }

            __state = restores;
        }

        public static void Finalizer(object __state)
        {
            if (!(__state is List<Restore> restores))
            {
                return;
            }

            for (int i = restores.Count - 1; i >= 0; i--)
            {
                Restore restore = restores[i];
                if (restore.pawnReward != null)
                {
                    restore.pawnReward.randomXenotypeFrom = restore.original;
                }
                else if (restore.randomPawnReward != null)
                {
                    restore.randomPawnReward.randomXenotypeFrom = restore.original;
                }
            }
        }
    }
}
