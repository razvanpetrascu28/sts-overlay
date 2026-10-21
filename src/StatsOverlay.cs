using System.Globalization;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Runs;

namespace StsOverlay;

internal static class StatsOverlay
{
    private sealed record Metric(string Name, Func<Metrics, FightStats, string> Read, string Tip = "");
    private static readonly Color gold = new("eacb89"), cream = new("f0e5ce"), muted = new("afa798");
    private static Godot.Timer? timer;
    private static Control? root, screen;
    private static Button? entry, back;
    private static VBoxContainer content = null!;
    private static HBoxContainer playerRow = null!;
    private static OptionButton playerPicker = null!;
    private static Label caption = null!;
    private static Font regular = null!, bold = null!;
    private static string tab = "Overview", structure = "", nameSignature = "";
    private static bool overall, hotkeys;
    private static Control? priorFocus;
    private static readonly List<string> playerIds = new();
    private static readonly List<(Label Label, Func<FightStats, string> Read)> live = new();
    private static readonly Dictionary<string, Button> tabs = new(), scopes = new();
    private static readonly Dictionary<string, Button> cardButtons = new();
    private static PanelContainer? inspectPanel;
    private static string? inspectPlayer, inspectCard;
    private static string inspectStructure = "";
    private static readonly List<(Label Combat, Label Run)> inspectValues = new();

    internal static void Attach(NGame game)
    {
        if (GodotObject.IsInstanceValid(timer)) return;
        regular = ResourceLoader.Load<Font>("res://themes/kreon_regular_shared.tres");
        bold = ResourceLoader.Load<Font>("res://themes/kreon_bold_glyph_space_one.tres");
        timer = new Godot.Timer { Name = "SpireStatsRefresh", WaitTime = .25, Autostart = true, IgnoreTimeScale = true };
        timer.Timeout += () => GameTracker.Safe(() => {
            GameTracker.Tick();
            if (!GodotObject.IsInstanceValid(root) && NRun.Instance?.GlobalUi is { } ui && ui.IsNodeReady()) Build(ui, ui.TopBar.Map);
            Refresh();
        });
        game.AddChild(timer);
        GD.Print("[Spire Stats] Statistics available from the run's top bar.");
    }

    private static void Build(Control parent, Control? map)
    {
        root = new Control { Name = "SpireStatistics", MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        structure = nameSignature = ""; live.Clear(); cardButtons.Clear(); tabs.Clear(); scopes.Clear();
        entry = ActionButton(map?.GetParent() ?? root, "Stats", () => { if (screen!.Visible) Close(); else Show(); });
        entry.Name = "SpireStatisticsButton"; entry.CustomMinimumSize = new(112, 60); entry.AddThemeFontSizeOverride("font_size", 23);
        entry.Icon = Texture("res://images/atlases/ui_atlas.sprites/top_bar/top_bar_deck.tres");
        entry.ExpandIcon = true; entry.AddThemeConstantOverride("icon_max_width", 28);
        if (map != null)
        {
            // Let the native top-bar container make room beside Map and its timer.
            entry.GetParent().MoveChild(entry, map.GetIndex());
        }
        else { entry.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight); entry.OffsetLeft = -460; entry.OffsetRight = -330; entry.OffsetTop = 14; entry.OffsetBottom = 74; }

        screen = new Control { Name = "StatisticsScreen", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddChild(screen); screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new("080b10eb"), MouseFilter = Control.MouseFilterEnum.Ignore };
        screen.AddChild(shade); shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var sheet = Frame(screen, "StatisticsSheet", 30);
        sheet.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        sheet.OffsetLeft = 150; sheet.OffsetRight = -150; sheet.OffsetTop = 110; sheet.OffsetBottom = -90;
        var column = Column(sheet, 20);
        var heading = Row(column, 18);
        Text(heading, "Combat Statistics", 42, gold, true);
        back = ActionButton(heading, "Back", Close);
        back.Icon = Texture("res://images/atlases/compressed.sprites/back_button_arrow.tres");
        back.ExpandIcon = true; back.AddThemeConstantOverride("icon_max_width", 30);
        var scope = Row(column, 20);
        foreach (string name in new[] { "This Combat", "Entire Run" }) {
            string key = name; scopes[name] = ActionButton(scope, name, () => { overall = key == "Entire Run"; Refresh(); });
        }
        caption = Text(scope, "", 22, muted); caption.HorizontalAlignment = HorizontalAlignment.Right;
        Rule(column);
        var navigation = Row(column, 16);
        foreach (string name in new[] { "Overview", "Offense", "Defense", "Support", "Cards" })
        {
            string key = name;
            var button = ActionButton(navigation, name, () => { tab = key; structure = ""; Refresh(); });
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Icon = TabIcon(name); button.ExpandIcon = true; button.AddThemeConstantOverride("icon_max_width", 30);
            tabs[name] = button;
        }
        playerRow = Row(column, 16); Text(playerRow, "Player", 24, muted).SizeFlagsHorizontal = Control.SizeFlags.Fill;
        playerPicker = new OptionButton { CustomMinimumSize = new(300, 48) };
        StyleControl(playerPicker); playerRow.AddChild(playerPicker);
        playerPicker.ItemSelected += _ => { structure = ""; Refresh(); };
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddChild(scroll);
        content = Column(scroll, 16); content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        root.TreeExiting += Close;
        GD.Print("[Spire Stats] Run statistics screen attached.");
    }

    private static void Show()
    {
        if (!GodotObject.IsInstanceValid(screen) || screen!.Visible) return;
        priorFocus = screen.GetViewport().GuiGetFocusOwner(); screen.Visible = true;
        NHotkeyManager.Instance!.AddBlockingScreen(screen);
        NHotkeyManager.Instance.PushHotkeyPressedBinding(MegaInput.cancel, Close);
        NHotkeyManager.Instance.PushHotkeyPressedBinding(MegaInput.pauseAndBack, Close);
        hotkeys = true; structure = ""; Refresh(); back?.GrabFocus();
    }

    private static void Close()
    {
        if (hotkeys && GodotObject.IsInstanceValid(NHotkeyManager.Instance)) {
            NHotkeyManager.Instance.RemoveHotkeyPressedBinding(MegaInput.cancel, Close);
            NHotkeyManager.Instance.RemoveHotkeyPressedBinding(MegaInput.pauseAndBack, Close);
            if (GodotObject.IsInstanceValid(screen)) NHotkeyManager.Instance.RemoveBlockingScreen(screen!);
        }
        hotkeys = false;
        if (GodotObject.IsInstanceValid(screen)) screen!.Visible = false;
        if (GodotObject.IsInstanceValid(priorFocus)) priorFocus!.GrabFocus();
    }

    private static void Refresh()
    {
        if (GodotObject.IsInstanceValid(inspectPanel) && inspectPanel!.IsVisibleInTree()) RefreshInspection();
        if (!GodotObject.IsInstanceValid(screen) || !screen!.Visible) return;
        var engine = GameTracker.Stats; var view = engine.View(overall);
        string names = string.Join("|", engine.Run.Names.Select(p => p.Key + p.Value));
        if (names != nameSignature)
        {
            string? previous = SelectedPlayer(); nameSignature = names; playerIds.Clear(); playerPicker.Clear();
            foreach (var (id, name) in engine.Run.Names) { playerIds.Add(id); playerPicker.AddItem(name); }
            string? selected = previous ?? LocalContext.NetId?.ToString(CultureInfo.InvariantCulture);
            if (playerIds.Count > 0) playerPicker.Select(Math.Max(0, selected == null ? 0 : playerIds.IndexOf(selected)));
        }
        foreach (var (name, button) in tabs) button.Modulate = name == tab ? gold : muted;
        foreach (var (name, button) in scopes) button.Modulate = (name == "Entire Run") == overall ? gold : muted;
        playerRow.Visible = tab == "Cards";
        caption.Text = $"{view.Turns} rounds · {view.Players.Count} players";
        string? player = SelectedPlayer(); var cardStats = player == null ? null : view.Players.GetValueOrDefault(player);
        string key = $"{tab}:{overall}:{player}:" + string.Join("|", view.Players.Keys)
            + (tab == "Cards" && cardStats != null ? string.Join("|", cardStats.Cards.Keys.Order()) : "");
        if (structure != key)
        {
            structure = key; Clear(content); live.Clear(); cardButtons.Clear();
            if (view.Players.Count == 0) Text(content, "Play a combat to start collecting statistics.", 28, muted);
            else if (tab == "Cards") BuildCards(player, cardStats);
            else BuildPlayers(view);
        }
        foreach (var cell in live) cell.Label.Text = cell.Read(view);
        foreach (var (card, button) in cardButtons) {
            var metric = cardStats?.Cards.GetValueOrDefault(card) ?? new();
            button.Text = CardName(card); button.TooltipText = $"{metric.Uses} plays · {N(metric.Damage)} damage. Click to inspect this card.";
        }
    }

    private static Metric[] Columns() => tab switch {
        "Offense" => [new("Damage", (m, _) => N(m.Damage)), new("AoE damage", (m, _) => N(m.AreaDamage), "Damage from effects targeting all opponents; included in total damage."),
            new("Damage / round", (m, v) => Rate(m.Damage, v.Turns), "Actual enemy HP removed divided by combat rounds."), new("Card plays", (m, _) => N(m.Uses))],
        "Defense" => [new("Block generated", (m, _) => N(m.BlockGenerated)), new("Damage blocked", (m, _) => N(m.DamageBlocked), "Actual damage absorbed on this player."),
            new("HP lost", (m, _) => N(m.DamageTaken)), new("Self damage", (m, _) => N(m.SelfDamage))],
        "Support" => [new("Healing given", (m, _) => N(m.Healing), "HP actually restored; excludes overhealing."), new("To allies", (m, _) => N(m.AllyHealing)),
            new("Healing received", (m, _) => N(m.HealingReceived)), new("Stacks applied", (m, _) => N(m.PowersApplied), "Observed power stacks, not a support score.")],
        _ => [new("Damage", (m, _) => N(m.Damage)), new("Damage / round", (m, v) => Rate(m.Damage, v.Turns)),
            new("Damage blocked", (m, _) => N(m.DamageBlocked)), new("Ally healing", (m, _) => N(m.AllyHealing))]
    };

    private static void BuildPlayers(FightStats view)
    {
        var metrics = Columns(); var grid = Grid(content, metrics.Length + 1);
        Text(grid, "Player", 24, muted);
        foreach (var metric in metrics) Text(grid, metric.Name, 24, muted).TooltipText = metric.Tip;
        foreach (string id in view.Players.Keys)
        {
            var identity = Row(grid, 14); identity.CustomMinimumSize = new(240, 78);
            var player = FindPlayer(id); if (player != null) Icon(identity, player.Character.IconTexture, 46);
            Text(identity, Name(id), 28, cream, true).TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            foreach (var metric in metrics) {
                var value = Text(grid, "", 32, gold); value.VerticalAlignment = VerticalAlignment.Center; value.TooltipText = metric.Tip;
                live.Add((value, current => metric.Read(current.Players.GetValueOrDefault(id)?.Total ?? new(), current)));
            }
        }
        if (view.UnattributedDamage > 0) Text(content, $"{N(view.UnattributedDamage)} damage has no unique source.", 20, muted)
            .TooltipText = "Shared effects are kept separate when their owner cannot be determined.";
    }

    private static void BuildCards(string? playerId, PlayerStats? stats)
    {
        if (stats == null || stats.Cards.Count == 0) { Text(content, "No card results in this view yet.", 28, muted); return; }
        var grid = Grid(content, 5);
        foreach (string title in new[] { "Card", "Plays", "Damage", "Block", "Healing" }) Text(grid, title, 24, muted);
        foreach (string card in stats.Cards.Keys.OrderBy(CardName))
        {
            string cardId = card; var line = Row(grid, 14); line.CustomMinimumSize = new(340, 68);
            var model = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == cardId); if (model != null) Icon(line, model.Portrait, 48);
            cardButtons[cardId] = ActionButton(line, CardName(cardId), () => InspectCard(playerId!, cardId));
            cardButtons[cardId].ClipText = true; cardButtons[cardId].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            foreach (Func<Metrics, long> read in new Func<Metrics, long>[] { m => m.Uses, m => m.Damage, m => m.BlockGenerated, m => m.Healing }) {
                var label = Text(grid, "", 28, gold); label.VerticalAlignment = VerticalAlignment.Center;
                live.Add((label, current => N(read(current.Players.GetValueOrDefault(playerId!)?.Cards.GetValueOrDefault(cardId) ?? new()))));
            }
        }
    }

    private static void InspectCard(string playerId, string cardId)
    {
        var player = FindPlayer(playerId); var canonical = ModelDb.AllCards.FirstOrDefault(c => c.Id.Entry == cardId);
        if (player == null || canonical == null) return;
        var preview = canonical.ToMutable(); preview.Owner = player;
        Close(); NGame.Instance!.GetInspectCardScreen().Open([preview], 0);
    }

    internal static void UpdateDrawBadge(NCard node)
    {
        const string name = "SpireStatsDrawChance"; var badge = node.Body.GetNodeOrNull<Label>(name);
        if (node.DisplayingPile != PileType.Draw || node.Model?.Pile?.Type != PileType.Draw)
        { if (badge != null) badge.Visible = false; return; }
        var card = node.Model; var pile = card.Pile!;
        int copies = pile.Cards.Count(c => c.Id == card.Id), count = pile.Cards.Count, draws = CombatManager.baseHandDrawCount;
        if (card.Owner.Creature.CombatState is { } state) draws = Math.Clamp((int)Hook.ModifyHandDraw(state, card.Owner, draws, out _), 0, CardPile.MaxCardsInHand);
        double chance = StatsEngine.NextHand(copies, count, draws);
        if (badge == null) {
            badge = new Label { Name = name, Position = new(-140, 164), Size = new(280, 32), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Pass, ZIndex = 30 };
            StyleControl(badge, 21); badge.AddThemeFontOverride("font", bold);
            badge.AddThemeColorOverride("font_outline_color", new Color("17100b")); badge.AddThemeConstantOverride("outline_size", 7); node.Body.AddChild(badge);
        }
        badge.Visible = true; badge.Text = $"Next hand · {chance.ToString("0.#%", CultureInfo.InvariantCulture)}";
        badge.AddThemeColorOverride("font_color", chance >= 1 ? new Color("aee5aa") : gold);
        badge.TooltipText = $"At least one copy in the next {draws} draws. {copies} of {count} remaining cards.\nUses the current draw count and a shuffled pile; future draw restrictions or ordered cards can change this.";
    }

    internal static void CardDetails(NInspectCardScreen parent, CardModel card)
    {
        Close(); inspectCard = GameTracker.CardKey(card); inspectStructure = "";
        try { inspectPlayer = GameTracker.Id(card.Owner); } catch { inspectPlayer = null; }
        inspectPanel = parent.GetNodeOrNull<PanelContainer>("SpireStatsCardDetails");
        if (inspectPanel == null) { inspectPanel = Frame(parent, "SpireStatsCardDetails", 20); inspectPanel.Position = new(28, 180); inspectPanel.CustomMinimumSize = new(390, 0); }
        RefreshInspection();
    }

    private static List<(string Name, long Combat, long Run, string Tip)> CardValues(Metrics c, Metrics r)
    {
        var values = new List<(string, long, long, string)>();
        void Add(string name, long combat, long run, string tip = "") { if (combat != 0 || run != 0) values.Add((name, combat, run, tip)); }
        Add("Damage", c.Damage, r.Damage); Add("AoE damage", c.AreaDamage, r.AreaDamage); Add("Block generated", c.BlockGenerated, r.BlockGenerated);
        Add("Block used", c.BlockUsed, r.BlockUsed, "Credits the oldest remaining block first (FIFO). Shared block has no uniquely correct per-card split.");
        Add("Healing", c.Healing, r.Healing); Add("To allies", c.AllyHealing, r.AllyHealing);
        values.Add(("Plays", c.Uses, r.Uses, "Includes automatic plays and replays."));
        Add("Drawn", c.Draws, r.Draws); Add("Energy spent", c.Energy, r.Energy); Add("Stars spent", c.Stars, r.Stars); Add("Stacks applied", c.PowersApplied, r.PowersApplied);
        return values;
    }

    private static void RefreshInspection()
    {
        if (inspectPanel == null || inspectCard == null) return;
        string cardId = inspectCard;
        Metrics Get(bool all) => inspectPlayer == null ? new() : GameTracker.Stats.View(all).Players.GetValueOrDefault(inspectPlayer)?.Cards.GetValueOrDefault(cardId) ?? new();
        var values = CardValues(Get(false), Get(true)); string key = inspectPlayer + inspectCard + string.Join("|", values.Select(v => v.Name));
        if (inspectStructure != key) {
            inspectStructure = key; Clear(inspectPanel); inspectValues.Clear();
            var column = Column(inspectPanel, 16); Text(column, "Card Statistics", 30, gold, true); Rule(column);
            var grid = Grid(column, 3, 14); Text(grid, "", 19); Text(grid, "Combat", 19, muted); Text(grid, "Run", 19, muted);
            foreach (var value in values) { Text(grid, value.Name, 21).TooltipText = value.Tip; inspectValues.Add((Text(grid, "", 23, gold), Text(grid, "", 23, gold))); }
        }
        for (int i = 0; i < values.Count; i++) { inspectValues[i].Combat.Text = N(values[i].Combat); inspectValues[i].Run.Text = N(values[i].Run); }
    }

    private static string? SelectedPlayer() => playerPicker != null && playerPicker.Selected >= 0 && playerPicker.Selected < playerIds.Count ? playerIds[playerPicker.Selected] : null;
    private static Player? FindPlayer(string id) {
        var player = GameTracker.Combat?.Players.FirstOrDefault(p => GameTracker.Id(p) == id) ?? RunManager.Instance.DebugOnlyGetState()?.Players.FirstOrDefault(p => GameTracker.Id(p) == id);
#if UI_SMOKE
        player ??= PreviewPlayers.GetValueOrDefault(id);
#endif
        return player;
    }
    private static string Name(string id) => GameTracker.Stats.Run.Names.GetValueOrDefault(id, id);
    private static string CardName(string id) => GameTracker.Stats.Run.CardNames.GetValueOrDefault(id, id.Replace('_', ' '));
    private static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    private static string Rate(long value, int rounds) => rounds > 0 ? ((double)value / rounds).ToString("0.#", CultureInfo.InvariantCulture) : "—";
    private static Texture2D? Texture(string path) => ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    private static Texture2D? TabIcon(string category) => category switch {
        "Offense" => ModelDb.Power<StrengthPower>().Icon, "Defense" => ModelDb.Power<PlatingPower>().Icon, "Support" => ModelDb.Power<RegenPower>().Icon,
        _ => Texture("res://images/atlases/ui_atlas.sprites/top_bar/top_bar_deck.tres")
    };
    private static void Clear(Node node) { foreach (var child in node.GetChildren()) { node.RemoveChild(child); child.QueueFree(); } }
    private static void StyleControl(Control control, int size = 25) {
        control.AddThemeFontOverride("font", regular); control.AddThemeFontSizeOverride("font_size", size);
        control.AddThemeColorOverride("font_color", cream); control.AddThemeColorOverride("font_hover_color", gold); control.AddThemeColorOverride("font_focus_color", gold);
    }
    private static Label Text(Node parent, string text, int size = 24, Color? color = null, bool heading = false) {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Pass, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        StyleControl(label, size); label.AddThemeFontOverride("font", heading ? bold : regular); label.AddThemeColorOverride("font_color", color ?? cream); parent.AddChild(label); return label;
    }
    private static Button ActionButton(Node parent, string text, Action action) {
        var button = new Button { Text = text, MouseDefaultCursorShape = Control.CursorShape.PointingHand, CustomMinimumSize = new(110, 52) };
        StyleControl(button); var normal = new StyleBoxEmpty { ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8 };
        button.AddThemeStyleboxOverride("normal", normal); button.AddThemeStyleboxOverride("hover", normal); button.AddThemeStyleboxOverride("pressed", normal);
        button.Pressed += action; parent.AddChild(button); return button;
    }
    private static HBoxContainer Row(Node parent, int space = 16) { var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", space); parent.AddChild(row); return row; }
    private static VBoxContainer Column(Node parent, int space = 16) { var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", space); parent.AddChild(column); return column; }
    private static GridContainer Grid(Node parent, int columns, int gap = 30) {
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; grid.AddThemeConstantOverride("h_separation", gap); grid.AddThemeConstantOverride("v_separation", 24); parent.AddChild(grid); return grid;
    }
    private static void Icon(Node parent, Texture2D texture, int size) { parent.AddChild(new TextureRect { Texture = texture, CustomMinimumSize = new(size, size), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore }); }
    private static void Rule(Node parent) { parent.AddChild(new ColorRect { Color = new("67563b"), CustomMinimumSize = new(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore }); }
    private static PanelContainer Frame(Node parent, string name, int margin) {
        var frame = new PanelContainer { Name = name };
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new("171d24f5"), BorderColor = new("90734a"), BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin });
        parent.AddChild(frame); return frame;
    }

#if UI_SMOKE
    internal static readonly Dictionary<string, Player> PreviewPlayers = new();
    internal static void Preview(NGame game, Control? map = null) { Build(game, map); Show(); }
#endif
}
