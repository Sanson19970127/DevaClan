using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DevaClan
{
    internal static class ChargeAbilities
    {
        internal static bool Affordable(int cooldown, int charge) => cooldown > 0 && charge >= cooldown;
        internal static bool CanPay(int cooldown, ICoreGameManagers g) => g != null && Plugin.Charge != null
            && Affordable(cooldown, Plugin.Charge.GetCurrentValue(g.GetSaveManager().PreviewMode));
        internal static bool CanPayCurrent(int cooldown) => Plugin.Charge != null
            && Affordable(cooldown, Plugin.Charge.GetCurrentValue());
        internal static TrainRoomAttachmentState FindRoomAbility(RoomState room, CardState ability, bool preview)
        {
            if (room == null || ability == null) return null;
            var attachments = preview ? room.PreviewAttachments : room.Attachments;
            return attachments.FirstOrDefault(a => a.IsActive && a.TrainSide == Team.Type.Monsters
                && a.UpgradeStates.Any(u => u.Item2.GetRoomAbilityUpgrade()?.GetID() == ability.GetCardDataID()));
        }
    }

    [HarmonyPatch(typeof(CharacterState), nameof(CharacterState.GetUnitAbilityAvailability))]
    static class ChargeAvailability
    {
        static void Postfix(CharacterState __instance, CombatManager combatManager, AllGameManagers ___allGameManagers, ref CharacterState.UnitAbilityAvailability __result)
        {
            if (__result != CharacterState.UnitAbilityAvailability.Cooldown || combatManager == null || combatManager.IsPlacementPhase) return;
            var g = ___allGameManagers.GetCoreManagers();
            if (__instance.GetTeamType() != Team.Type.Monsters || !ChargeAbilities.CanPay(__instance.GetStatusEffectStacks("cooldown"), g)) return;
            if (!g.GetCardManager().GetBelowHandSize() && !__instance.GetUnitAbilityCardState().CanBePlayedWhenHandFull()) return;
            __result = CharacterState.UnitAbilityAvailability.CanActivate;
        }
    }

    [HarmonyPatch(typeof(RoomAbilitySelectionBehaviour), "GetCurrentAbilityAvailability")]
    static class RoomChargeAvailability
    {
        static void Prefix(TrainRoomAttachmentState ___selectedRoomAttachmentState, ref CharacterState.UnitAbilityAvailability __result)
        {
            var attachment = ___selectedRoomAttachmentState;
            if (attachment != null && attachment.IsActive && attachment.TrainSide == Team.Type.Monsters
                && attachment.CurrentAbilityCooldown > 0 && ChargeAbilities.CanPayCurrent(attachment.CurrentAbilityCooldown))
                __result = CharacterState.UnitAbilityAvailability.CanActivate;
        }
        static void Postfix(TrainRoomAttachmentState ___selectedRoomAttachmentState, ref CharacterState.UnitAbilityAvailability __result)
        {
            if (__result != CharacterState.UnitAbilityAvailability.Cooldown || ___selectedRoomAttachmentState == null
                || ___selectedRoomAttachmentState.TrainSide != Team.Type.Monsters || !___selectedRoomAttachmentState.IsActive) return;
            var g = AllGameManagers.Instance?.GetCoreManagers();
            if (ChargeAbilities.CanPay(___selectedRoomAttachmentState.CurrentAbilityCooldown, g))
                __result = CharacterState.UnitAbilityAvailability.CanActivate;
        }
    }

    // Keep input eligibility separate from presentation. Carry the remaining
    // cooldown into the visual state even when Charge makes the ability usable.
    [HarmonyPatch(typeof(UnitAbilityUIState), nameof(UnitAbilityUIState.Create), new[] { typeof(CharacterState), typeof(CombatManager) })]
    static class ChargeAbilityDisplayState
    {
        static void Postfix(CharacterState characterState, ref UnitAbilityUIState __result)
        {
            if (__result.abilityAvailability == CharacterState.UnitAbilityAvailability.CanActivate && characterState != null)
            {
                int cooldown = characterState.GetStatusEffectStacks("cooldown");
                if (cooldown > 0) __result = UnitAbilityUIState.Create(__result.abilityAvailability, true, cooldown, false);
            }
        }
    }

    [HarmonyPatch(typeof(UnitAbilityUI), nameof(UnitAbilityUI.Set))]
    static class ChargeAbilityVisuals
    {
        static readonly Dictionary<Graphic, Color> OriginalColors = new Dictionary<Graphic, Color>();
        static readonly Color ChargeReadyTint = new Color(0.25f, 0.85f, 1f, 1f);
        internal static bool UsesChargeVisual(UnitAbilityUIState state) =>
            state.abilityAvailability == CharacterState.UnitAbilityAvailability.CanActivate && state.hasCooldown && state.cooldown > 0 && !state.isSilenced;
        static void Prefix(ref UnitAbilityUIState state, out bool __state)
        {
            __state = UsesChargeVisual(state);
            if (__state) state = UnitAbilityUIState.Create(CharacterState.UnitAbilityAvailability.Cooldown, true, state.cooldown, false);
        }
        static void Postfix(Transform ___unitAbilityInactive, bool __state)
        {
            if (___unitAbilityInactive == null) return;
            foreach (var dead in OriginalColors.Keys.Where(g => g == null).ToArray()) OriginalColors.Remove(dead);
            // Tint the cooldown artwork, leaving the native number readable.
            foreach (var graphic in ___unitAbilityInactive.GetComponentsInChildren<Image>(true))
            {
                if (!OriginalColors.TryGetValue(graphic, out var original)) OriginalColors.Add(graphic, original = graphic.color);
                graphic.color = __state ? Color.Lerp(original, ChargeReadyTint, 0.62f) : original;
            }
        }
    }

    [HarmonyPatch(typeof(TrainRoomAttachmentDisplay), nameof(TrainRoomAttachmentDisplay.RefreshDisplay))]
    static class RoomChargeVisuals
    {
        static void Postfix(TrainRoomAttachmentDisplay __instance, Image ___previewIcon)
        {
            if (SaveManager.IsInUndoModeStatic || (___previewIcon != null && ___previewIcon.gameObject.activeSelf)) return;
            var attachment = __instance.GetAttachmentState();
            var g = AllGameManagers.Instance?.GetCoreManagers();
            if (attachment == null || !attachment.HasRoomAbility || !attachment.IsActive || attachment.TrainSide != Team.Type.Monsters
                || g == null || g.GetSaveManager().PreviewMode || g.GetCombatManager().IsPlacementPhase) return;
            if (ChargeAbilities.CanPay(attachment.CurrentAbilityCooldown, g))
                __instance.AbilityUi?.Set(UnitAbilityUIState.Create(CharacterState.UnitAbilityAvailability.CanActivate, true, attachment.CurrentAbilityCooldown, false));
        }
    }

    [HarmonyPatch(typeof(CardManager), nameof(CardManager.PlayAnyCard))]
    static class ChargePayment
    {
        static bool Prefix(CardState cardState, CharacterState characterThatActivatedAbility, RoomState roomThatActivatedAbility,
            AllGameManagers ___allGameManagers, ref bool __result, out int __state, ref CommonSelectionBehavior.SelectionError lastFailedEffectError)
        {
            var g = ___allGameManagers.GetCoreManagers();
            __state = characterThatActivatedAbility != null && characterThatActivatedAbility.GetTeamType() == Team.Type.Monsters
                ? characterThatActivatedAbility.GetStatusEffectStacks("cooldown")
                : ChargeAbilities.FindRoomAbility(roomThatActivatedAbility, cardState, g.GetSaveManager().PreviewMode)?.CurrentAbilityCooldown ?? 0;
            if (__state <= 0 || ChargeAbilities.CanPay(__state, g)) return true;
            lastFailedEffectError = CommonSelectionBehavior.SelectionError.Cooldown;
            __result = false;
            return false;
        }
        // Only accepted plays pay. Vanilla still owns the new cooldown, including
        // room abilities; cancellation and invalid targets never enter this path.
        static void Postfix(bool __result, CardState cardState, int __state, AllGameManagers ___allGameManagers)
        {
            if (__result && __state > 0) Plugin.Gain(___allGameManagers.GetCoreManagers(), -__state, cardState);
        }
    }
}
