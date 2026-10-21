using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace StsOverlay;

// Included only with -p:UiSmoke=true. No run is started or saved.
internal static class UiSmoke
{
    internal static void Schedule() => Callable.From(() => {
        var timer = new Godot.Timer { OneShot = true, WaitTime = 20, Autostart = true, IgnoreTimeScale = true };
        timer.Timeout += () => _ = Run(NGame.Instance!); NGame.Instance!.AddChild(timer);
    }).CallDeferred();

    private static IEnumerable<Node> Tree(Node root) {
        yield return root; foreach (var child in root.GetChildren()) foreach (var node in Tree(child)) yield return node;
    }
    private static async Task Frame(Node game) {
        await game.ToSignal(game.GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);
        await game.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }
    private static async Task Run(NGame game)
    {
        try
        {
            string output = CommandLineHelper.GetValue("stats-ui-output") ?? Path.Combine(Path.GetTempPath(), "SpireStatsUiSmoke");
            Directory.CreateDirectory(output);
            if (int.TryParse(CommandLineHelper.GetValue("stats-expected-unlocks"), out int expected) && SaveManager.Instance.Progress.TotalUnlocks != expected)
                throw new Exception($"Restored progression did not load: expected {expected}, got {SaveManager.Instance.Progress.TotalUnlocks} unlocks.");
            GD.Print($"[Spire Stats UI] Progress loaded: {SaveManager.Instance.Progress.TotalUnlocks} unlocks.");
            var alice = Player.CreateForNewRun<Ironclad>(UnlockState.none, 1); alice.ResetCombatState();
            var bob = Player.CreateForNewRun<Silent>(UnlockState.none, 2); bob.ResetCombatState();
            StatsOverlay.PreviewPlayers["1"] = alice; StatsOverlay.PreviewPlayers["2"] = bob;
            var engine = GameTracker.Stats;
            engine.Begin("ui-fixture", "fight", new Dictionary<string, string> { ["1"] = "Alice", ["2"] = "Bob" });
            var areaCard = ModelDb.AllCards.First(c => c.Type == CardType.Attack && c.TargetType == TargetType.AllEnemies);
            string cleave = areaCard.Id.Entry;
            engine.Run.CardNames[cleave] = areaCard.Title;
            engine.Record("1", cleave, m => { m.Uses += 4; m.Damage += 128; m.AreaDamage += 128; m.Energy += 4; });
            engine.Record("2", null, m => { m.Damage += 94; m.Uses += 7; m.BlockGenerated += 36; });
            engine.Damage(null, null, "2", false, 7, 19, false); engine.Heal("1", null, "2", 5); engine.Tick(32, 3);
            // Same right-aligned HBox layout as the installed top_bar.tscn; no live run is needed.
            var topBar = new HBoxContainer { Name = "TopBarLayoutFixture", Alignment = BoxContainer.AlignmentMode.End, GrowHorizontal = Control.GrowDirection.Begin };
            game.AddChild(topBar); topBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
            topBar.OffsetLeft = -492; topBar.OffsetRight = -16; topBar.OffsetBottom = 80;
            var timer = new Button { Text = "00:32", CustomMinimumSize = new(160, 80) }; topBar.AddChild(timer);
            var map = new Button { Text = "Map", CustomMinimumSize = new(100, 80) }; topBar.AddChild(map);
            topBar.AddChild(new Button { Text = "Deck", CustomMinimumSize = new(100, 80) });
            topBar.AddChild(new Button { Text = "Menu", CustomMinimumSize = new(100, 80) });
            StatsOverlay.Preview(game, map);
            var screen = game.GetNode<Control>("SpireStatistics/StatisticsScreen");
            void Click(string text) => Tree(screen).OfType<Button>().Single(b => b.Text == text).EmitSignal(Button.SignalName.Pressed);
            void AssertBounds() {
                var sheet = Tree(screen).OfType<PanelContainer>().Single(p => p.Name == "StatisticsSheet");
                if (!game.GetViewport().GetVisibleRect().Encloses(sheet.GetGlobalRect())) throw new Exception("Statistics screen exceeds the viewport.");
            }
            async Task Shot(string name) { await Frame(game); game.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output, name)); }
            await Frame(game); AssertBounds();
            var entry = topBar.GetNode<Button>("SpireStatisticsButton");
            if (entry.GetGlobalRect().Intersects(map.GetGlobalRect()) || entry.GetGlobalRect().Intersects(timer.GetGlobalRect()) ||
                !game.GetViewport().GetVisibleRect().Encloses(entry.GetGlobalRect())) throw new Exception("Stats overlaps the map/timer or leaves the viewport.");
            if (Tree(screen).OfType<Label>().Any(l => l.Text.Contains("DPS"))) throw new Exception("DPS is still displayed.");
            if (Tree(screen).OfType<Button>().Any(b => b.Text == "−")) throw new Exception("Old collapse control remains.");
            var label = Tree(screen).OfType<Label>().Single(l => l.Text == "Damage / round");
            engine.Record("1", null, m => m.Damage++);
            typeof(StatsOverlay).GetMethod("Refresh", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
            if (!GodotObject.IsInstanceValid(label)) throw new Exception("Live refresh replaced static controls.");
            await Shot("v2-overview.png");
            foreach (string tab in new[] { "Offense", "Defense", "Support", "Cards" }) { Click(tab); await Shot("v2-" + tab.ToLowerInvariant() + ".png"); }
            Click("Overview"); Click("Entire Run"); await Frame(game); Click("This Combat"); Click("Back");
            if (screen.Visible) throw new Exception("Back did not close statistics.");
            entry.EmitSignal(Button.SignalName.Pressed);
            if (!screen.Visible) throw new Exception("Stats button did not reopen the screen.");
            MegaCrit.Sts2.Core.Nodes.CommonUi.NHotkeyManager.Instance!._UnhandledInput(new InputEventAction {
                Action = MegaCrit.Sts2.Core.ControllerInput.MegaInput.cancel, Pressed = true });
            await Frame(game);
            if (screen.Visible) throw new Exception("Escape/cancel did not close statistics.");

            var pile = alice.PlayerCombatState!.DrawPile;
            var first = ModelDb.Card<StrikeIronclad>().ToMutable(); first.Owner = alice; pile.AddInternal(first, silent: true);
            for (int i = 0; i < 4; i++) { var card = ModelDb.Card<DefendIronclad>().ToMutable(); card.Owner = alice; pile.AddInternal(card, silent: true); }
            var node = NCard.Create(first)!; game.AddChild(node); node.Position = new(960, 500); node.UpdateVisuals(PileType.Draw, CardPreviewMode.Normal);
            await Frame(game);
            var badge = node.Body.GetNode<Label>("SpireStatsDrawChance");
            if (!badge.Text.Contains("100%")) throw new Exception("Five-card hand should show 100%: " + badge.Text);
            var bounds = node.Body.GetGlobalTransform() * new Rect2(-NCard.defaultSize / 2, NCard.defaultSize);
            if (!bounds.Encloses(badge.GetGlobalRect())) throw new Exception("Draw label is outside the card.");
            await Shot("v2-draw-hand.png"); node.Visible = false;
            engine.Run.CardNames[first.Id.Entry] = first.Title; engine.Record("1", first.Id.Entry, m => { m.Uses += 3; m.Damage += 18; m.Energy += 3; });
            var inspect = game.GetInspectCardScreen(); inspect.Open(pile.Cards.ToList(), 0); await Shot("v2-card-inspect.png");
            var details = inspect.GetNode<PanelContainer>("SpireStatsCardDetails");
            if (!details.IsVisibleInTree() || !game.GetViewport().GetVisibleRect().Encloses(details.GetGlobalRect())) throw new Exception("Card details are outside the viewport.");
            GD.Print("[Spire Stats UI] PASS native fonts, five tabs, scope, stable refresh, top-bar layout, Back/Escape/reopen, 5-of-5 probability and card inspection.");
            game.GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("[Spire Stats UI] FAIL " + ex); game.GetTree().Quit(1); }
    }
}
