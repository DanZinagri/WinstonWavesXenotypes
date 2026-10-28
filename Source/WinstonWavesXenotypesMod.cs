using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WinstonWavesXenotypes
{
    public class WinstonWavesXenotypesMod : Mod
    {
        public static WinstonWavesXenotypesSettings Settings;

        private static int fallbackTab;

        public WinstonWavesXenotypesMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<WinstonWavesXenotypesSettings>();

            Harmony harmony = new Harmony("DanZinagri.WinstonWavesXenotypes");
            Patch_RewardXenotypes.Apply(harmony);
            Patch_WinstonSettingsTab.Apply(harmony);
            // The assembly is passed explicitly: on .NET 10 (1.7) the parameterless PatchAll() reads the calling
            // assembly off the stack, which breaks when the JIT inlines the call (Harmony issue #395).
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        // While the settings tabs are living inside the Winston Waves menu there is no reason to
        // also carry a duplicate entry of our own. If they could not be injected (Winston Waves
        // changed its settings window, say) we fall back to one.
        public override string SettingsCategory()
        {
            return Patch_WinstonSettingsTab.Injected ? "" : "WWX.ModName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect tabRect = new Rect(inRect);
            tabRect.y = inRect.y + 40f;

            Rect mainRect = new Rect(inRect);
            mainRect.y = inRect.y + 40f;
            mainRect.height = inRect.height - 40f;

            Widgets.DrawMenuSection(mainRect);

            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("WWX.TabColonists".Translate(), () => fallbackTab = 0, fallbackTab == 0),
                new TabRecord("WWX.TabSanguophages".Translate(), () => fallbackTab = 1, fallbackTab == 1)
            };
            TabDrawer.DrawTabs(tabRect, tabs, 200f);

            Rect contentRect = mainRect.ContractedBy(15f);
            if (fallbackTab == 0)
            {
                Settings.DoColonistTab(contentRect);
            }
            else
            {
                Settings.DoSanguophageTab(contentRect);
            }
        }

        public static void WriteSettingsIfDirty()
        {
            if (Settings == null || !Settings.Dirty)
            {
                return;
            }

            Settings.Dirty = false;
            LoadedModManager.GetMod<WinstonWavesXenotypesMod>()?.WriteSettings();
        }
    }

    public class WinstonWavesXenotypesSettings : ModSettings
    {
        public bool colonistEnabled = true;

        // Off by default: sanguophages and their kin stay the sanguophage reward's business,
        // which is how Winston Waves had it.
        public bool includeHemogenInColonists = false;

        // On by default: the ghoul, shambler and warcasket rewards hand out humans too, so the
        // colonist list may as well reach them. Untick to leave them as Winston Waves had them.
        public bool includeMutantJoins = true;

        public bool includeWarcasketJoins = true;

        public List<string> colonistXenotypes = new List<string>();

        public bool sanguophageEnabled = true;

        public List<string> sanguophageXenotypes = new List<string>();

        private Vector2 colonistScroll;
        private Vector2 sanguophageScroll;
        private string colonistSearch = "";
        private string sanguophageSearch = "";

        private List<XenotypeDef> resolvedColonist;
        private List<XenotypeDef> resolvedSanguophage;

        public bool Dirty;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref colonistEnabled, "colonistEnabled", true);
            Scribe_Values.Look(ref includeHemogenInColonists, "includeHemogenInColonists", false);
            Scribe_Values.Look(ref includeMutantJoins, "includeMutantJoins", true);
            Scribe_Values.Look(ref includeWarcasketJoins, "includeWarcasketJoins", true);
            Scribe_Collections.Look(ref colonistXenotypes, "colonistXenotypes", LookMode.Value);
            Scribe_Values.Look(ref sanguophageEnabled, "sanguophageEnabled", true);
            Scribe_Collections.Look(ref sanguophageXenotypes, "sanguophageXenotypes", LookMode.Value);

            colonistXenotypes ??= new List<string>();
            sanguophageXenotypes ??= new List<string>();
            resolvedColonist = null;
            resolvedSanguophage = null;
        }

        // What the "Random colonist joins" rewards roll from. Empty means leave them alone.
        public List<XenotypeDef> ResolvedColonistXenotypes
        {
            get
            {
                if (resolvedColonist == null)
                {
                    resolvedColonist = Resolve(colonistXenotypes)
                        .Where(x => includeHemogenInColonists || !XenotypeUtil.IsHemogen(x))
                        .ToList();
                }

                return resolvedColonist;
            }
        }

        // What the sanguophage rewards roll from. Selecting nothing means every hemogen
        // xenotype, which is the point of the tab - narrowing it is the optional part.
        public List<XenotypeDef> ResolvedSanguophageXenotypes
        {
            get
            {
                if (resolvedSanguophage == null)
                {
                    resolvedSanguophage = Resolve(sanguophageXenotypes)
                        .Where(XenotypeUtil.IsHemogen)
                        .ToList();

                    if (resolvedSanguophage.Count == 0)
                    {
                        resolvedSanguophage = XenotypeUtil.Hemogen.ToList();
                    }
                }

                return resolvedSanguophage;
            }
        }

        private static IEnumerable<XenotypeDef> Resolve(List<string> defNames)
        {
            // CanBeRewardPawn is re-checked here and not just when building the pickers, so a
            // selection saved before a xenotype became ineligible cannot reach pawn generation.
            return defNames
                .Select(defName => DefDatabase<XenotypeDef>.GetNamedSilentFail(defName))
                .Where(XenotypeUtil.CanBeRewardPawn);
        }

        private void InlineCheckbox(Rect rect, string key, ref bool value)
        {
            bool before = value;
            Widgets.DrawHighlightIfMouseover(rect);
            TooltipHandler.TipRegion(rect, (key + "Tip").Translate().ToString());
            Widgets.CheckboxLabeled(rect, key.Translate(), ref value);
            if (value != before)
            {
                Changed();
            }
        }

        private void Changed()
        {
            resolvedColonist = null;
            resolvedSanguophage = null;
            Dirty = true;
        }

        public void DoColonistTab(Rect inRect)
        {
            List<XenotypeDef> candidates = includeHemogenInColonists ? XenotypeUtil.All : XenotypeUtil.NonHemogen;

            DoPicker(inRect, candidates, colonistXenotypes, ref colonistScroll, ref colonistSearch, "WWX.NoneSelectedColonists",
                listing =>
                {
                    bool enabledBefore = colonistEnabled;
                    listing.CheckboxLabeled("WWX.ColonistEnable".Translate(), ref colonistEnabled, "WWX.ColonistEnableTip".Translate());
                    if (colonistEnabled != enabledBefore)
                    {
                        Changed();
                    }

                    bool hemogenBefore = includeHemogenInColonists;
                    listing.CheckboxLabeled("WWX.IncludeHemogen".Translate(), ref includeHemogenInColonists, "WWX.IncludeHemogenTip".Translate());
                    if (includeHemogenInColonists != hemogenBefore)
                    {
                        Changed();
                    }

                    // Side by side, to spend as little of the header on them as possible.
                    Rect optionRow = listing.GetRect(24f);
                    float optionWidth = (optionRow.width - 8f) / 2f;
                    InlineCheckbox(new Rect(optionRow.x, optionRow.y, optionWidth, optionRow.height),
                        "WWX.IncludeMutants", ref includeMutantJoins);
                    InlineCheckbox(new Rect(optionRow.xMax - optionWidth, optionRow.y, optionWidth, optionRow.height),
                        "WWX.IncludeWarcaskets", ref includeWarcasketJoins);

                    listing.Gap(4f);
                    listing.Label("WWX.NonCombatantNote".Translate());
                });
        }

        public void DoSanguophageTab(Rect inRect)
        {
            DoPicker(inRect, XenotypeUtil.Hemogen, sanguophageXenotypes, ref sanguophageScroll, ref sanguophageSearch, "WWX.NoneSelectedSanguophages",
                listing =>
                {
                    bool enabledBefore = sanguophageEnabled;
                    listing.CheckboxLabeled("WWX.SanguophageEnable".Translate(), ref sanguophageEnabled, "WWX.SanguophageEnableTip".Translate());
                    if (sanguophageEnabled != enabledBefore)
                    {
                        Changed();
                    }

                    listing.Gap(6f);
                    listing.Label("WWX.SanguophageExplain".Translate());
                });
        }

        private void DoPicker(Rect inRect, List<XenotypeDef> candidates, List<string> selected,
            ref Vector2 scrollPosition, ref string searchText, string emptyHintKey, Action<Listing_Standard> headerRows)
        {
            Listing_Standard header = new Listing_Standard();
            header.Begin(new Rect(inRect.x, inRect.y, inRect.width, 300f));

            headerRows(header);
            header.Gap(6f);

            searchText = header.TextEntry(searchText);
            header.Gap(4f);

            Rect buttonRow = header.GetRect(30f);
            float halfWidth = (buttonRow.width - 8f) / 2f;
            if (Widgets.ButtonText(new Rect(buttonRow.x, buttonRow.y, halfWidth, buttonRow.height), "WWX.SelectAll".Translate()))
            {
                selected.Clear();
                selected.AddRange(candidates.Select(x => x.defName));
                Changed();
            }
            if (Widgets.ButtonText(new Rect(buttonRow.xMax - halfWidth, buttonRow.y, halfWidth, buttonRow.height), "WWX.ClearAll".Translate()))
            {
                selected.Clear();
                Changed();
            }

            header.Gap(4f);
            int selectedCount = selected.Count(defName => candidates.Any(x => x.defName == defName));
            header.Label(selectedCount == 0
                ? emptyHintKey.Translate()
                : "WWX.SelectedCount".Translate(selectedCount, candidates.Count));
            header.GapLine(6f);

            float headerHeight = header.CurHeight;
            header.End();

            if (candidates.Count == 0)
            {
                Widgets.Label(new Rect(inRect.x, inRect.y + headerHeight + 4f, inRect.width, 60f), "WWX.NoCandidates".Translate());
                return;
            }

            Rect scrollRect = new Rect(
                inRect.x,
                inRect.y + headerHeight + 4f,
                inRect.width,
                inRect.height - headerHeight - 4f);

            List<XenotypeDef> visible = candidates;
            if (!searchText.NullOrEmpty())
            {
                string search = searchText.ToLower();
                visible = candidates
                    .Where(x =>
                        (x.label != null && x.label.ToLower().Contains(search)) ||
                        x.defName.ToLower().Contains(search))
                    .ToList();
            }

            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, visible.Count * 28f + 20f);
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            foreach (XenotypeDef xenotype in visible)
            {
                bool isSelected = selected.Contains(xenotype.defName);
                bool before = isSelected;

                listing.CheckboxLabeled(xenotype.LabelCap, ref isSelected, xenotype.description);

                if (isSelected == before)
                {
                    continue;
                }

                if (isSelected)
                {
                    selected.Add(xenotype.defName);
                }
                else
                {
                    selected.Remove(xenotype.defName);
                }
                Changed();
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}
