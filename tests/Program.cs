using StsOverlay;

int passed = 0;
void Check(string name, Action test)
{
    test(); passed++; Console.WriteLine($"PASS {name}");
}
void Equal<T>(T expected, T actual) where T : IEquatable<T>
{
    if (!expected.Equals(actual)) throw new Exception($"Expected {expected}, got {actual}");
}
StatsEngine New()
{
    var stats = new StatsEngine();
    stats.Begin("run-1", "fight-1", new Dictionary<string, string> { ["a"] = "Alice", ["b"] = "Bob" });
    return stats;
}

Check("Duplicates double next-draw probability without reading order", () => {
    Equal(.1, StatsEngine.NextDraw(1, 10)); Equal(.2, StatsEngine.NextDraw(2, 10));
    Equal(0.0, StatsEngine.NextDraw(0, 0)); Equal(1.0, StatsEngine.NextDraw(3, 3));
    try { StatsEngine.NextDraw(3, 2); throw new Exception("Accepted invalid counts"); }
    catch (ArgumentOutOfRangeException) { }
});
Check("Actual damage and AoE are counted once per target", () => {
    var s = New(); s.Damage("a", "cleave", null, true, 6, 4, true);
    s.Damage("a", "cleave", null, true, 2, 0, true);
    Equal(8L, s.Current!.Players["a"].Total.Damage);
    Equal(8L, s.Current.Players["a"].Cards["cleave"].AreaDamage);
});
Check("Generated block differs from damage absorbed", () => {
    var s = New(); s.GainBlock("b", "a", "defend-ally", 0, 15);
    s.UseBlock("b", 15, 8); s.Damage(null, null, "b", false, 0, 8, false);
    Equal(15L, s.Current!.Players["a"].Total.BlockGenerated);
    Equal(8L, s.Current.Players["a"].Cards["defend-ally"].BlockUsed);
    Equal(8L, s.Current.Players["b"].Total.DamageBlocked);
});
Check("FIFO preserves unknown block and discards expired block", () => {
    var s = New(); s.GainBlock("a", "a", "defend", 4, 5);
    s.UseBlock("a", 9, 6); Equal(2L, s.Current!.Players["a"].Cards["defend"].BlockUsed);
    s.SyncBlock("a", 0); s.GainBlock("a", "a", "new-defend", 0, 3); s.UseBlock("a", 3, 3);
    Equal(2L, s.Current.Players["a"].Cards["defend"].BlockUsed);
    Equal(3L, s.Current.Players["a"].Cards["new-defend"].BlockUsed);
});
Check("Healing counts restored HP and separates allies from self", () => {
    var s = New(); s.Heal("a", "heal", "b", 3); s.Heal("a", "heal", "a", 2); s.Heal("a", "heal", "b", 0);
    Equal(5L, s.Current!.Players["a"].Cards["heal"].Healing);
    Equal(3L, s.Current.Players["a"].Total.AllyHealing);
    Equal(3L, s.Current.Players["b"].Total.HealingReceived);
});
Check("HP loss, blocked hits and self damage stay separate", () => {
    var s = New(); s.Damage(null, null, "a", false, 5, 8, false);
    s.Damage("a", "offering", "a", false, 6, 0, false);
    Equal(11L, s.Current!.Players["a"].Total.DamageTaken);
    Equal(8L, s.Current.Players["a"].Total.DamageBlocked);
    Equal(6L, s.Current.Players["a"].Total.SelfDamage);
    Equal(0L, s.Current.Players["a"].Total.Damage);
});
Check("Run aggregates combat time and turns; replay replaces a fight", () => {
    var s = New(); s.Damage("a", "strike", null, true, 10, 0, false); s.Tick(20, 2); s.End();
    s.Begin("run-1", "fight-2", s.Run.Names.ToArray()); s.Damage("a", "strike", null, true, 5, 0, false); s.Tick(10, 1);
    Equal(15L, s.View(true).Players["a"].Total.Damage); Equal(30.0, s.View(true).Seconds); Equal(3, s.View(true).Turns);
    s.Begin("run-1", "fight-2", s.Run.Names.ToArray()); s.Damage("a", "strike", null, true, 2, 0, false);
    Equal(12L, s.View(true).Players["a"].Total.Damage);
});
Check("Saved statistics round trip without cross-run leakage", () => {
    var s = New(); s.Damage("b", "strike", null, true, 7, 0, false);
    var restored = new StatsEngine(); restored.FromJson(s.ToJson());
    Equal(7L, restored.View(true).Players["b"].Total.Damage);
    restored.Begin("run-2", "fight-1", new Dictionary<string,string> { ["a"] = "Alice" });
    Equal(0L, restored.View(true).Players["a"].Total.Damage);
});
Check("Unknown sources are visible and never assigned to another player", () => {
    var s = New(); s.Damage(null, null, null, true, 9, 0, false);
    Equal(9L, s.Current!.UnattributedDamage); Equal(0L, s.Current.Players["a"].Total.Damage);
});
Check("Ended fights ignore further events and time", () => {
    var s = New(); s.End(); s.Damage("a", "strike", null, true, 9, 0, false); s.Tick(4, 2);
    Equal(0L, s.Current!.Players["a"].Total.Damage); Equal(0.0, s.Current.Seconds);
});
Console.WriteLine($"{passed} checks passed.");
