using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using I2.Loc;
using TrainworksReloaded.Base.Localization;

// Runs real managed game/framework methods outside Unity. An inert translations
// asset supplies managed storage only; no native Unity method is invoked.
static class LocalizationRegression
{
    static int checks;
    static void Main(string[] args)
    {
        string game = args[0], mod = args[1];
        var dirs = new[] { Path.Combine(game,"MonsterTrain2_Data/Managed"), Path.Combine(game,"BepInEx/core"),
            Path.Combine(game,"BepInEx/plugins/Trainworks"), Path.Combine(game,"BepInEx/plugins/Conductor/plugins"), mod };
        AppDomain.CurrentDomain.AssemblyResolve += (_,e) => {
            var name = new AssemblyName(e.Name).Name + ".dll";
            var path = dirs.Select(d => Path.Combine(d,name)).FirstOrDefault(File.Exists);
            return path == null ? null : Assembly.LoadFrom(path);
        };
        Run(mod);
        Console.WriteLine("PASS: " + checks + " localization regression checks using installed game and Trainworks assemblies.");
    }
    static void Check(bool condition, string message) { if(!condition)throw new Exception(message); checks++; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run(string mod)
    {
        var source = new LanguageSourceData();
        source.mLanguages.Add(new LanguageData { Name="English [en-US]",Code="en-US" });
        source.mLanguages.Add(new LanguageData { Name="Chinese",Code="zh-CN" });
        source.mLanguages.Add(new LanguageData { Name="Chinese (Traditional)",Code="zh-TW" });
        var asset = (LanguageSourceTranslationsData)FormatterServices.GetUninitializedObject(typeof(LanguageSourceTranslationsData));
        // Unity's null operator only reads this marker; no pointer is dereferenced.
        typeof(UnityEngine.Object).GetField("m_CachedPtr",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(asset,new IntPtr(1));
        typeof(LanguageSourceTranslationsData).GetField("terms",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(asset,new List<TermTranslationsData>());
        typeof(LanguageSourceTranslationsData).GetField("termsDict",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(asset,new Dictionary<string,TermTranslationsData>());
        source.translationsData=asset;
        var write=typeof(CustomLocalizationTermRegistry).GetMethod("SetLanguage",BindingFlags.NonPublic|BindingFlags.Static);
        var patch=Assembly.LoadFrom(Path.Combine(mod,"DevaClan.dll")).GetType("DevaClan.LocalizationCompatibility").GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static);
        // These shapes come from Trainworks pipelines, not from the patch's
        // matching logic. In particular the framework type prefix comes FIRST.
        var keys = new[] {
            "CardData_nameKey-sanson.DevaClan-Card-Mara",
            "CardData_descriptionKey-sanson.DevaClan-Card-Mantra",
            "CharacterData_nameKey-sanson.DevaClan-Character-Mara",
            "CharacterTriggerData_descriptionKey-sanson.DevaClan-CharacterTrigger-MaraCreator",
            "CardUpgrade_titleKey-sanson.DevaClan-Upgrade-MaraCreator1",
            "ClassData_titleKey-sanson.DevaClan-Class-ClassDeva",
            "ClassData_descriptionKey-sanson.DevaClan-Class-ClassDeva",
            "SubtytpesData_nameKey-sanson.DevaClan-Subtype-Construct",
            "RoomModifierData_descriptionKey-sanson.DevaClan-RoomModifier-Acceleration",
            "RelicData_titleKey-sanson.DevaClan-RelicData-Incense",
            "AdditionalTooltipData_tooltipTitleKey-sanson.DevaClan-AdditionalTooltip-KarmaTip",
            "sanson.DevaClan_karmergy",
            "ReplacementStringsData_replacement-sanson.DevaClan_Charge",
            "ReplacementStringsData_replacement-sanson.DevaClan_Reset",
            "ReplacementStringsData_replacement-sanson.DevaClan_karmergy",
            "CardTriggerEffectData_descriptionKey-sanson.DevaClan-CardTrigger-ExorciseSlay",
            "Default/CardData_nameKey-sanson.DevaClan-Card-Mara",
            "Default\\CardData_nameKey-sanson.DevaClan-Card-Mara"
        };
        foreach(var key in keys)
        {
            var term = new TermData(source); term.Term=key; term.SetNumLanguages(3);
            Check(term.Languages.Length==1,"Expected new-term storage bug");
            write.Invoke(null,new object[]{term,0,"Mara"});
            write.Invoke(null,new object[]{term,1,"魔罗"});
            Check(term.Languages.Length==1,"Old writer should skip Chinese");
            patch.Invoke(null,new object[]{term});
            Check(term.Languages.Length==3,"Patch must allocate all language slots");
            write.Invoke(null,new object[]{term,1,"魔罗"});
            Check(term.Languages[1]=="魔罗","Chinese must persist");
            Check(term.Languages[0]=="Mara","English must be preserved");
            patch.Invoke(null,new object[]{term});
            Check(term.Languages[1]=="魔罗","Patch must be idempotent");
        }
        foreach(var key in new[]{"OtherMod-Card-Test","CardData_nameKey-sanson.DevaClanExtra-Card-Mara","CardData_nameKey-notsanson.DevaClan-Card-Mara"})
        {
            var unrelated=new TermData(source);unrelated.Term=key;
            patch.Invoke(null,new object[]{unrelated});
            Check(unrelated.Languages.Length==1,"Other mods must be unaffected: "+key);
        }
        Check(source.GetLanguageIndex("Chinese")==1,"Chinese is a valid language name");
        source.mLanguages[1].Name="Chinese (Simplified)";
        Check(source.GetLanguageIndex("Chinese")==1,"Game supports simplified Chinese alias matching");
    }
}
