using System.Text.Json;

namespace StsOverlay;

public sealed class Metrics
{
    public long Damage { get; set; }
    public long AreaDamage { get; set; }
    public long BlockGenerated { get; set; }
    public long BlockUsed { get; set; }
    public long DamageBlocked { get; set; }
    public long DamageTaken { get; set; }
    public long SelfDamage { get; set; }
    public long Healing { get; set; }
    public long AllyHealing { get; set; }
    public long HealingReceived { get; set; }
    public long Uses { get; set; }
    public long Draws { get; set; }
    public long Energy { get; set; }
    public long Stars { get; set; }
    public long PowersApplied { get; set; }

    public void Add(Metrics other)
    {
        Damage += other.Damage; AreaDamage += other.AreaDamage;
        BlockGenerated += other.BlockGenerated; BlockUsed += other.BlockUsed;
        DamageBlocked += other.DamageBlocked; DamageTaken += other.DamageTaken;
        SelfDamage += other.SelfDamage; Healing += other.Healing;
        AllyHealing += other.AllyHealing; HealingReceived += other.HealingReceived;
        Uses += other.Uses; Draws += other.Draws; Energy += other.Energy;
        Stars += other.Stars; PowersApplied += other.PowersApplied;
    }
}

public sealed class PlayerStats
{
    public Metrics Total { get; set; } = new();
    public Dictionary<string, Metrics> Cards { get; set; } = new();
}

public sealed class FightStats
{
    public double Seconds { get; set; }
    public int Turns { get; set; }
    public bool Complete { get; set; }
    public long UnattributedDamage { get; set; }
    public Dictionary<string, PlayerStats> Players { get; set; } = new();
}

public sealed class RunStats
{
    public int Version { get; set; } = 1;
    public string Key { get; set; } = "";
    public Dictionary<string, string> Names { get; set; } = new();
    public Dictionary<string, string> CardNames { get; set; } = new();
    public Dictionary<string, FightStats> Fights { get; set; } = new();
}

// A shared block pool has no canonical per-card owner. This ledger uses FIFO.
public sealed class BlockLedger
{
    private sealed class Credit(string? player, string? card, long amount)
    {
        public string? Player = player;
        public string? Card = card;
        public long Amount = amount;
    }
    private readonly List<Credit> credits = new();
    public long Remaining => credits.Sum(c => c.Amount);

    public void Sync(long actual)
    {
        long remove = Remaining - Math.Max(0, actual);
        if (remove > 0) Consume(remove, (_, _, _) => { });
        else if (remove < 0) credits.Add(new(null, null, -remove));
    }

    public void Add(string? player, string? card, long amount)
    {
        if (amount > 0) credits.Add(new(player, card, amount));
    }

    public void Consume(long amount, Action<string?, string?, long> record)
    {
        while (amount > 0 && credits.Count > 0)
        {
            var credit = credits[0];
            long used = Math.Min(amount, credit.Amount);
            record(credit.Player, credit.Card, used);
            amount -= used; credit.Amount -= used;
            if (credit.Amount == 0) credits.RemoveAt(0);
        }
    }
}

public sealed class StatsEngine
{
    public RunStats Run { get; private set; } = new();
    public string? CurrentFightKey { get; private set; }
    public FightStats? Current => CurrentFightKey is { } key ? Run.Fights.GetValueOrDefault(key) : null;
    public bool Active { get; private set; }
    private readonly Dictionary<string, BlockLedger> block = new();

    public void Load(RunStats run)
    {
        if (run.Version != 1 || run.Fights == null || run.Names == null || run.CardNames == null)
            throw new InvalidDataException("Unsupported statistics file.");
        Run = run; CurrentFightKey = null; Active = false; block.Clear();
    }

    public void Begin(string runKey, string fightKey, IEnumerable<KeyValuePair<string, string>> players)
    {
        if (Run.Key != runKey) Run = new RunStats { Key = runKey };
        CurrentFightKey = fightKey;
        // Reloading/replaying a fight replaces its previous contribution, never adds it twice.
        Run.Fights[fightKey] = new();
        block.Clear(); Active = true;
        foreach (var (id, name) in players)
        {
            Run.Names[id] = name;
            Current!.Players[id] = new();
        }
    }

    public void End()
    {
        if (Current != null) Current.Complete = true;
        Active = false;
    }

    public void Tick(double seconds, int round)
    {
        if (!Active || Current == null) return;
        if (double.IsFinite(seconds) && seconds > 0) Current.Seconds += seconds;
        Current.Turns = Math.Max(Current.Turns, round);
    }

    public void Record(string? player, string? card, Action<Metrics> change)
    {
        if (!Active || Current == null || player == null) return;
        if (!Current.Players.TryGetValue(player, out var stats)) Current.Players[player] = stats = new();
        change(stats.Total);
        if (card == null) return;
        if (!stats.Cards.TryGetValue(card, out var metric)) stats.Cards[card] = metric = new();
        change(metric);
    }

    public void Damage(string? source, string? card, string? receiver, bool targetIsEnemy,
        long hpLost, long blocked, bool area)
    {
        if (!Active || Current == null) return;
        hpLost = Math.Max(0, hpLost); blocked = Math.Max(0, blocked);
        if (targetIsEnemy)
        {
            if (source == null) Current.UnattributedDamage += hpLost;
            else Record(source, card, m => { m.Damage += hpLost; if (area) m.AreaDamage += hpLost; });
        }
        if (receiver != null)
            Record(receiver, null, m => { m.DamageTaken += hpLost; m.DamageBlocked += blocked;
                if (source == receiver) m.SelfDamage += hpLost; });
    }

    public void GainBlock(string receiver, string? source, string? card, long before, long gained)
    {
        if (!Active) return;
        var pool = Pool(receiver); pool.Sync(before);
        pool.Add(source, card, Math.Max(0, gained));
        Record(source, card, m => m.BlockGenerated += Math.Max(0, gained));
    }

    public void UseBlock(string receiver, long before, long used)
    {
        if (!Active) return;
        var pool = Pool(receiver); pool.Sync(before);
        pool.Consume(Math.Max(0, used), (source, card, amount) => Record(source, card, m => m.BlockUsed += amount));
    }

    public void SyncBlock(string receiver, long actual) => Pool(receiver).Sync(actual);
    private BlockLedger Pool(string receiver)
    {
        if (!block.TryGetValue(receiver, out var pool)) block[receiver] = pool = new();
        return pool;
    }

    public void Heal(string? source, string? card, string receiver, long actual)
    {
        actual = Math.Max(0, actual);
        Record(receiver, null, m => m.HealingReceived += actual);
        Record(source, card, m => { m.Healing += actual; if (source != receiver) m.AllyHealing += actual; });
    }

    public FightStats View(bool overall)
    {
        if (!overall) return Current ?? new();
        var result = new FightStats();
        foreach (var fight in Run.Fights.Values)
        {
            result.Seconds += fight.Seconds; result.Turns += fight.Turns;
            result.UnattributedDamage += fight.UnattributedDamage;
            foreach (var (id, stats) in fight.Players)
            {
                if (!result.Players.TryGetValue(id, out var player)) result.Players[id] = player = new();
                player.Total.Add(stats.Total);
                foreach (var (card, metric) in stats.Cards)
                {
                    if (!player.Cards.TryGetValue(card, out var sum)) player.Cards[card] = sum = new();
                    sum.Add(metric);
                }
            }
        }
        return result;
    }

    public static double NextDraw(int copies, int remaining)
    {
        if (remaining < 0 || copies < 0 || copies > remaining) throw new ArgumentOutOfRangeException(nameof(copies));
        return remaining == 0 ? 0 : (double)copies / remaining;
    }

    public static double NextHand(int copies, int remaining, int draws)
    {
        if (remaining < 0 || copies < 0 || copies > remaining || draws < 0)
            throw new ArgumentOutOfRangeException(nameof(copies));
        if (copies == 0 || draws == 0) return 0;
        if (draws >= remaining) return 1;
        double miss = 1;
        for (int i = 0; i < draws; i++)
            miss *= (double)Math.Max(0, remaining - copies - i) / (remaining - i);
        return 1 - miss;
    }

    public string ToJson() => JsonSerializer.Serialize(Run);
    public void FromJson(string json) => Load(JsonSerializer.Deserialize<RunStats>(json)
        ?? throw new InvalidDataException("Empty statistics file."));
}
