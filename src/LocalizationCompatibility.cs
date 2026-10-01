using System;
using HarmonyLib;
using I2.Loc;
using TrainworksReloaded.Base.Localization;

namespace DevaClan
{
    // Trainworks 0.7.20 checks Languages.Length before writing a translation.
    // New MT2 terms initially report Length == 1: AddTerm/SetNumLanguages do
    // not create the separate translation storage. Allocate it before that
    // check, using the same Group setter as the upstream Trainworks fix.
    [HarmonyPatch(typeof(CustomLocalizationTermRegistry), "SetLanguage")]
    internal static class LocalizationCompatibility
    {
        internal static int RepairedTerms;

        internal static bool IsDevaTerm(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (key.StartsWith("Default/", StringComparison.Ordinal) ||
                key.StartsWith("Default\\", StringComparison.Ordinal)) key = key.Substring(8);
            // Trainworks keys include a type/field prefix before the mod ID,
            // e.g. CardData_nameKey-sanson.DevaClan-Card-Mara.
            int start = key.IndexOf(Plugin.Guid, StringComparison.Ordinal);
            if (start < 0 || (start > 0 && key[start - 1] != '-')) return false;
            int end = start + Plugin.Guid.Length;
            return end < key.Length && (key[end] == '-' || key[end] == '_');
        }

        internal static void Prefix(TermData termData)
        {
            if (termData == null || !IsDevaTerm(termData.Term)) return;

            var source = termData.languageSourceData;
            if (source == null || termData.Languages.Length >= source.mLanguages.Count) return;
            termData.Group = termData.Group;
            termData.SetNumLanguages(source.mLanguages.Count);
            RepairedTerms++;
        }
    }

    [HarmonyPatch(typeof(CustomLocalizationTermRegistry), "LoadData")]
    internal static class LocalizationVerification
    {
        static void Postfix()
        {
            int terms = 0, missing = 0;
            bool maraFound = false, maraPassed = false;
            foreach (var source in LocalizationManager.Sources)
            {
                int zh = source.GetLanguageIndex("Chinese", true, false);
                if (zh < 0) continue;
                foreach (var term in source.mTerms)
                {
                    if (!LocalizationCompatibility.IsDevaTerm(term.Term)) continue;
                    terms++;
                    string value = zh < term.Languages.Length ? term.Languages[zh] : null;
                    if (string.IsNullOrEmpty(value)) missing++;
                    if (term.Term == "CardData_nameKey-sanson.DevaClan-Card-Mara")
                    {
                        maraFound = true;
                        maraPassed = value == "“魔罗”波甸";
                        Plugin.Log.LogInfo($"DEVA LOCALIZATION CHECK: language={source.mLanguages[zh].Name}; Mara={value}; expected=“魔罗”波甸.");
                    }
                }
            }
            string report = $"DEVA LOCALIZATION CHECK: repaired={LocalizationCompatibility.RepairedTerms}; terms={terms}; missing Chinese={missing}; Mara={(maraFound && maraPassed ? "PASS" : "FAIL")}.";
            if (terms == 0 || missing != 0 || !maraFound || !maraPassed) Plugin.Log.LogError(report);
            else Plugin.Log.LogInfo(report);
        }
    }
}
