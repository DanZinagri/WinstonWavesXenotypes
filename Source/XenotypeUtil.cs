using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WinstonWavesXenotypes
{
    public static class XenotypeUtil
    {
        private static List<XenotypeDef> all;
        private static List<XenotypeDef> hemogen;
        private static List<XenotypeDef> nonHemogen;
        private static int builtForDefCount = -1;

        public static List<XenotypeDef> All
        {
            get
            {
                EnsureBuilt();
                return all;
            }
        }

        public static List<XenotypeDef> Hemogen
        {
            get
            {
                EnsureBuilt();
                return hemogen;
            }
        }

        public static List<XenotypeDef> NonHemogen
        {
            get
            {
                EnsureBuilt();
                return nonHemogen;
            }
        }
        // Winston Waves generates every reward pawn with mustBeCapableOfViolence, and a forced
        // xenotype bypasses the filter vanilla applies when it picks one itself - it drops any
        // xenotype with canGenerateAsCombatant=false from the draw. Force one in anyway and
        // generation rejects the pawn 120 times over and hands back null, which then takes the
        // whole reward down. So we apply vanilla's own rule here: highmates and their modded
        // equivalents are never eligible for a Winston Waves reward.
        public static bool CanBeRewardPawn(XenotypeDef xenotype)
        {
            if (xenotype == null || !xenotype.canGenerateAsCombatant)
            {
                return false;
            }

            if (xenotype.genes != null)
            {
                // Backstop for xenotypes carrying a violence-disabling gene whose author never
                // set canGenerateAsCombatant.
                foreach (GeneDef gene in xenotype.genes)
                {
                    if (gene != null && (gene.disabledWorkTags & WorkTags.Violent) != WorkTags.None)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // True for any xenotype carrying a gene that runs on hemogen - vanilla's Hemogenic and
        // anything a mod builds on it, rather than a hardcoded list of xenotype names.
        public static bool IsHemogen(XenotypeDef xenotype)
        {
            if (xenotype?.genes == null)
            {
                return false;
            }

            foreach (GeneDef gene in xenotype.genes)
            {
                if (gene == null)
                {
                    continue;
                }
                if (gene.geneClass != null && typeof(Gene_Hemogen).IsAssignableFrom(gene.geneClass))
                {
                    return true;
                }
                if (gene.resourceGizmoType != null && typeof(GeneGizmo_ResourceHemogen).IsAssignableFrom(gene.resourceGizmoType))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureBuilt()
        {
            // Comparing against the def count also covers being asked before defs have loaded.
            if (all != null && builtForDefCount == DefDatabase<XenotypeDef>.DefCount)
            {
                return;
            }
            builtForDefCount = DefDatabase<XenotypeDef>.DefCount;

            all = DefDatabase<XenotypeDef>.AllDefsListForReading
                .Where(CanBeRewardPawn)
                .OrderBy(x => x.label ?? x.defName)
                .ToList();
            hemogen = all.Where(IsHemogen).ToList();
            nonHemogen = all.Where(x => !IsHemogen(x)).ToList();
        }
    }
}
