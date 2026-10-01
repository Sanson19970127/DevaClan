using System;
using System.Collections;
using System.Linq;
using HarmonyLib;

namespace DevaClan
{
    internal static class ChampionStats
    {
        internal static int Extra(CardState card)
        {
            bool mara=Plugin.Is(card,"Mara"), nayuta=Plugin.Is(card,"Nayuta");
            if(!mara&&!nayuta) return 0;
            string[] paths=mara?new[]{"MaraCreator","MaraLiberation","MaraAsura"}:new[]{"NayutaServer","NayutaSwarm","NayutaRecreate"};
            int sum=0,already=0;
            foreach(var path in paths)
                for(int i=3;i>0;i--) if(Plugin.Upgrades.TryGetValue(path+i,out var u)&&card.HasUpgrade(u)) {sum+=i;already+=mara?20*(i-1):new[]{0,5,15}[i-1];break;}
            if(sum==0)return 0;
            int target=mara?20*(Math.Min(3,sum)-1):new[]{0,5,15}[Math.Min(3,sum)-1];
            return target-already;
        }
    }
    [HarmonyPatch(typeof(CardState),nameof(CardState.GetTotalAttackDamage))]
    static class MixedAttack { static void Postfix(CardState __instance,ref int __result) { __result+=ChampionStats.Extra(__instance); } }
    [HarmonyPatch(typeof(CardState),nameof(CardState.GetHealth))]
    static class MixedHealth { static void Postfix(CardState __instance,ref float __result) { __result+=ChampionStats.Extra(__instance); } }
    [HarmonyPatch(typeof(CharacterState),nameof(CharacterState.Setup))]
    static class MixedSpawn
    {
        static IEnumerator Postfix(IEnumerator result,CharacterState __instance)
        {
            while(result.MoveNext())yield return result.Current;
            int n=ChampionStats.Extra(__instance.GetSpawnerCard());
            if(n>0) {__instance.BuffDamage(n,null);__instance.SetHealth(__instance.GetHP()+n,__instance.GetMaxHP()+n);}
        }
    }
}
