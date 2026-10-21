using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;

namespace StsOverlay;

internal sealed record EffectSource(string? Player, string? Card, bool Area = false);

internal static class GameTracker
{
    internal static readonly StatsEngine Stats = new();
    internal static readonly AsyncLocal<EffectSource?> Ambient = new();
    internal static readonly AsyncLocal<Dictionary<Creature, EffectSource>?> DoomTargets = new();
    private static readonly Dictionary<PowerModel, EffectSource?> powerSources = new();
    private static readonly HashSet<string> warnings = new();
    private static readonly Stopwatch clock = Stopwatch.StartNew();
    private static double lastTick;
    private static double lastSave;
    private static IRunState? run;
    private static string? savePath;
    internal static CombatState? Combat { get; private set; }

    internal static void Initialize()
    {
        var manager = CombatManager.Instance;
        manager.CombatSetUp += state => Safe(() => Begin(state));
        manager.CombatEnded += _ => Safe(End);
        manager.TurnStarted += state => Safe(() => Stats.Tick(0, state.RoundNumber));
    }

    internal static void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            string message = $"{ex.GetType().Name}: {ex.Message}";
            if (warnings.Add(message)) GD.PushWarning($"[Spire Stats] {message}");
        }
    }

    private static void Begin(CombatState state)
    {
        Flush(); Combat = state; powerSources.Clear();
        string key = $"{state.RunState.Rng.StringSeed}:{AccessTools.Field(typeof(RunManager), "_startTime").GetValue(RunManager.Instance)}";
        if (!ReferenceEquals(run, state.RunState))
        {
            run = state.RunState;
            string directory = Path.Combine(OS.GetUserDataDir(), "spire-stats");
            Directory.CreateDirectory(directory);
            savePath = Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".json");
            if (File.Exists(savePath)) Safe(() => Stats.FromJson(File.ReadAllText(savePath)));
        }
        string fight = $"{run.CurrentActIndex}:{run.TotalFloor}:{run.CurrentRoom?.Id}";
        Stats.Begin(key, fight, state.Players.Select(p => KeyValuePair.Create(Id(p), PlayerName(p))));
        lastTick = clock.Elapsed.TotalSeconds;
        GD.Print($"[Spire Stats] Combat tracking started: {fight}, {state.Players.Count} players.");
        Flush();
    }

    private static void End()
    {
        Tick(); Stats.End(); Flush(); Combat = null; powerSources.Clear();
    }

    internal static void Tick()
    {
        double now = clock.Elapsed.TotalSeconds;
        double delta = now - lastTick; lastTick = now;
        if (Combat != null && CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsPaused)
            Stats.Tick(delta, Combat.RoundNumber);
        if (now - lastSave > 5) { Flush(); lastSave = now; }
    }

    internal static void Flush()
    {
        if (savePath == null || Stats.Run.Key.Length == 0) return;
        Safe(() => {
            string temp = savePath + ".tmp";
            File.WriteAllText(temp, Stats.ToJson()); File.Move(temp, savePath, true);
        });
    }

    internal static string Id(Player player) => player.NetId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    internal static Player? Owner(Creature? creature) => creature?.Player ?? creature?.PetOwner;
    internal static string? Id(Creature? creature) => Owner(creature) is { } p ? Id(p) : null;
    internal static string CreatureKey(Creature c) => c.IsPlayer ? "player:" + Id(c) : "creature:" + c.CombatId;
    internal static string CardKey(CardModel card) => card.Id.Entry;

    internal static EffectSource CardSource(CardModel card)
    {
        string key = CardKey(card);
        Stats.Run.CardNames[key] = card.Title;
        return new(Id(card.Owner), key, card.TargetType == TargetType.AllEnemies);
    }

    internal static string PlayerName(Player player)
    {
        try { return PlatformUtil.GetPlayerName(RunManager.Instance.NetService.Platform, player.NetId); }
        catch { return player.Character.Id.Entry; }
    }

    internal static EffectSource PowerSource(PowerModel power)
    {
        if (powerSources.TryGetValue(power, out var tracked)) return tracked ?? new(null, null);
        return new(Id(power.Applier ?? (power.Owner.IsEnemy ? null : power.Owner)), null);
    }

    internal static void History(ICombatState state, CombatHistoryEntry entry)
    {
        if (!ReferenceEquals(state, Combat) || !state.IsLiveCombat()) return;
        Stats.Tick(0, state.RoundNumber);
        switch (entry)
        {
            case CardPlayStartedEntry started:
                var play = started.CardPlay; var source = CardSource(play.Card);
                Stats.Record(source.Player, source.Card, m => {
                    m.Uses++;
                    if (play.IsFirstInSeries) { m.Energy += play.Resources.EnergySpent; m.Stars += play.Resources.StarsSpent; }
                });
                break;
            case CardDrawnEntry drawn:
                Stats.Run.CardNames[CardKey(drawn.Card)] = drawn.Card.Title;
                Stats.Record(Id(drawn.Card.Owner), CardKey(drawn.Card), m => m.Draws++);
                break;
            case DamageReceivedEntry damage:
                var card = damage.CardSource;
                var context = Ambient.Value;
                // An explicit dealer/card wins over ambient context (e.g. retaliation during a card play).
                string? dealer = Id(damage.Dealer);
                EffectSource? origin = card != null ? CardSource(card)
                    : dealer != null ? new(dealer, null, context?.Player == dealer && context.Area)
                    : damage.Dealer != null ? null : context;
                Stats.Damage(origin?.Player, origin?.Card, damage.Receiver.IsPlayer ? Id(damage.Receiver) : null,
                    damage.Receiver.IsEnemy, damage.Result.UnblockedDamage, damage.Result.BlockedDamage,
                    origin?.Area == true || (card != null && context != null && context.Card == CardKey(card) && context.Area));
                break;
            case PowerReceivedEntry power:
                if (power.Amount <= 0) break;
                var applier = Id(power.Applier);
                var ambient = Ambient.Value;
                var applied = new EffectSource(applier, ambient != null && ambient.Player == applier ? ambient.Card : null);
                if (powerSources.TryGetValue(power.Power, out var previous))
                    powerSources[power.Power] = previous?.Player != null && previous.Player == applied.Player
                        ? previous with { Card = previous.Card == applied.Card ? previous.Card : null }
                        : null;
                else powerSources[power.Power] = applied;
                Stats.Record(applier, applied.Card, m => m.PowersApplied += (long)power.Amount);
                break;
        }
    }

    internal static void BlockGained(Creature creature, int before)
    {
        if (!ReferenceEquals(creature.CombatState, Combat) || creature.IsEnemy) return;
        var source = Ambient.Value;
        Stats.GainBlock(CreatureKey(creature), source == null ? Id(creature) : source.Player, source?.Card,
            before, Math.Max(0, creature.Block - before));
    }

    internal static void BlockUsed(Creature creature, int before, decimal used)
    {
        if (!ReferenceEquals(creature.CombatState, Combat) || creature.IsEnemy) return;
        Stats.UseBlock(CreatureKey(creature), before, (long)used);
    }

    internal static void Healed(Creature creature, int before)
    {
        if (!ReferenceEquals(creature.CombatState, Combat) || creature.IsEnemy) return;
        var source = Ambient.Value;
        // Effects without a known source remain received healing; do not invent a healer.
        Stats.Heal(source?.Player, source?.Card, Id(creature) ?? CreatureKey(creature),
            Math.Max(0, creature.CurrentHp - before));
    }

    internal static void DoomHpLost(Creature creature, DamageResult result)
    {
        // Doom bypasses damage history. Observe only its actual HP-removal call, once per target.
        if (DoomTargets.Value is not { } targets || !targets.Remove(creature, out var source)
            || !ReferenceEquals(creature.CombatState, Combat)) return;
        Stats.Damage(source.Player, source.Card, creature.IsPlayer ? Id(creature) : null,
            creature.IsEnemy, result.UnblockedDamage, 0, source.Area);
    }
}
