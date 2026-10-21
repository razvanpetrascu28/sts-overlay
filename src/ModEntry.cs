using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace StsOverlay;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public static void Initialize()
    {
        var harmony = new Harmony("razvan.spire.stats");
        harmony.PatchAll(typeof(ModEntry).Assembly);
        GameTracker.Initialize();
        GD.Print("[Spire Stats] Initialized. Informational only; no gameplay or network changes.");
        if (NGame.Instance != null) Callable.From(() => StatsOverlay.Attach(NGame.Instance)).CallDeferred();
#if UI_SMOKE
        UiSmoke.Schedule();
#endif
    }
}

[HarmonyPatch(typeof(CombatHistory), "Add")]
internal static class HistoryPatch
{
    private static void Postfix(ICombatState combatState, MegaCrit.Sts2.Core.Combat.History.CombatHistoryEntry entry)
        => GameTracker.Safe(() => GameTracker.History(combatState, entry));
}

// Enter before the async method captures ExecutionContext, then restore the caller's context.
[HarmonyPatch]
internal static class EffectScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(CardModel), nameof(CardModel.OnPlayWrapper));
        yield return AccessTools.Method(typeof(AttackCommand), nameof(AttackCommand.Execute));
        yield return AccessTools.Method(typeof(PoisonPower), nameof(PoisonPower.AfterSideTurnStart));
        // Scope effects that directly provide block/healing, so support is credited to the provider.
        foreach (var type in typeof(AbstractModel).Assembly.GetTypes().Where(t => !t.IsAbstract &&
            (t.IsSubclassOf(typeof(PowerModel)) || t.IsSubclassOf(typeof(RelicModel)) ||
             t.IsSubclassOf(typeof(PotionModel)) || t.IsSubclassOf(typeof(OrbModel)))))
        foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            var state = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
            var moveNext = state?.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (moveNext != null && PatchProcessor.GetOriginalInstructions(moveNext).Any(i =>
                i.operand is MethodBase called && called.DeclaringType == typeof(CreatureCmd) &&
                called.Name is nameof(CreatureCmd.GainBlock) or nameof(CreatureCmd.Heal)))
                yield return method;
        }
    }

    private static void Prefix(object __instance, out EffectSource? __state)
    {
        var previous = GameTracker.Ambient.Value;
        __state = previous;
        GameTracker.Safe(() => {
            GameTracker.Ambient.Value = __instance switch {
                CardModel card => GameTracker.CardSource(card),
                AttackCommand attack => attack.ModelSource is CardModel card
                    ? GameTracker.CardSource(card) with { Area = attack.IsMultiTargeted && !attack.IsRandomlyTargeted }
                    : new(GameTracker.Id(attack.Attacker), null, attack.IsMultiTargeted && !attack.IsRandomlyTargeted),
                PowerModel power => GameTracker.PowerSource(power),
                RelicModel relic => new(GameTracker.Id(relic.Owner), null),
                PotionModel potion => new(GameTracker.Id(potion.Owner), null),
                OrbModel orb => new(GameTracker.Id(orb.Owner), null),
                _ => previous
            };
        });
    }

    private static Exception? Finalizer(EffectSource? __state, Exception? __exception)
    {
        GameTracker.Ambient.Value = __state;
        return __exception;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.GainBlockInternal))]
internal static class GainBlockPatch
{
    private static void Prefix(Creature __instance, out int __state) => __state = __instance.Block;
    private static void Postfix(Creature __instance, int __state) => GameTracker.Safe(() => GameTracker.BlockGained(__instance, __state));
}

[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
internal static class DamageBlockPatch
{
    private static void Prefix(Creature __instance, out int __state) => __state = __instance.Block;
    private static void Postfix(Creature __instance, int __state, decimal __result)
        => GameTracker.Safe(() => GameTracker.BlockUsed(__instance, __state, __result));
}

[HarmonyPatch(typeof(Creature), nameof(Creature.HealInternal))]
internal static class HealPatch
{
    private static void Prefix(Creature __instance, out int __state) => __state = __instance.CurrentHp;
    private static void Postfix(Creature __instance, int __state) => GameTracker.Safe(() => GameTracker.Healed(__instance, __state));
}

[HarmonyPatch(typeof(DoomPower), nameof(DoomPower.DoomKill))]
internal static class DoomScopePatch
{
    private static void Prefix(IReadOnlyList<Creature> creatures, out Dictionary<Creature, EffectSource>? __state)
    {
        __state = GameTracker.DoomTargets.Value;
        GameTracker.Safe(() => GameTracker.DoomTargets.Value = creatures.ToDictionary(c => c,
            c => GameTracker.Ambient.Value ?? (c.GetPower<DoomPower>() is { } power
                ? GameTracker.PowerSource(power) : new EffectSource(null, null))));
    }
    private static Exception? Finalizer(Dictionary<Creature, EffectSource>? __state, Exception? __exception)
    {
        GameTracker.DoomTargets.Value = __state; return __exception;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
internal static class DoomDamagePatch
{
    private static void Postfix(Creature __instance, DamageResult __result)
        => GameTracker.Safe(() => GameTracker.DoomHpLost(__instance, __result));
}

[HarmonyPatch(typeof(NGame), nameof(NGame._Ready))]
internal static class GameReadyPatch
{
    private static void Postfix(NGame __instance) => GameTracker.Safe(() => StatsOverlay.Attach(__instance));
}

[HarmonyPatch(typeof(NGame), nameof(NGame._ExitTree))]
internal static class GameExitPatch
{
    private static void Prefix() => GameTracker.Flush();
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class CardBadgePatch
{
    private static void Postfix(NCard __instance) => GameTracker.Safe(() => StatsOverlay.UpdateDrawBadge(__instance));
}

[HarmonyPatch(typeof(NInspectCardScreen), "UpdateCardDisplay")]
internal static class CardInspectPatch
{
    private static void Postfix(NInspectCardScreen __instance, List<CardModel> ____cards, int ____index)
        => GameTracker.Safe(() => StatsOverlay.CardDetails(__instance, ____cards[____index]));
}
