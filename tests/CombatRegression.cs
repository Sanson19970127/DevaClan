using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;

// Real managed game objects, inert Unity handles; no scene/animation or native calls.
static class CombatRegression
{
    static Assembly mod;
    static int checks, handles;
    static readonly List<IntPtr> pointers = new List<IntPtr>();
    static readonly BindingFlags Fields = BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static void Main(string[] args)
    {
        string game=args[0], root=args[1];
        string[] dirs={Path.Combine(game,"MonsterTrain2_Data/Managed"),Path.Combine(game,"BepInEx/core"),
            Path.Combine(game,"BepInEx/plugins/Trainworks"),Path.Combine(game,"BepInEx/plugins/Conductor/plugins"),root};
        AppDomain.CurrentDomain.AssemblyResolve += (_,e) => {
            var path=dirs.Select(d=>Path.Combine(d,new AssemblyName(e.Name).Name+".dll")).FirstOrDefault(File.Exists);
            return path==null?null:Assembly.LoadFrom(path);
        };
        try
        {
            Run(root);
            Console.WriteLine("PASS: "+checks+" combat managed regression checks (not Unity playtesting).");
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode=1; }
        finally { foreach(var pointer in pointers) Marshal.FreeHGlobal(pointer); }
    }
    static void Check(bool ok,string label) { if(!ok)throw new Exception(label); checks++; }
    static void Set(object obj,string field,object value)
    {
        var t=obj.GetType(); FieldInfo f=null;
        while(t!=null && (f=t.GetField(field,Fields))==null)t=t.BaseType;
        if(f==null)throw new Exception("Missing field "+field);
        f.SetValue(obj,value);
    }
    static object Get(object obj,string field)=>obj.GetType().GetField(field,Fields).GetValue(obj);
    static object Call(string type,string method,params object[] args)=>mod.GetType("DevaClan."+type).GetMethod(method,Fields).Invoke(null,args);
    static T Inert<T>() where T:class
    {
        var obj=(T)FormatterServices.GetUninitializedObject(typeof(T));
        if(obj is UnityEngine.Object)
        {
            typeof(UnityEngine.Object).GetField("OffsetOfInstanceIDInCPlusPlusObject",Fields).SetValue(null,0);
            var pointer=Marshal.AllocHGlobal(4);pointers.Add(pointer);Marshal.WriteInt32(pointer,++handles);
            Set(obj,"m_CachedPtr",pointer);
        }
        return obj;
    }
    static CardState Card(string id) {
        var c=Inert<CardState>(); Set(c,"cardDataID",id);
        Set(c,"cardModifiers",new CardStateModifiers()); Set(c,"temporaryCardModifiers",new CardStateModifiers());
        return c;
    }
    static CharacterState Unit(CardState card,int floor=0,Team.Type team=Team.Type.Monsters,int hp=10)
    {
        var c=Inert<CharacterState>();
        var t=typeof(CharacterState).GetNestedType("StateInformation",BindingFlags.NonPublic);
        var info=FormatterServices.GetUninitializedObject(t);
        Set(info,"hp",new ShinyShoe.ObfuscatedNumber(hp));
        Set(info,"statusEffects",new Dictionary<string,CharacterState.StatusEffectStack>());
        Set(info,"appliedCardUpgrades",new List<CardUpgradeState>());
        var room=Inert<RoomState>(); Set(room,"roomIndex",floor);
        var point=Inert<SpawnPoint>(); Set(point,"roomState",room); Set(point,"characterState",c);
        Set(info,"spawnPoint",point); Set(c,"_primaryStateInformation",info);
        Set(c,"spawnerCard",card);Set(c,"teamType",team);return c;
    }
    static bool Contains(CharacterState c)=>(bool)Call("TemporaryMemory","Contains",c);
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run(string root)
    {
        mod=Assembly.LoadFrom(Path.Combine(root,"DevaClan.dll"));
        Call("BattleMemory","Clear");
        var caster=Unit(Card("chant"));var friend=Unit(Card("friend"));
        var effect=Inert<CardEffectState>();
        var impl=new DevaClan.CardEffectOtherAllyStatus();
        var p=new CardEffectParams {characterThatActivatedAbility=caster,selfTarget=friend};p.targets.Add(friend);
        Check(impl.TestEffectOnTarget(effect,p,friend,null),"hover selfTarget=candidate must remain selectable");
        Check(impl.TestEffect(effect,p,null),"hover full TestEffect accepts friend");
        p.selfTarget=caster;
        Check(impl.TestEffectOnTarget(effect,p,friend,null),"play selfTarget=caster accepts friend");
        Check(!impl.TestEffectOnTarget(effect,p,caster,null),"cannot target caster");
        Check(!impl.TestEffectOnTarget(effect,p,Unit(Card("enemy"),0,Team.Type.Heroes),null),"cannot target enemy");
        Check(!impl.TestEffectOnTarget(effect,p,Unit(Card("other-floor"),1),null),"cannot target other floor");
        Check(!impl.TestEffectOnTarget(effect,p,Unit(Card("dead"),0,Team.Type.Monsters,0),null),"cannot target dead");
        p.targets.Clear();Check(!impl.TestEffect(effect,p,null),"empty target invalid");
        p.characterThatActivatedAbility=null;Check(!impl.TestEffectOnTarget(effect,p,friend,null),"missing caster invalid");

        var source=Card("same-type");var unrelated=Card("same-type");var unit=Unit(source);
        Call("TemporaryMemory","Remember",unit,true);
        Check(Contains(unit),"mark persists on unit");
        Check(Contains(Unit(source)),"replacement using same card preserves restriction");
        Check(!Contains(Unit(unrelated)),"another copy of same card type unaffected");
        var copy=Card("same-type");Call("CopyTemporaryCard","Postfix",source,copy);
        Check(Contains(Unit(copy)),"explicit copy inherits restriction");
        Check(!(bool)Call("PreserveTemporaryStatus","Prefix",unit,"sanson.devaclan_temporary"),"cleanse cannot erase restriction");
        Check((bool)Call("PreserveTemporaryStatus","Prefix",unit,"silenced"),"native status removal unaffected");
        Check((bool)Call("PreserveTemporaryStatus","Prefix",unit,"othermod_temporary"),"other mods unaffected");
        Call("BattleMemory","BeginPreview");Call("TemporaryMemory","Remember",Unit(unrelated),true);
        Check(Contains(Unit(unrelated)),"preview has its own mark");Call("BattleMemory","EndPreview");
        Check(!Contains(Unit(unrelated)),"preview mark does not leak into real battle");
        Check(Contains(Unit(source)),"preview rollback preserves real mark");
        Call("BattleMemory","Clear");Check(!Contains(Unit(source)),"next battle clears ledger");

        var registry=(Dictionary<string,CardData>)mod.GetType("DevaClan.Plugin").GetField("Cards",Fields).GetValue(null);
        var data=Inert<CardData>();Set(data,"id","arena");registry["AsuraArena"]=data;
        var arena=Inert<TrainRoomAttachmentState>();Set(arena,"_cardState",Card("arena"));
        var other=Inert<TrainRoomAttachmentState>();Set(other,"_cardState",Card("other"));
        foreach(var flag in new[]{"isOuterTrainBoss","isMiniboss","isTrueFinalBoss","isCompanionBoss"})
        {
            var boss=Unit(Card(flag),0,Team.Type.Heroes);Set(boss,flag,true);
            Check((bool)Call("AsuraArenaFilter","Excludes",arena,boss),"arena excludes "+flag);
            Check(!(bool)Call("AsuraArenaFilter","Excludes",other,boss),"other room unchanged "+flag);
            var arguments=new object[]{arena,boss,false};Call("AsuraArenaIgnoreBosses","Postfix",arguments);
            Check((bool)arguments[2],"filter hook excludes "+flag);
            var probe=new Probe();var guarded=(IEnumerator)Call("AsuraArenaApplyGuard","Postfix",probe,arena,boss);
            Check(!guarded.MoveNext() && probe.Steps==0,"direct apply skips boss without stripping statuses");
        }
        Check(!(bool)Call("AsuraArenaFilter","Excludes",arena,friend),"regular unit not boss-filtered");
        var ordinaryProbe=new Probe();var allowed=(IEnumerator)Call("AsuraArenaApplyGuard","Postfix",ordinaryProbe,arena,friend);
        allowed.MoveNext();Check(ordinaryProbe.Steps==1,"normal application executes");
        Check(impl.CanPlayWhenHandFull,"default effects allow full hand");
        Check(!new DevaClan.CardEffectIdatenRecall().CanPlayWhenHandFull,"Idaten needs hand space");
        // The preview guard is also asserted in source: the idaten branch checks
        // SaveManager.PreviewMode before ReturnUnitCardToHand, which is Unity-only.
        string effectsSource=File.ReadAllText(Path.Combine(root,"..","..","..","src","AttackEffects.cs"));
        Check(effectsSource.Contains("!g.GetSaveManager().PreviewMode") && effectsSource.Contains("!cm.GetHand().Contains(card)"),"Idaten recall is preview-safe and duplicate-safe");
        Check(!effectsSource.Contains("haste"),"Idaten must not grant the enemy-only haste trait");
        RebirthAndBowl();
        RefactorContracts();
        FeedbackRegressions();
    }
    static void RefactorContracts()
    {
        Check(mod.GetType("DevaClan.CardEffectDeva")==null,"universal dispatcher removed");
        Check(mod.GetType("DevaClan.CardEffectDevaExorcise")==null,"handwritten kill detection removed");
        Check(new DevaClan.CardEffectOtherAllyStatus() is CardEffectAddStatusEffect,"targeted ability inherits native status semantics");
        Check(!new DevaClan.CardEffectCreateConstruct().CanPlayWhenHandFull,"creation requires hand space");
        Check(!new DevaClan.CardEffectRebirth().CanPlayWhenHandFull,"rebirth requires hand space");
        Check(new DevaClan.CardEffectGainCharge().CanPlayWhenHandFull,"charge does not require hand space");
        var e=Inert<CardEffectState>();
        var stacks=new[]{new StatusEffectStackData{statusId="untouchable",count=1}};
        Set(e,"paramStatusEffects",stacks);
        var ids=new List<string>();
        new DevaClan.CardEffectTurnStasis().GetTooltipsStatusList(e,ref ids);
        Check(ids.SequenceEqual(new[]{"untouchable"}),"stasis reports native tooltip");
        new DevaClan.CardEffectTurnStasis().GetTooltipsStatusList(e,ref ids);
        Check(ids.Count==1,"custom collector avoids duplicate status tooltip");
        ids.Clear();new DevaClan.CardEffectPerfectCreation().GetTooltipsStatusList(e,ref ids);
        Check(ids.Contains("sanson.devaclan_temporary"),"temporary summon reports status tooltip");
    }
    static object InstanceCall(object obj,string method,params object[] args) => obj.GetType().GetMethod(method,Fields).Invoke(obj,args);
    static CardUpgradeData Upgrade(string id)
    {
        var data=Inert<CardUpgradeData>(); Set(data,"id",id); return data;
    }
    static CardUpgradeState UpgradeState(CardUpgradeData data)
    {
        var state=new CardUpgradeState(); Set(state,"cardUpgradeDataId",data.GetID()); return state;
    }
    static void Status(CharacterState c,string id,int count)
    {
        ((Dictionary<string,CharacterState.StatusEffectStack>)Get(Get(c,"_primaryStateInformation"),"statusEffects"))[id]
            =new CharacterState.StatusEffectStack(null,count);
    }
    static void FeedbackRegressions()
    {
        var registry=(Dictionary<string,CardUpgradeData>)mod.GetType("DevaClan.Plugin").GetField("Upgrades",Fields).GetValue(null);
        var champion=Unit(Card("mara"));
        for(int level=1;level<=3;level++)
        {
            var upgrade=Upgrade("liberation"+level); registry["MaraLiberation"+level]=upgrade;
            champion.GetSpawnerCard().GetCardStateModifiers().AddUpgrade(UpgradeState(upgrade));
            Check((int)Call("Plugin","Level",champion,"MaraLiberation")==level,"reads champion path from spawning card at level "+level);
        }
        Check((int)Call("Plugin","Level",Unit(Card("other")),"MaraLiberation")==0,"unupgraded unit has no liberation");
        var applied=Unit(Card("applied"));
        ((List<CardUpgradeState>)Get(Get(applied,"_primaryStateInformation"),"appliedCardUpgrades")).Add(UpgradeState(registry["MaraLiberation2"]));
        Check((int)Call("Plugin","Level",applied,"MaraLiberation")==2,"runtime character upgrades still supported");

        Call("BattleMemory","Clear");
        var memory=mod.GetType("DevaClan.BattleMemory").GetProperty("Active",Fields).GetValue(null);
        var a=Card("identical");var b=Card("identical");
        foreach(var name in new[]{"Karma","Discounts"})
        {
            var ledger=(Dictionary<CardState,int>)Get(memory,name); ledger[a]=4;
            Check(!ledger.ContainsKey(b),name+" does not leak to another copy");
            Call("BattleMemory","BeginPreview");
            var preview=mod.GetType("DevaClan.BattleMemory").GetProperty("Active",Fields).GetValue(null);
            ((Dictionary<CardState,int>)Get(preview,name))[a]=9;
            Call("BattleMemory","EndPreview");
            Check(ledger[a]==4,name+" preview does not change actual value");
        }

        var arenaUpgrade=Upgrade("arena-relentless");registry["AsuraArenaUpgrade"]=arenaUpgrade;
        var enemy=Unit(Card("ordinary"),0,Team.Type.Heroes);
        ((List<CardUpgradeState>)Get(Get(enemy,"_primaryStateInformation"),"appliedCardUpgrades")).Add(UpgradeState(arenaUpgrade));
        var destroy=new object[]{enemy,true};Call("AsuraArenaNoRoomDestruction","Postfix",destroy);
        Check(!(bool)destroy[1],"arena does not let ordinary enemies destroy floors");
        foreach(var flag in new[]{"isOuterTrainBoss","isMiniboss","isTrueFinalBoss","isCompanionBoss"})
        {
            Set(enemy,flag,true);destroy=new object[]{enemy,true};Call("AsuraArenaNoRoomDestruction","Postfix",destroy);
            Check((bool)destroy[1],"preserves native floor destruction for "+flag);Set(enemy,flag,false);
        }
        var source=Inert<CharacterData>();Set(source,"startingStatusEffects",new[]{new StatusEffectStackData{statusId="relentless",count=1}});
        Set(enemy,"characterData",source);
        Check(!(bool)Call("AsuraArenaFilter","ArenaRelentlessOnly",enemy),"preserves innate relentless on non-boss enemies");
        destroy=new object[]{Unit(Card("unaffected"),0,Team.Type.Heroes),true};Call("AsuraArenaNoRoomDestruction","Postfix",destroy);
        Check((bool)destroy[1],"other enemies retain native behavior");

        foreach(var id in new[]{"relentless","untouchable"})
        {
            var cardKey=StatusEffectManager.GetLocalizedCardTooltipTextKey(id);
            var characterKey=StatusEffectManager.GetLocalizedCharacterTooltipTextKey(id);
            Check((string)Call("NativeStatusTooltips","Resolve",id,cardKey,(Func<string,bool>)(k=>k==characterKey))==characterKey,"fallback uses native character tooltip: "+id);
            Check((string)Call("NativeStatusTooltips","Resolve",id,cardKey,(Func<string,bool>)(k=>true))==cardKey,"existing card translation takes precedence: "+id);
            Check((string)Call("NativeStatusTooltips","Resolve",id,cardKey,(Func<string,bool>)(k=>false))==cardKey,"does not substitute another missing term: "+id);
        }
        Check((string)Call("NativeStatusTooltips","Resolve","other","other-key",(Func<string,bool>)(k=>throw new Exception("unexpected lookup")))=="other-key","other statuses unaffected");
        ChargeAndConverter();
    }
    static void ChargeAndConverter()
    {
        Check((bool)Call("ChargeAbilities","Affordable",3,3),"exact Charge balance can pay cooldown");
        Check((bool)Call("ChargeAbilities","Affordable",3,4),"excess Charge can pay cooldown");
        Check(!(bool)Call("ChargeAbilities","Affordable",3,2),"insufficient Charge cannot pay cooldown");
        Check(!(bool)Call("ChargeAbilities","Affordable",0,5),"ready ability does not spend Charge");
        var character=Unit(Card("ability-user"));Status(character,"cooldown",3);
        var args=new object[]{character,UnitAbilityUIState.Create(CharacterState.UnitAbilityAvailability.CanActivate,false,0,false)};
        Call("ChargeAbilityDisplayState","Postfix",args);
        var usable=(UnitAbilityUIState)args[1];
        Check(usable.abilityAvailability==CharacterState.UnitAbilityAvailability.CanActivate && usable.cooldown==3 && usable.hasCooldown,"clickable state carries remaining cooldown");
        var display=new object[]{usable,false};Call("ChargeAbilityVisuals","Prefix",display);
        var shown=(UnitAbilityUIState)display[0];
        Check((bool)display[1] && shown.abilityAvailability==CharacterState.UnitAbilityAvailability.Cooldown && shown.cooldown==3,"Charge-ready display uses native cooldown icon and number");
        Check(usable.abilityAvailability==CharacterState.UnitAbilityAvailability.CanActivate,"display conversion leaves caller eligibility intact");
        foreach(var availability in new[]{CharacterState.UnitAbilityAvailability.Cooldown,CharacterState.UnitAbilityAvailability.Disabled,CharacterState.UnitAbilityAvailability.DeploymentPhase})
            Check(!(bool)Call("ChargeAbilityVisuals","UsesChargeVisual",UnitAbilityUIState.Create(availability,true,3,false)),"no Charge tint for "+availability);
        Check(!(bool)Call("ChargeAbilityVisuals","UsesChargeVisual",UnitAbilityUIState.Create(CharacterState.UnitAbilityAvailability.CanActivate,true,3,true)),"silenced ability never tinted");
        Check(!(bool)Call("ChargeAbilityVisuals","UsesChargeVisual",UnitAbilityUIState.Create(CharacterState.UnitAbilityAvailability.CanActivate,true,0,false)),"zero cooldown uses native ready icon");

        var ability=Card("native-room-ability");Set(ability,"isRoomAbility",true);
        var room=Inert<RoomState>();var data=Inert<CardData>();Set(data,"id","native-room-ability");
        var upgrade=Upgrade("native-room-upgrade");Set(upgrade,"roomAbilityUpgrade",data);
        var attachment=Inert<TrainRoomAttachmentState>();Set(attachment,"_isActive",true);Set(attachment,"_trainSide",Team.Type.Monsters);
        Set(attachment,"_upgradeStates",new List<(CardUpgradeState,CardUpgradeData)>{(UpgradeState(upgrade),upgrade)});
        Set(attachment,"_charactersImpacted",new List<CharacterState>());Set(attachment,"_roomStateModifiers",new List<IRoomStateModifier>());
        Set(attachment,"_cooldownCurrent",3);
        var charge=new DevaClan.ChargeResource();charge.SetValue(null,3,notify:false);mod.GetType("DevaClan.Plugin").GetField("Charge",Fields).SetValue(null,charge);
        var roomState=new object[]{attachment,CharacterState.UnitAbilityAvailability.Cooldown};Call("RoomChargeAvailability","Prefix",roomState);
        Check((CharacterState.UnitAbilityAvailability)roomState[1]==CharacterState.UnitAbilityAvailability.CanActivate,"original room ability becomes selectable when Charge covers cooldown");
        var preview=new TrainRoomAttachmentState(attachment);preview.ClearAbilityCooldown();
        Set(room,"attachments",new List<TrainRoomAttachmentState>{attachment});Set(room,"previewAttachments",new List<TrainRoomAttachmentState>{preview});
        Check(ReferenceEquals(Call("ChargeAbilities","FindRoomAbility",room,ability,false),attachment),"native room ability maps to actual building");
        Check(ReferenceEquals(Call("ChargeAbilities","FindRoomAbility",room,ability,true),preview),"preview room ability uses isolated building cooldown");
        Set(attachment,"_isActive",false);Check(Call("ChargeAbilities","FindRoomAbility",room,ability,false)==null,"inactive building excluded");Set(attachment,"_isActive",true);
        Set(attachment,"_trainSide",Team.Type.Heroes);Check(Call("ChargeAbilities","FindRoomAbility",room,ability,false)==null,"enemy building excluded");

        var cards=(Dictionary<string,CardData>)mod.GetType("DevaClan.Plugin").GetField("Cards",Fields).GetValue(null);
        var convert=Inert<CardData>();Set(convert,"id","convert");cards["Convert"]=convert;
        var effect=Inert<CardEffectState>();Set(effect,"cardEffect",new CardEffectGainEnergy());
        var p=new CardEffectParams{playedCard=Card("convert")};
        Check(!effect.GetCardEffect().CanApplyInPreviewMode,"native Ember gain remains prohibited in combat preview");
        var test=new object[]{effect,p,true};Call("ConversionTargetingTest","Prefix",test);
        Check((bool)test[2],"normal combat preview flag untouched");
        var scope=new object[]{p.playedCard,false};Call("ConversionTargetingScope","Prefix",scope);
        test=new object[]{effect,p,true};Call("ConversionTargetingTest","Prefix",test);
        Check(!(bool)test[2],"selection-only test permits converter caster");
        p.playedCard=Card("other");test=new object[]{effect,p,true};Call("ConversionTargetingTest","Prefix",test);
        Check((bool)test[2],"other card tests retain preview restriction");
        p.playedCard=Card("convert");Set(effect,"cardEffect",new CardEffectAddStatusEffect());test=new object[]{effect,p,true};Call("ConversionTargetingTest","Prefix",test);
        Check((bool)test[2],"other effect tests retain preview restriction");
        var error=new InvalidOperationException("selection failure");
        Check(ReferenceEquals(Call("ConversionTargetingScope","Finalizer",error,scope[1]),error),"selection error preserved");
        Check((int)mod.GetType("DevaClan.ConversionTargetingScope").GetField("Depth",Fields).GetValue(null)==0,"selection scope released even on exception");
        NativeEmberAndCharge();
    }
    static void NativeEmberAndCharge()
    {
        var save=Inert<SaveManager>();var player=Inert<PlayerManager>();var combat=Inert<CombatManager>();
        var data=Inert<AllGameData>();var balance=Inert<BalanceData>();Set(balance,"maxEnergy",10);Set(data,"balanceData",balance);
        Set(save,"allGameData",data);Set(player,"saveManager",save);Set(player,"energy",2);
        Set(player,"energyChangedSignal",new ShinyShoe.Signal<int>());
        Set(combat,"combatPhase",CombatManager.Phase.MonsterTurn);
        var g=DispatchProxy.Create<ICoreGameManagers,ManagerProxy>();var proxy=(ManagerProxy)(object)g;
        proxy.Save=save;proxy.Player=player;proxy.Combat=combat;
        var sys=DispatchProxy.Create<ISystemManagers,ManagerProxy>();
        var effect=Inert<CardEffectState>();var impl=new CardEffectGainEnergy();Set(effect,"cardEffect",impl);Set(effect,"paramInt",1);
        Check(impl.TestEffect(effect,new CardEffectParams(),g),"native converter accepts monster turn");
        Drain(impl.ApplyEffect(effect,new CardEffectParams(),g,sys));
        Check(player.GetEnergy()==3,"actual native converter adds exactly one Ember");
        var charge=new DevaClan.ChargeResource();
        mod.GetType("DevaClan.Plugin").GetField("Charge",Fields).SetValue(null,charge);
        charge.SetValue(null,3,notify:false);
        Check((bool)Call("ChargeAbilities","CanPay",3,g),"payment reads real Charge balance");
        Check(!(bool)Call("ChargeAbilities","CanPay",4,g),"payment rejects insufficient real Charge");
        charge.OnCombatPreviewEnabled();
        var previewStats=(int[])typeof(Conductor.TrackedValues.SimpleGlobalTrackedValueHandler).GetField("previewStats",Fields).GetValue(charge);
        previewStats[0]=1;
        Check(charge.GetCurrentValue()==3 && charge.GetCurrentValue(true)==1,"preview charge spending leaves actual Charge intact");
        charge.OnCombatPreviewDisabled();charge.UpdateStatsForNextTurn();
        Check(charge.GetCurrentValue()==3,"Charge persists into next turn");
        charge.Reset();
        Check(charge.GetCurrentValue()==0,"battle/replay reset clears Charge");
        mod.GetType("DevaClan.Plugin").GetField("Charge",Fields).SetValue(null,null);
    }
    static object Property(object obj,string name) => obj.GetType().GetProperty(name,Fields).GetValue(obj);
    static HashSet<CardState> Dead() => (HashSet<CardState>)Get(mod.GetType("DevaClan.BattleMemory").GetProperty("Active",Fields).GetValue(null),"Dead");
    static void RebirthAndBowl()
    {
        Call("BattleMemory","Clear");
        var dead=Card("same-unit"); Set(dead,"cardType",CardType.Monster);
        var unplayed=Card("same-unit"); Set(unplayed,"cardType",CardType.Monster);
        var spell=Card("spell"); Set(spell,"cardType",CardType.Spell);
        var offPile=Card("off-pile"); Set(offPile,"cardType",CardType.Monster);
        var cm=Inert<CardManager>();
        var pile=new List<CardState>{dead,unplayed,spell}; Set(cm,"exhaustedCards",pile);
        Dead().Add(dead); Dead().Add(offPile);
        var type=mod.GetType("DevaClan.RebirthSelection");
        Func<object> create=()=>Activator.CreateInstance(type,Fields,null,new object[]{cm},null);
        var selection=create();
        var config=(DeckScreen.Params)InstanceCall(selection,"CreateParams","test-title");
        Check((bool)Property(selection,"HasCandidates"),"rebirth can play with a dead unit in exhaust");
        Check(config.showCancel && config.ignoreDefaultFilters && config.allowSelectChampion,"rebirth permits cancel and champions without upgrade filters");
        Check(!config.filterFunc(dead),"native filter must keep the defeated unit");
        Check(config.filterFunc(unplayed),"same CardData ID does not imply that copy died");
        Check(config.filterFunc(spell) && config.filterFunc(offPile) && config.filterFunc(null),"exclude spells, cards outside exhaust, and null");
        var deck=Inert<DeckScreen>();
        Set(deck,"mode",config.mode); Set(deck,"targetMode",config.targetMode);
        Set(deck,"cardTypeFilter",config.cardTypeFilter); Set(deck,"filterFunc",config.filterFunc); Set(deck,"cardManager",cm);
        var collected=(List<CardState>)InstanceCall(deck,"CollectCardsForSelection");
        Check(collected.Count==1 && ReferenceEquals(collected[0],dead),"real DeckScreen collects precisely the eligible instance");
        Check(pile.Count==3,"native selector leaves original pile intact");
        Set(deck,"cardStateChosenDelegates",new List<DeckScreen.CardStateChosenDelegate>());
        int closes=0;
        InstanceCall(selection,"Bind",deck,(Action)(()=>closes++));
        foreach(var callback in (List<DeckScreen.CardStateChosenDelegate>)Get(deck,"cardStateChosenDelegates")) callback(dead);
        Check((bool)Property(selection,"Completed") && ReferenceEquals(Property(selection,"Selected"),dead) && closes==1,"choose callback ends wait and closes screen");
        InstanceCall(selection,"Cancel");
        Check(ReferenceEquals(Property(selection,"Selected"),dead),"closing after choice preserves chosen card");
        var cancelled=create(); InstanceCall(cancelled,"Bind",deck,(Action)(()=>closes++));
        ((Action)Get(deck,"deckScreenCancelledCallback"))();
        Check((bool)Property(cancelled,"Completed") && Property(cancelled,"Selected")==null && closes==1,"cancel ends wait without double-closing native screen");
        var nullChoice=create(); InstanceCall(nullChoice,"Choose",new object[]{null});
        Check((bool)Property(nullChoice,"Completed") && Property(nullChoice,"Selected")==null,"null choice safely ends selection");
        pile.Remove(dead);
        Check(!(bool)Property(create(),"HasCandidates"),"no fallback to unrelated exhausted units when defeated unit left pile");
        var stale=create(); InstanceCall(stale,"Choose",dead);
        Check(Property(stale,"Selected")==null,"stale choice cannot restore a card that left exhaust");
        Call("BattleMemory","BeginPreview"); Dead().Add(unplayed); Call("BattleMemory","EndPreview");
        Check(!Dead().Contains(unplayed) && Dead().Contains(dead),"preview deaths do not leak to actual resurrection eligibility");
        Call("BattleMemory","Clear");
        Check(Dead().Count==0,"next battle resets death records");

        var ability=Card("ability"); Set(ability,"isUnitAbility",true);
        var roomAbility=Card("room-ability"); Set(roomAbility,"isRoomAbility",true);
        var actor=Unit(Card("actor"));
        Func<CardState,CharacterState,bool,IEnumerator> bowl=(a,u,has)=>(IEnumerator)Call("SingingBowl","AfterAbility",a,u,has);
        Check(!bowl(ability,actor,false).MoveNext(),"bowl requires owning relic");
        Check(!bowl(spell,actor,true).MoveNext(),"ordinary spells do not trigger bowl");
        Check(!bowl(roomAbility,actor,true).MoveNext() && !bowl(ability,null,true).MoveNext(),"room abilities have no unit recipient");
        Check(!bowl(ability,Unit(Card("enemy"),0,Team.Type.Heroes),true).MoveNext(),"enemy ability does not trigger bowl");
        Check(!bowl(ability,Unit(Card("dead"),0,Team.Type.Monsters,0),true).MoveNext(),"dead ability user is not revived by growth");
        var removed=Unit(Card("removed")); Set(removed,"destroyedState",CharacterState.DestroyedState.Destroyed);
        Check(!bowl(ability,removed,true).MoveNext(),"removed ability user is not upgraded");
        var growth=bowl(ability,actor,true);
        Check(growth.MoveNext(),"living friendly ability user receives one upgrade");
        var nativeUpgrade=(IEnumerator)growth.Current;
        var upgrade=(CardUpgradeState)Get(nativeUpgrade,"cardUpgradeState");
        Check(upgrade.GetAttackDamage()==3 && upgrade.GetAdditionalHP()==3,"bowl issues exactly +3 attack and +3 health");
        Check(ReferenceEquals(Get(nativeUpgrade,"<>4__this"),actor),"native upgrade targets only the ability user");
        Check(!growth.MoveNext(),"one ability issues exactly one bowl upgrade");
    }
    static void Drain(IEnumerator iterator) { while(iterator.MoveNext())if(iterator.Current is IEnumerator nested)Drain(nested); }
    sealed class Probe:IEnumerator
    { public int Steps; public object Current=>null; public bool MoveNext(){Steps++;return false;} public void Reset(){} }
}
public class ManagerProxy:DispatchProxy
{
    public SaveManager Save;public int OtherCalls;
    public PlayerManager Player;public CombatManager Combat;
    protected override object Invoke(MethodInfo method,object[] args)
    {
        if(method.Name=="GetSaveManager")return Save;
        if(method.Name=="GetPlayerManager")return Player;
        if(method.Name=="GetCombatManager")return Combat;
        if(method.Name=="GetPopupNotificationManager")return null;
        OtherCalls++;throw new Exception("Unexpected manager: "+method.Name);
    }
}
