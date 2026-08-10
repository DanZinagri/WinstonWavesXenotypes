using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WinstonWavesXenotypes
{
    // Adds a fourth tab to the Winston Waves settings window. VSEWW.WinstonMod is internal and
    // draws its tab strip and its tab body in one method, so the only clean seam is to replace
    // that method: we redraw the same three tabs (calling straight back into its own private
    // drawing methods), plus ours.
    public static class Patch_WinstonSettingsTab
    {
        /// Deliberately far away from Winston's own tab in case it gains more tabs.
        private const int ColonistXenotypeTab = 100;

        private const int SanguophageXenotypeTab = 101;

        private const float TopAreaHeight = 40f;

        public static bool Injected;

        private static FieldInfo currentTabField;
        private static MethodInfo doGameSettings;
        private static MethodInfo doWaveSettings;
        private static MethodInfo doModifierSettings;

        public static void Apply(Harmony harmony)
        {
            Type winstonMod = AccessTools.TypeByName("VSEWW.WinstonMod");
            MethodInfo target = winstonMod == null
                ? null
                : AccessTools.Method(winstonMod, "DoSettingsWindowContents", new[] { typeof(Rect) });

            currentTabField = winstonMod == null ? null : AccessTools.Field(winstonMod, "currentTab");
            doGameSettings = winstonMod == null ? null : AccessTools.Method(winstonMod, "DoGameSettings", new[] { typeof(Rect) });
            doWaveSettings = winstonMod == null ? null : AccessTools.Method(winstonMod, "DoWaveSettings", new[] { typeof(Rect) });
            doModifierSettings = winstonMod == null ? null : AccessTools.Method(winstonMod, "DoModifierSettings", new[] { typeof(Rect) });

            if (target == null || currentTabField == null || doGameSettings == null || doWaveSettings == null || doModifierSettings == null)
            {
                Log.Warning("[WinstonWavesXenotypes] Could not hook the Winston Waves settings window; the xenotype settings will get their own mod settings entry instead.");
                return;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(typeof(Patch_WinstonSettingsTab), nameof(Prefix)));
            Injected = true;
        }

        public static bool Prefix(Mod __instance, Rect inRect)
        {
            int currentTab = (int)currentTabField.GetValue(__instance);

            Rect tabRect = new Rect(inRect);
            tabRect.y = inRect.y + TopAreaHeight;

            Rect mainRect = new Rect(inRect);
            mainRect.height = inRect.height - TopAreaHeight;
            mainRect.y = inRect.y + TopAreaHeight;

            Widgets.DrawMenuSection(mainRect);

            List<TabRecord> tabs = new List<TabRecord>
            {
                new TabRecord("VESWW.GS".Translate(), () => SelectTab(__instance, 0), currentTab == 0),
                new TabRecord("VESWW.VS".Translate(), () => SelectTab(__instance, 1), currentTab == 1),
                new TabRecord("VESWW.MS".Translate(), () => SelectTab(__instance, 2), currentTab == 2),
                new TabRecord("WWX.TabColonists".Translate(), () => SelectTab(__instance, ColonistXenotypeTab), currentTab == ColonistXenotypeTab),
                new TabRecord("WWX.TabSanguophages".Translate(), () => SelectTab(__instance, SanguophageXenotypeTab), currentTab == SanguophageXenotypeTab)
            };
            TabDrawer.DrawTabs(tabRect, tabs, 200f);

            Rect contentRect = mainRect.ContractedBy(15f);
            switch (currentTab)
            {
                case 0:
                    doGameSettings.Invoke(__instance, new object[] { contentRect });
                    break;
                case 1:
                    doWaveSettings.Invoke(__instance, new object[] { contentRect });
                    break;
                case 2:
                    doModifierSettings.Invoke(__instance, new object[] { contentRect });
                    break;
                case ColonistXenotypeTab:
                    WinstonWavesXenotypesMod.Settings.DoColonistTab(contentRect);
                    break;
                case SanguophageXenotypeTab:
                    WinstonWavesXenotypesMod.Settings.DoSanguophageTab(contentRect);
                    break;
            }

            return false;
        }

        private static void SelectTab(Mod winstonMod, int tab)
        {
            currentTabField.SetValue(winstonMod, tab);
            // Winston Waves saves on every tab change; keep that behaviour, and save ours too.
            winstonMod.WriteSettings();
            WinstonWavesXenotypesMod.WriteSettingsIfDirty();
        }
    }

    // Only the mod whose settings page is open gets WriteSettings called on close, and ours is
    // riding along inside someone else's page - so save on the way out ourselves.
    [HarmonyPatch(typeof(Dialog_ModSettings), nameof(Dialog_ModSettings.PreClose))]
    public static class Patch_ModSettingsClosed
    {
        public static void Postfix()
        {
            WinstonWavesXenotypesMod.WriteSettingsIfDirty();
        }
    }
}
