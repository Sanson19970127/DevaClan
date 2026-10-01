using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using TrainworksReloaded.Core.Configuration;
using TrainworksReloaded.Core.Impl;
using TrainworksReloaded.Core.Interfaces;
using TrainworksReloaded.Core.Enum;
using TrainworksReloaded.Base.Effect;
using TrainworksReloaded.Base.Enums;
using TrainworksReloaded.Base.Trigger;
using TrainworksReloaded.Base.Localization;
using TrainworksReloaded.Base.Extensions;

// Executes the installed merger, type resolver and keyword parser outside Unity.
// Unity/Mono-only finalizers and battle/card rendering are not invoked here.
static class ContentRuntimeRegression
{
    const string Guid = "sanson.DevaClan";
    static int checks;
    static void Check(bool ok,string reason) { if(!ok)throw new Exception(reason); checks++; }
    static void Main(string[] args)
    {
        var dirs=new[]{Path.Combine(args[0],"MonsterTrain2_Data/Managed"),Path.Combine(args[0],"BepInEx/core"),
            Path.Combine(args[0],"BepInEx/plugins/Trainworks"),Path.Combine(args[0],"BepInEx/plugins/Conductor/plugins"),args[1]};
        AppDomain.CurrentDomain.AssemblyResolve+=(_,e)=>{
            var name=new AssemblyName(e.Name).Name+".dll";
            var path=dirs.Select(d=>Path.Combine(d,name)).FirstOrDefault(File.Exists);
            return path==null?null:Assembly.LoadFrom(path);
        };
        Run(args[1],args[2]);
        Console.WriteLine("PASS: "+checks+" content runtime checks using installed framework and game assemblies.");
    }
    sealed class Registry<T>:Dictionary<string,T>,IRegister<T>
    {
        public void Register(string key,T item) { Add(key,item); }
        public List<string> GetAllIdentifiers(RegisterIdentifierType type) => Keys.ToList();
        public bool TryLookupIdentifier(string id,RegisterIdentifierType type,out T value,out bool? modded)
        {modded=true;return TryGetValue(id,out value);}
    }
    sealed class Logger<T>:IModLogger<T>
    {
        public void Log(LogLevel level,object data)
        { if((level&(LogLevel.Error|LogLevel.Fatal|LogLevel.Warning))!=0)throw new Exception(data.ToString()); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run(string modPath,string jsonRoot)
    {
        var files=Directory.GetFiles(jsonRoot,"*.json",SearchOption.AllDirectories)
            .Select(p=>Path.GetRelativePath(jsonRoot,p)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        Check(files.Length==23,"Expected 23 modular JSON files");
        using var fileProvider=new PhysicalFileProvider(Path.GetFullPath(jsonRoot));
        var provider=new MergedJsonConfigurationProvider(new MergedJsonConfigurationSource(fileProvider,false,files));
        using var config=new ConfigurationRoot(new List<IConfigurationProvider>{provider});
        Check(config.GetSection("cards").GetChildren().Count()==66,"All 66 cards merged");
        Check(config.GetSection("effects").GetChildren().Count()==90,"All 90 effects merged");
        var atlas=new PluginAtlas();
        var assembly=Assembly.LoadFrom(Path.Combine(modPath,"DevaClan.dll"));
        atlas.PluginDefinitions.Add(Guid,new PluginDefinition(config){Assembly=assembly});
        var definitions=config.GetSection("effects").GetChildren().ToList();
        var converter=definitions.Single(d=>d["id"]=="ConvertEffect");
        Check(converter["name"]=="CardEffectGainEnergy" && converter["param_int"]=="1","Converter uses native one-Ember effect");
        var feast=config.GetSection("cards").GetChildren().Single(c=>c["id"]=="Feast");
        Check(feast["cost"]=="2" && feast["rarity"]=="rare","Feast costs 2 while retaining rarity");
        foreach(var definition in definitions)
        {
            var reference=definition.GetSection("name").ParseReference();
            Check(reference.id.GetFullyQualifiedName<CardEffectBase>(assembly,out var name),"Framework resolves effect: "+definition["id"]);
            var type=typeof(CardEffectBase).Assembly.GetType(name) ?? Type.GetType(name,true);
            Check(Activator.CreateInstance(type) is CardEffectBase,"Constructible effect: "+definition["id"]);
        }
        var trigger=config.GetSection("card_triggers").GetChildren().Single();
        var triggerTypes=new CardTriggerTypeRegister(new Logger<CardTriggerTypeRegister>());
        Check(triggerTypes[trigger["trigger"]]==CardTriggerType.OnKill,"Framework resolves on_kill to native OnKill");
        Check(trigger["effects:0"]=="@ExorciseCharge","Slay references the Charge effect");
        var charge=definitions.Single(d=>d["id"]=="ExorciseCharge");
        Check(charge["param_int"]=="2" && charge["name"]=="@CardEffectGainCharge","Slay effect grants 2 Charge");
        var exorcise=config.GetSection("cards").GetChildren().Single(c=>c["id"]=="Exorcise");
        Check(exorcise["triggers:0"]=="@ExorciseSlay","Exorcise references the native trigger");
        var replacements=new Dictionary<string,ReplacementStringData>();
        var terms=new Dictionary<string,LocalizationTerm>();
        foreach(var section in config.GetSection("replacement_texts").GetChildren())
        {
            var keyword=Guid+"_"+section["key"];
            var textKey="ReplacementStringsData_replacement-"+keyword;
            var replacement=new ReplacementStringData();
            typeof(ReplacementStringData).GetField("_keyword",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(replacement,keyword);
            typeof(ReplacementStringData).GetField("_replacement",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(replacement,textKey);
            replacements.Add(keyword,replacement);
            terms.Add(textKey,section.GetSection("texts").ParseLocalizationTerm());
        }
        Check(replacements.Count==2,"Both custom keyword replacements defined");
        var handler=new LocalizationGlobalParameterHandler(null,true);
        var dict=(Dictionary<string,ReplacementStringData>)typeof(LocalizationGlobalParameterHandler)
            .GetField("_replacements",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(handler);
        foreach(var replacement in replacements.Values)dict.Add(replacement.Keyword,replacement);
        foreach(var replacement in replacements.Values)
        {
            var token=handler.ParseParam(replacement.Keyword,null) as TranslationTokenReplacementString;
            Check(token!=null && token.key==replacement.ReplacementTextKey,"Keyword resolves to registered translation");
            var text=terms[replacement.ReplacementTextKey];
            Check(text.English.StartsWith("<b>") && text.Chinese.StartsWith("<b>"),"Bilingual keyword emphasis preserved");
        }
        // Karmergy is registered by the existing status pipeline. Test its
        // macro spelling here; full status registration needs Unity assets.
        var allowed=new HashSet<string>(replacements.Keys){Guid+"_karmergy"};
        foreach(var item in config.AsEnumerable().Where(k=>k.Value!=null))
            foreach(Match match in Regex.Matches(item.Value,@"\[([^\]]*sanson\.DevaClan_[^\]]*)\]"))
                Check(allowed.Contains(match.Groups[1].Value),"Valid runtime keyword: "+match.Value);
        Check(LocalizationUtil.ReplacePlainBrackets("["+Guid+"_Charge]")=="{["+Guid+"_Charge]}","Game parser wraps keyword token correctly");
    }
}
