using System.Collections.Generic;
using Verse;

namespace WinstonWavesXenotypes
{
    // Names which Winston Waves rewards each settings tab drives, so the lists can be edited in
    // Defs/JoinRewardLists.xml without rebuilding the assembly. Every def of this type is merged,
    // so a patch or another mod can add its own rather than having to replace ours.
    //
    // These are plain defName strings rather than <RewardDef> references on purpose: the
    // sanguophage rewards only exist with Biotech, and a name that resolves to nothing is simply
    // skipped instead of throwing a cross-reference error.
    public class JoinRewardListDef : Def
    {
        // Rewards that hand out ordinary colonists - driven by the Colonist xenotypes tab.
        public List<string> colonistJoinRewards = new List<string>();

        // Rewards meant to hand out hemogen drinkers - driven by the Sanguophage xenotypes tab.
        public List<string> sanguophageRewards = new List<string>();

        // Ghoul and shambler joins. Humans underneath - mutating a pawn only disables a couple of
        // named genes, so its xenotype survives - but they are their own thing, so the Colonist
        // tab has a toggle for whether its list reaches them.
        public List<string> mutantJoinRewards = new List<string>();

        // Warcasket joins, same deal: humans in a tin can, toggled from the Colonist tab.
        public List<string> warcasketJoinRewards = new List<string>();
    }
}
