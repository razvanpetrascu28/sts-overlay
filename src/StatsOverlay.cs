using System.Globalization;
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace StsOverlay;

internal static class StatsOverlay
{
    private static CanvasLayer? layer;
    private static PanelContainer meter = null!;
    private static PanelContainer menu = null!;
    private static VBoxContainer rows = null!;
    private static VBoxContainer body = null!;
    private static Label scope = null!;
    private static Label footer = null!;
    private static OptionButton playerPicker = null!;
    private static readonly List<string> playerIds = new();
    private static bool overall;
    private static bool cardsTab;
    private static bool compact;
    private static bool dragging;
    private static string? selectedCard;
    private static string? inspectedCard;
    private static string? inspectedPlayer;
    private static PanelContainer? inspectedPanel;
    private static string nameSignature = "";
    private static string menuSignature = "";
    private static string inspectSignature = "";
    private static string meterSignature = "";
    private static readonly Dictionary<string, (Label Total, Label Rate)> meterLabels = new();
    private static readonly List<Label> metricLabels = new();
    private static readonly Dictionary<string, Button> cardButtons = new();
    private static readonly List<(Label Combat, Label Run)> inspectValues = new();
    private static readonly Color gold = new("dcc594");

    internal static void Attach(NGame game)
    {
        if (GodotObject.IsInstanceValid(layer)) return;
        layer = new CanvasLayer { Name = "SpireStats", Layer = 40 };
        game.AddChild(layer);
        var root = new Control { Name = "StatsRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        meter = Panel(root, "DamageMeter");
        meter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        meter.OffsetLeft = -354; meter.OffsetRight = -24; meter.OffsetTop = 140; meter.OffsetBottom = 250;
        meter.CustomMinimumSize = new(330, 0);
        var content = Column(meter);
        var header = new HBoxContainer(); content.AddChild(header);
        var drag = Button(header, "Spire Stats", () => { });
        drag.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        drag.TooltipText = "Drag to move the damage panel.";
        drag.GuiInput += input => {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } click) dragging = click.Pressed;
            if (input is InputEventMouseMotion move && dragging) {
                meter.Position += move.Relative;
                var size = root.GetRect().Size;
                meter.Position = meter.Position.Clamp(Vector2.Zero, new Vector2(Math.Max(0, size.X - meter.Size.X), Math.Max(0, size.Y - meter.Size.Y)));
            }
        };
        Button(header, "Stats", () => { menu.Visible = !menu.Visible; Refresh(); });
        Button(header, "−", () => { compact = !compact; rows.Visible = !compact; footer.Visible = !compact; });
        var tabs = new HBoxContainer(); content.AddChild(tabs);
        Button(tabs, "Current Combat", () => { overall = false; Refresh(); });
        Button(tabs, "Overall Run", () => { overall = true; Refresh(); });
        scope = Text(content, "Current Combat", 14, gold);
        rows = new VBoxContainer(); content.AddChild(rows);
        footer = Text(content, "Enter a combat to start tracking.", 12);

        menu = Panel(root, "StatisticsMenu");
        menu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        menu.OffsetLeft = -790; menu.OffsetRight = -380; menu.OffsetTop = 140; menu.OffsetBottom = 700;
        menu.CustomMinimumSize = new(410, 0);
        menu.Visible = false;
        var menuColumn = Column(menu);
        var menuHeader = new HBoxContainer(); menuColumn.AddChild(menuHeader);
        Text(menuHeader, "Detailed Statistics", 18, gold).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Button(menuHeader, "Close", () => menu.Visible = false);
        var menuTabs = new HBoxContainer(); menuColumn.AddChild(menuTabs);
        Button(menuTabs, "Player", () => { cardsTab = false; selectedCard = null; Refresh(); });
        Button(menuTabs, "Cards", () => { cardsTab = true; selectedCard = null; Refresh(); });
        playerPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        menuColumn.AddChild(playerPicker);
        playerPicker.ItemSelected += _ => { selectedCard = null; Refresh(); };
        var scroll = new ScrollContainer { CustomMinimumSize = new(380, 420), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        menuColumn.AddChild(scroll);
        body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(body);
        Text(menuColumn, "Card results group copies and upgrades of the same card.", 12);

        var timer = new Godot.Timer { WaitTime = .25, Autostart = true, IgnoreTimeScale = true };
        timer.Timeout += () => GameTracker.Safe(() => { GameTracker.Tick(); Refresh(); });
        layer.AddChild(timer);
        Refresh();
        GD.Print("[Spire Stats] Overlay attached.");
    }

    private static void Refresh()
    {
        if (!GodotObject.IsInstanceValid(layer)) return;
        var engine = GameTracker.Stats;
        meter.Visible = engine.Run.Key.Length > 0;
        var view = engine.View(overall);
        scope.Text = overall ? "Overall Run" : "Current Combat";
        string roster = string.Join("|", view.Players.Keys);
        if (meterSignature != roster)
        {
            meterSignature = roster; Clear(rows); meterLabels.Clear();
            foreach (string id in view.Players.Keys) meterLabels[id] = (Text(rows, "", 15), Text(rows, "", 12, gold));
        }
        foreach (var (id, player) in view.Players)
        {
            var total = player.Total;
            double dps = view.Seconds > 0 ? total.Damage / view.Seconds : 0;
            double dpt = view.Turns > 0 ? (double)total.Damage / view.Turns : 0;
            meterLabels[id].Total.Text = $"{Name(id)}   {N(total.Damage)} damage";
            meterLabels[id].Rate.Text = $"{F(dps)} DPS  ·  {F(dpt)} / turn";
        }
        footer.Text = view.Players.Count == 0 ? "Enter a combat to start tracking."
            : $"{view.Turns} turns · {view.Seconds:F0}s combat time"
                + (view.UnattributedDamage > 0 ? $"\n{N(view.UnattributedDamage)} damage with no unique source" : "");
        string signature = string.Join("|", engine.Run.Names.Select(p => p.Key + ":" + p.Value));
        if (nameSignature != signature)
        {
            nameSignature = signature; playerIds.Clear(); playerPicker.Clear();
            foreach (var (id, name) in engine.Run.Names) { playerIds.Add(id); playerPicker.AddItem(name); }
            string? me = LocalContext.NetId?.ToString(CultureInfo.InvariantCulture);
            int index = me == null ? 0 : Math.Max(0, playerIds.IndexOf(me));
            if (playerIds.Count > 0) playerPicker.Select(index);
        }
        if (menu.Visible)
        {
            string? playerId = playerPicker.Selected >= 0 && playerPicker.Selected < playerIds.Count ? playerIds[playerPicker.Selected] : null;
            var stats = playerId != null ? view.Players.GetValueOrDefault(playerId) : null;
            string signatureNow = $"{cardsTab}:{selectedCard}:{playerId}:{stats != null}:"
                + (cardsTab && selectedCard == null && stats != null ? string.Join("|", stats.Cards.Keys.Order()) : "");
            if (menuSignature != signatureNow)
            {
                menuSignature = signatureNow; Clear(body); metricLabels.Clear(); cardButtons.Clear();
                if (stats == null) Text(body, "No tracked results in this view.", 14);
                else if (!cardsTab) Summary(body, stats.Total);
                else if (selectedCard != null)
                {
                    Button(body, "← All cards", () => { selectedCard = null; Refresh(); });
                    Text(body, CardName(selectedCard), 18, gold);
                    Summary(body, stats.Cards.GetValueOrDefault(selectedCard) ?? new(), true);
                }
                else
                {
                    Text(body, "Select a card for its observed results.", 14);
                    foreach (string card in stats.Cards.Keys.OrderBy(CardName))
                    {
                        string key = card;
                        cardButtons[card] = Button(body, "", () => { selectedCard = key; Refresh(); });
                    }
                }
            }
            if (stats != null)
            {
                Metrics current = cardsTab && selectedCard != null ? stats.Cards.GetValueOrDefault(selectedCard) ?? new() : stats.Total;
                var values = Values(current, cardsTab).ToArray();
                for (int i = 0; i < metricLabels.Count; i++) metricLabels[i].Text = N(values[i].Item2);
                foreach (var (card, button) in cardButtons)
                {
                    var value = stats.Cards[card];
                    button.Text = $"{CardName(card)}  ·  {value.Uses} uses  ·  {N(value.Damage)} damage";
                }
            }
        }
        if (GodotObject.IsInstanceValid(inspectedPanel) && inspectedPanel!.IsVisibleInTree()) RenderCardComparison();
    }

    private static void Summary(Node parent, Metrics m, bool card = false)
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(grid);
        foreach (var (label, value) in Values(m, card)) {
            Text(grid, label, 14).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            metricLabels.Add(Text(grid, N(value), 14, gold));
        }
        Text(parent, "Block used (FIFO) credits the oldest remaining block first.\nDamage blocked is the exact amount absorbed on the player.\nHealing counts HP actually restored; excess healing is excluded.", 12);
    }

    private static IEnumerable<(string, long)> Values(Metrics m, bool card)
    {
        yield return ("Damage dealt", m.Damage);
        yield return ("AoE damage", m.AreaDamage);
        yield return ("Block generated", m.BlockGenerated);
        yield return ("Block used (FIFO)", m.BlockUsed);
        if (!card) {
            yield return ("Damage blocked", m.DamageBlocked);
            yield return ("HP lost", m.DamageTaken);
            yield return ("Self-inflicted HP loss", m.SelfDamage);
        }
        yield return ("Healing given", m.Healing);
        yield return ("Healing given to allies", m.AllyHealing);
        if (!card) yield return ("Healing received", m.HealingReceived);
        yield return (card ? "Times played (incl. replays)" : "Card plays (incl. replays)", m.Uses);
        yield return ("Times drawn", m.Draws);
        yield return ("Energy spent", m.Energy);
        yield return ("Stars spent", m.Stars);
        yield return ("Power stacks applied", m.PowersApplied);
    }

    internal static void UpdateDrawBadge(NCard node)
    {
        const string badgeName = "SpireStatsDrawChance";
        var badge = node.Body.GetNodeOrNull<Label>(badgeName);
        bool visible = node.DisplayingPile == PileType.Draw && node.Model?.Pile?.Type == PileType.Draw;
        if (!visible) { if (badge != null) badge.Visible = false; return; }
        var card = node.Model!;
        var pile = card.Pile!;
        int copies = pile.Cards.Count(c => c.Id == card.Id);
        int remaining = pile.Cards.Count;
        if (badge == null)
        {
            badge = new Label { Name = badgeName, Position = new(-140, 160), Size = new(280, 40),
                HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 30 };
            badge.AddThemeFontSizeOverride("font_size", 19);
            badge.AddThemeColorOverride("font_color", gold);
            badge.AddThemeColorOverride("font_outline_color", Colors.Black);
            badge.AddThemeConstantOverride("outline_size", 7);
            node.Body.AddChild(badge);
        }
        badge.Visible = true;
        badge.Text = $"Next draw: {StatsEngine.NextDraw(copies, remaining).ToString("P1", CultureInfo.InvariantCulture)} ({copies}/{remaining})";
        badge.TooltipText = "Chance of this card type on one random draw. Copies and upgrades are counted together. Known top-deck effects can override a random draw.";
    }

    internal static void CardDetails(NInspectCardScreen screen, CardModel card)
    {
        if (GodotObject.IsInstanceValid(menu)) menu.Visible = false;
        // The original card's owner is retained; upgrade-preview clones must not select another player's stats.
        inspectedCard = GameTracker.CardKey(card);
        inspectSignature = "";
        try { inspectedPlayer = GameTracker.Id(card.Owner); } catch { inspectedPlayer = null; }
        inspectedPanel = screen.GetNodeOrNull<PanelContainer>("SpireStatsCardDetails");
        if (inspectedPanel == null)
        {
            inspectedPanel = Panel(screen, "SpireStatsCardDetails");
            inspectedPanel.Position = new(30, 180); inspectedPanel.CustomMinimumSize = new(360, 0);
        }
        RenderCardComparison();
    }

    private static void RenderCardComparison()
    {
        if (inspectedPanel == null || inspectedCard == null) return;
        string cardId = inspectedCard;
        Metrics Get(bool all) => inspectedPlayer == null ? new() : GameTracker.Stats.View(all).Players.GetValueOrDefault(inspectedPlayer)?.Cards.GetValueOrDefault(cardId) ?? new();
        var combatMetrics = Get(false); var runMetrics = Get(true);
        string currentSignature = cardId + inspectedPlayer;
        var runValues = Values(runMetrics, true).ToArray();
        var combatValues = Values(combatMetrics, true).ToArray();
        if (inspectSignature != currentSignature) {
        inspectSignature = currentSignature; Clear(inspectedPanel); inspectValues.Clear();
        var column = Column(inspectedPanel);
        Text(column, "Card Statistics", 18, gold);
        Text(column, "All copies of " + CardName(inspectedCard), 13);
        var grid = new GridContainer { Columns = 3 }; column.AddChild(grid);
        Text(grid, "", 13); Text(grid, "Combat", 13, gold); Text(grid, "Run", 13, gold);
        foreach (var (name, value) in combatValues) {
            Text(grid, name, 13).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            inspectValues.Add((Text(grid, N(value), 13), Text(grid, "", 13)));
        }
        Text(column, "Observed results only. Block used follows FIFO.\nCopies and upgrades share statistics.", 12);
        }
        for (int i = 0; i < inspectValues.Count; i++) {
            inspectValues[i].Combat.Text = N(combatValues[i].Item2);
            inspectValues[i].Run.Text = N(runValues[i].Item2);
        }
    }

    private static string Name(string id) => GameTracker.Stats.Run.Names.GetValueOrDefault(id, id);
    private static string CardName(string id) => GameTracker.Stats.Run.CardNames.GetValueOrDefault(id, id.Replace('_', ' '));
    private static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    private static string F(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
    private static void Clear(Node node) { foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); } }

    private static PanelContainer Panel(Node parent, string name)
    {
        var panel = new PanelContainer { Name = name, MouseFilter = Control.MouseFilterEnum.Stop };
        var style = new StyleBoxFlat { BgColor = new("191a1ff2"), BorderColor = new("756548"), BorderWidthBottom = 1,
            BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1, ContentMarginLeft = 12,
            ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10,
            CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5, CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5 };
        panel.AddThemeStyleboxOverride("panel", style); parent.AddChild(panel); return panel;
    }

    private static VBoxContainer Column(Node parent)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8); parent.AddChild(column); return column;
    }
    private static Label Text(Node parent, string text, int size, Color? color = null)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? new Color("e4e4e7")); parent.AddChild(label); return label;
    }
    private static Button Button(Node parent, string text, Action action)
    {
        var button = new Button { Text = text, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        button.AddThemeFontSizeOverride("font_size", 14); button.Pressed += action; parent.AddChild(button); return button;
    }
}
