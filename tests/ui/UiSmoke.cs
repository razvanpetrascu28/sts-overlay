using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Unlocks;

namespace StsOverlay;

// Compiled only with -p:UiSmoke=true, never included in the release DLL.
internal static class UiSmoke
{
    internal static void Schedule() => Callable.From(() => {
        var game = NGame.Instance!;
        var timer = new Godot.Timer { OneShot = true, WaitTime = 20, Autostart = true, IgnoreTimeScale = true };
        timer.Timeout += () => _ = Run(game); game.AddChild(timer);
    }).CallDeferred();

    private static IEnumerable<Node> Tree(Node root)
    {
        yield return root;
        foreach (var child in root.GetChildren()) foreach (var node in Tree(child)) yield return node;
    }
    private static async Task Frame(Node game)
    {
        await game.ToSignal(game.GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
        await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
        await game.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }
    private static async Task Run(NGame game)
    {
        try
        {
            string output = MegaCrit.Sts2.Core.Helpers.CommandLineHelper.GetValue("stats-ui-output")
                ?? Path.Combine(Path.GetTempPath(), "SpireStatsUiSmoke");
            Directory.CreateDirectory(output);
            var engine = GameTracker.Stats;
            engine.Begin("ui-fixture", "fight", new Dictionary<string, string> { ["1"] = "Alice", ["2"] = "Bob" });
            engine.Run.CardNames["CLEAVE"] = "Cleave";
            engine.Record("1", "CLEAVE", m => { m.Uses = 4; m.Damage = 128; m.AreaDamage = 128; m.Energy = 4; });
            engine.Record("2", "DEFEND_IRONCLAD", m => { m.Uses = 5; m.BlockGenerated = 25; m.BlockUsed = 19; });
            engine.Damage(null, null, "2", false, 7, 19, false);
            engine.Heal("1", null, "2", 5); engine.Tick(32, 3);
            typeof(StatsOverlay).GetMethod("Refresh", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
            var canvas = game.GetNode<CanvasLayer>("SpireStats");
            Tree(canvas).OfType<Button>().Single(b => b.Text == "Stats").EmitSignal(Button.SignalName.Pressed);
            await Frame(game);
            var meter = Tree(canvas).OfType<PanelContainer>().Single(p => p.Name == "DamageMeter");
            var menu = Tree(canvas).OfType<PanelContainer>().Single(p => p.Name == "StatisticsMenu");
            var stableLabel = Tree(menu).OfType<Label>().Single(l => l.Text == "Damage dealt");
            engine.Record("1", null, m => m.Damage++);
            typeof(StatsOverlay).GetMethod("Refresh", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
            if (!GodotObject.IsInstanceValid(stableLabel) || !ReferenceEquals(stableLabel, Tree(menu).OfType<Label>().Single(l => l.Text == "Damage dealt")))
                throw new Exception("Statistics refresh replaced the menu controls.");
            await Frame(game);
            var viewport = game.GetViewport().GetVisibleRect();
            if (!viewport.Encloses(meter.GetGlobalRect()) || !viewport.Encloses(menu.GetGlobalRect()))
                throw new Exception($"Panel outside viewport: {viewport}, meter={meter.GetGlobalRect()}, menu={menu.GetGlobalRect()}");
            game.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output, "ui-player.png"));
            Tree(canvas).OfType<Button>().Single(b => b.Text == "Cards").EmitSignal(Button.SignalName.Pressed);
            await Frame(game);
            Tree(canvas).OfType<Button>().Single(b => b.Text.StartsWith("Cleave  ")).EmitSignal(Button.SignalName.Pressed);
            await Frame(game);
            game.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output, "ui-card-menu.png"));

            // Create local card-view fixtures only. No run is started, no game state is saved.
            var player = Player.CreateForNewRun<Ironclad>(UnlockState.none, 1);
            player.ResetCombatState();
            var pile = player.PlayerCombatState!.DrawPile;
            CardModel? first = null;
            for (int i = 0; i < 10; i++)
            {
                var card = i < 2 ? ModelDb.Card<StrikeIronclad>().ToMutable() : ModelDb.Card<DefendIronclad>().ToMutable();
                card.Owner = player; pile.AddInternal(card, silent: true); first ??= card;
            }
            var node = NCard.Create(first!)!;
            game.AddChild(node); node.Position = new(620, 450);
            node.UpdateVisuals(PileType.Draw, CardPreviewMode.Normal);
            await Frame(game);
            var badge = node.Body.GetNode<Label>("SpireStatsDrawChance");
            if (!badge.Text.Contains("20.0") || !badge.Visible) throw new Exception("Duplicate draw badge failed: " + badge.Text);
            var cardBounds = node.Body.GetGlobalTransform() * new Rect2(-NCard.defaultSize / 2, NCard.defaultSize);
            if (!cardBounds.Encloses(badge.GetGlobalRect())) throw new Exception($"Draw badge is outside its card: {cardBounds}, {badge.GetGlobalRect()}");
            game.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output, "ui-draw-badge.png"));
            node.Visible = false;
            engine.Run.CardNames[first!.Id.Entry] = first.Title;
            engine.Record("1", first.Id.Entry, m => { m.Uses += 3; m.Damage += 18; m.Energy += 3; });
            var inspect = game.GetInspectCardScreen();
            inspect.Open(pile.Cards.ToList(), 0);
            await game.ToSignal(game.GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
            await Frame(game);
            var details = inspect.GetNode<PanelContainer>("SpireStatsCardDetails");
            if (!details.IsVisibleInTree() || !viewport.Encloses(details.GetGlobalRect())) throw new Exception("Card inspection details are missing or outside the viewport.");
            game.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(output, "ui-card-inspect.png"));
            GD.Print("[Spire Stats UI] PASS player menu, card menu, duplicate draw badge and panel bounds.");
            game.GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError("[Spire Stats UI] FAIL " + ex); game.GetTree().Quit(1); }
    }
}
