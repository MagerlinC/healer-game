#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.Talents;

/// <summary>
/// Root script for the Overworld scene.
///
/// The Overworld is a 2D walkable level using a library background.  Three
/// interactible objects are placed in world space:
///
///   • Spell Tome   → opens the Spellbook selection overlay
///   • Talent Board → opens the Talent selection overlay
///   • Run Scroll   → opens the Run History overlay
///
/// A persistent HUD (CanvasLayer 5) provides the "Start Run" and
/// "Main Menu" buttons.  Each selection overlay sits on CanvasLayer 10
/// and is hidden until the corresponding object is clicked.
///
/// All spell/talent selections write directly to <see cref="RunState"/> in
/// real-time, identical to the old tab-based UI.
/// </summary>
public partial class OverworldController : Node2D
{
    // ── shared colours ────────────────────────────────────────────────────────
    static readonly Color PanelBg     = new(0.07f, 0.06f, 0.06f, 0.97f);
    static readonly Color PanelBorder = new(0.65f, 0.52f, 0.28f);
    static readonly Color TitleColor  = new(0.95f, 0.84f, 0.50f);
    static readonly Color HintColor   = new(0.45f, 0.42f, 0.38f);
    static readonly Color SepColor    = new(0.50f, 0.40f, 0.22f, 0.55f);
    static readonly Color ArrowColor  = new(0.45f, 0.40f, 0.35f, 0.75f);

    // ── spell panel colours ───────────────────────────────────────────────────
    static readonly Color CardBorderIdle     = new(0.28f, 0.22f, 0.16f);
    static readonly Color CardBorderHover    = new(0.70f, 0.58f, 0.30f);
    static readonly Color CardBorderEquipped = new(0.98f, 0.82f, 0.15f);
    static readonly Color SlotBorderEmpty    = new(0.22f, 0.18f, 0.14f);
    static readonly Color SlotBorderFilled   = new(0.60f, 0.48f, 0.22f);

    const float CardW      = 92f;
    const float CardH      = 116f;
    const float CardIconSz = 64f;

    const string DefaultHint = "Walk up to an object and click it to interact";

    // ── school definitions ────────────────────────────────────────────────────
    static readonly (SpellSchool? School, string Name)[] SpellSchoolTabs =
    {
        (null,                    "All"),
        (SpellSchool.Holy,        "Holy"),
        (SpellSchool.Nature,      "Nature"),
        (SpellSchool.Void,        "Void"),
        (SpellSchool.Chronomancy, "Chronomancy"),
    };

    static readonly (SpellSchool School, string Name, Color Accent)[] TalentSchoolOrder =
    {
        (SpellSchool.Generic,     "General",     new Color(0.70f, 0.65f, 0.60f)),
        (SpellSchool.Holy,        "Holy",        new Color(0.95f, 0.85f, 0.40f)),
        (SpellSchool.Nature,      "Nature",      new Color(0.40f, 0.80f, 0.35f)),
        (SpellSchool.Void,        "Void",        new Color(0.65f, 0.35f, 0.85f)),
        (SpellSchool.Chronomancy, "Chronomancy", new Color(0.35f, 0.75f, 0.90f)),
    };

    // ── runtime state ─────────────────────────────────────────────────────────

    readonly SpellResource?[] _loadout = new SpellResource?[Player.MaxSpellSlots];
    readonly Dictionary<string, (PanelContainer Panel, StyleBoxFlat Border)> _libraryCards = new();
    (PanelContainer Panel, StyleBoxFlat Border, TextureRect Icon)[]? _loadoutSlots;
    readonly List<TalentSlot> _talentSlots = new();
    readonly Dictionary<SpellSchool, Dictionary<int, List<TalentSlot>>> _talentsBySchoolRow = new();

    // ── overworld references ──────────────────────────────────────────────────
    OverworldPlayer?  _player;
    CanvasLayer?      _spellPanel;
    CanvasLayer?      _talentPanel;
    CanvasLayer?      _historyPanel;
    VBoxContainer?    _historyContent;
    Label?            _hintLabel;
    readonly List<Area2D> _interactibles = new();

    // ── lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        System.Array.Copy(RunState.Instance.SelectedSpells, _loadout, Player.MaxSpellSlots);

        // Tooltip singleton must exist before spell/talent panels are built
        AddChild(new GameTooltip());

        // ── Library background ────────────────────────────────────────────────
        var bg = new Sprite2D();
        bg.Texture  = GD.Load<Texture2D>("res://assets/backgrounds/overworld/library.png");
        bg.Centered = true;
        bg.Position = new Vector2(896f, 512f); // centre of 1792×1024
        AddChild(bg);

        // ── Player ────────────────────────────────────────────────────────────
        _player = new OverworldPlayer();
        _player.Position = new Vector2(896f, 700f);
        AddChild(_player);

        // ── Interactibles ─────────────────────────────────────────────────────
        // Positions are approximate for a typical library layout.
        // Adjust them in the Godot editor if they don't land on the right spots.
        var spellTome     = MakeInteractible("res://assets/interactibles/spell-tome.png",
                                             new Vector2(380f,  610f),
                                             new Vector2(0.055f, 0.055f), 28f);
        var talentBoard   = MakeInteractible("res://assets/interactibles/talent-board.png",
                                             new Vector2(896f,  530f),
                                             new Vector2(0.090f, 0.090f), 50f);
        var historyScroll = MakeInteractible("res://assets/interactibles/run-history-scroll.png",
                                             new Vector2(1430f, 610f),
                                             new Vector2(0.055f, 0.055f), 28f);

        AddChild(spellTome);
        AddChild(talentBoard);
        AddChild(historyScroll);
        _interactibles.Add(spellTome);
        _interactibles.Add(talentBoard);
        _interactibles.Add(historyScroll);

        // ── Overlay panels (built once, shown/hidden on demand) ───────────────
        _spellPanel   = BuildOverlayPanel("Spellbook",   BuildSpellbookPane());
        _talentPanel  = BuildOverlayPanel("Talents",     BuildTalentPane());
        _historyPanel = BuildOverlayPanel("Run History", BuildRunHistoryPane());
        AddChild(_spellPanel);
        AddChild(_talentPanel);
        AddChild(_historyPanel);

        // ── HUD ───────────────────────────────────────────────────────────────
        var hud = new CanvasLayer { Layer = 5 };
        AddChild(hud);
        _hintLabel = BuildHintLabel();
        hud.AddChild(_hintLabel);
        hud.AddChild(BuildHUDButtonBar());

        // ── Wire interactible clicks ──────────────────────────────────────────
        spellTome.InputEvent += (_, ev, _) =>
        {
            if (IsLeftClick(ev)) OpenPanel(_spellPanel);
        };
        spellTome.MouseEntered += () => _hintLabel.Text = "Spellbook  •  Click to open";
        spellTome.MouseExited  += () => _hintLabel.Text = DefaultHint;

        talentBoard.InputEvent += (_, ev, _) =>
        {
            if (IsLeftClick(ev)) OpenPanel(_talentPanel);
        };
        talentBoard.MouseEntered += () => _hintLabel.Text = "Talent Board  •  Click to open";
        talentBoard.MouseExited  += () => _hintLabel.Text = DefaultHint;

        historyScroll.InputEvent += (_, ev, _) =>
        {
            if (IsLeftClick(ev)) OpenPanel(_historyPanel, rebuildHistory: true);
        };
        historyScroll.MouseEntered += () => _hintLabel.Text = "Run History  •  Click to open";
        historyScroll.MouseExited  += () => _hintLabel.Text = DefaultHint;

        // Sync talent slots from RunState (if player is returning from a run)
        SyncTalentSlotsFromRunState();
    }

    // ── panel open / close ────────────────────────────────────────────────────

    void OpenPanel(CanvasLayer panel, bool rebuildHistory = false)
    {
        CloseAllPanels();

        if (rebuildHistory && panel == _historyPanel)
            RebuildHistoryContent();

        panel.Visible = true;
        SetInteractiblesPickable(false);
        _player?.SetPhysicsProcess(false);
    }

    void CloseAllPanels()
    {
        if (_spellPanel  != null) _spellPanel.Visible  = false;
        if (_talentPanel != null) _talentPanel.Visible = false;
        if (_historyPanel!= null) _historyPanel.Visible= false;
        SetInteractiblesPickable(true);
        _player?.SetPhysicsProcess(true);
    }

    void SetInteractiblesPickable(bool pickable)
    {
        foreach (var a in _interactibles)
            a.InputPickable = pickable;
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    Label BuildHintLabel()
    {
        var lbl = new Label();
        lbl.Text                = DefaultHint;
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        lbl.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        lbl.OffsetTop           = 10f;
        lbl.AddThemeFontSizeOverride("font_size", 13);
        lbl.AddThemeColorOverride("font_color", HintColor);
        lbl.MouseFilter         = Control.MouseFilterEnum.Ignore;
        return lbl;
    }

    Control BuildHUDButtonBar()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        margin.GrowVertical = Control.GrowDirection.Begin;
        margin.AddThemeConstantOverride("margin_left",   24);
        margin.AddThemeConstantOverride("margin_right",  24);
        margin.AddThemeConstantOverride("margin_top",    12);
        margin.AddThemeConstantOverride("margin_bottom", 16);

        var hbox = new HBoxContainer();
        hbox.Alignment = BoxContainer.AlignmentMode.End;
        hbox.AddThemeConstantOverride("separation", 14);
        margin.AddChild(hbox);

        // Main Menu button
        var menuBtn = new Button();
        menuBtn.Text                    = "← Main Menu";
        menuBtn.Flat                    = true;
        menuBtn.CustomMinimumSize       = new Vector2(140f, 44f);
        menuBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        menuBtn.AddThemeFontSizeOverride("font_size", 14);
        menuBtn.AddThemeColorOverride("font_color",       new Color(0.72f, 0.68f, 0.62f));
        menuBtn.AddThemeColorOverride("font_hover_color", TitleColor);
        menuBtn.Pressed += () => GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
        hbox.AddChild(menuBtn);

        // Start Run button
        var startBtn = new Button();
        startBtn.Text                    = "Start Run  ▶";
        startBtn.CustomMinimumSize       = new Vector2(180f, 48f);
        startBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        startBtn.AddThemeFontSizeOverride("font_size", 18);
        startBtn.AddThemeColorOverride("font_color",       new Color(0.10f, 0.08f, 0.06f));
        startBtn.AddThemeColorOverride("font_hover_color", new Color(0.06f, 0.04f, 0.02f));

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = TitleColor;
        normalStyle.SetCornerRadiusAll(6);
        normalStyle.ContentMarginLeft = normalStyle.ContentMarginRight = 20f;
        normalStyle.ContentMarginTop  = normalStyle.ContentMarginBottom = 10f;

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(1.00f, 0.92f, 0.60f);
        hoverStyle.SetCornerRadiusAll(6);
        hoverStyle.ContentMarginLeft = hoverStyle.ContentMarginRight = 20f;
        hoverStyle.ContentMarginTop  = hoverStyle.ContentMarginBottom = 10f;

        startBtn.AddThemeStyleboxOverride("normal",  normalStyle);
        startBtn.AddThemeStyleboxOverride("hover",   hoverStyle);
        startBtn.AddThemeStyleboxOverride("pressed", normalStyle);
        startBtn.AddThemeStyleboxOverride("focus",   normalStyle);
        startBtn.Pressed += OnStartRun;
        hbox.AddChild(startBtn);

        return margin;
    }

    void OnStartRun()
    {
        RunState.Instance.SetSpells(_loadout);
        GlobalAutoLoad.Reset();
        RunHistoryStore.StartRun();
        GetTree().ChangeSceneToFile("res://levels/World.tscn");
    }

    // ── overlay panel builder ─────────────────────────────────────────────────

    /// <summary>
    /// Wraps <paramref name="content"/> in a full-screen CanvasLayer overlay
    /// with a dark dimmer, titled panel, and a Close button.
    /// The returned layer is hidden by default.
    /// </summary>
    CanvasLayer BuildOverlayPanel(string title, Control content)
    {
        var layer = new CanvasLayer { Layer = 10 };
        layer.Visible = false;

        // Dark backdrop — also blocks mouse from reaching world-space nodes
        var dimmer = new ColorRect();
        dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dimmer.Color       = new Color(0f, 0f, 0f, 0.72f);
        dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
        layer.AddChild(dimmer);

        // Inset container — gives 80px side margins, 50px top/bottom
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left",   80);
        margin.AddThemeConstantOverride("margin_right",  80);
        margin.AddThemeConstantOverride("margin_top",    50);
        margin.AddThemeConstantOverride("margin_bottom", 50);
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;
        layer.AddChild(margin);

        // Panel background
        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = PanelBg;
        panelStyle.SetCornerRadiusAll(8);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor             = PanelBorder;
        panelStyle.ContentMarginLeft       = panelStyle.ContentMarginRight  = 20f;
        panelStyle.ContentMarginTop        = panelStyle.ContentMarginBottom = 16f;

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panel.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        margin.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        panel.AddChild(vbox);

        // Title bar
        var titleBar = new HBoxContainer();
        titleBar.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(titleBar);

        var titleLabel = new Label();
        titleLabel.Text                = title;
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleLabel.AddThemeColorOverride("font_color", TitleColor);
        titleBar.AddChild(titleLabel);

        var closeBtn = new Button();
        closeBtn.Text                    = "✕  Close";
        closeBtn.Flat                    = true;
        closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        closeBtn.AddThemeFontSizeOverride("font_size", 14);
        closeBtn.AddThemeColorOverride("font_color",       new Color(0.72f, 0.68f, 0.62f));
        closeBtn.AddThemeColorOverride("font_hover_color", new Color(0.90f, 0.35f, 0.28f));
        closeBtn.Pressed += CloseAllPanels;
        titleBar.AddChild(closeBtn);

        // Separator
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", SepColor);
        vbox.AddChild(sep);

        // Content fills the rest
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(content);

        return layer;
    }

    // ── interactible factory ──────────────────────────────────────────────────

    Area2D MakeInteractible(string texturePath, Vector2 position,
                             Vector2 scale, float collisionRadius)
    {
        var area = new Area2D();
        area.Position      = position;
        area.InputPickable = true;
        area.Monitoring    = false;
        area.Monitorable   = false;

        var sprite = new Sprite2D();
        sprite.Texture = GD.Load<Texture2D>(texturePath);
        sprite.Scale   = scale;
        area.AddChild(sprite);

        var collision = new CollisionShape2D();
        collision.Shape = new CircleShape2D { Radius = collisionRadius };
        area.AddChild(collision);

        return area;
    }

    static bool IsLeftClick(InputEvent ev) =>
        ev is InputEventMouseButton mb &&
        mb.ButtonIndex == MouseButton.Left &&
        mb.Pressed;

    // ══════════════════════════════════════════════════════════════════════════
    // SPELLBOOK PANE  (same logic as before — now shown inside an overlay)
    // ══════════════════════════════════════════════════════════════════════════

    Control BuildSpellbookPane()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   16);
        margin.AddThemeConstantOverride("margin_right",  16);
        margin.AddThemeConstantOverride("margin_top",    8);
        margin.AddThemeConstantOverride("margin_bottom", 8);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        var libTabs = new TabContainer();
        libTabs.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        libTabs.CustomMinimumSize = new Vector2(0, 280f);
        foreach (var (school, name) in SpellSchoolTabs)
        {
            var pane = BuildSpellLibraryPane(school);
            pane.Name = name;
            libTabs.AddChild(pane);
        }
        vbox.AddChild(libTabs);

        AddHSep(vbox);
        vbox.AddChild(BuildLoadoutRow());

        var hint = new Label();
        hint.Text                = "Click a spell to equip or unequip it  •  Click a loadout slot to clear it";
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", HintColor);
        vbox.AddChild(hint);

        return margin;
    }

    Control BuildSpellLibraryPane(SpellSchool? school)
    {
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   14);
        margin.AddThemeConstantOverride("margin_right",  14);
        margin.AddThemeConstantOverride("margin_top",    14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(margin);

        var flow = new HFlowContainer();
        flow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        flow.AddThemeConstantOverride("h_separation", 10);
        flow.AddThemeConstantOverride("v_separation", 10);
        margin.AddChild(flow);

        var spells = school == null
            ? SpellRegistry.AllSpells
            : SpellRegistry.AllSpells.Where(s => s.School == school).ToList();

        if (spells.Count == 0)
        {
            var empty = new Label();
            empty.Text                = "No spells available yet!";
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            empty.AddThemeFontSizeOverride("font_size", 13);
            empty.AddThemeColorOverride("font_color", HintColor);
            flow.AddChild(empty);
        }
        else
        {
            foreach (var spell in spells)
                flow.AddChild(BuildSpellCard(spell));
        }

        return scroll;
    }

    PanelContainer BuildSpellCard(SpellResource spell)
    {
        var panel = new PanelContainer();
        panel.CustomMinimumSize       = new Vector2(CardW, CardH);
        panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        var border = new StyleBoxFlat();
        border.BgColor = new Color(0.09f, 0.07f, 0.07f, 0.97f);
        border.SetCornerRadiusAll(5);
        border.SetBorderWidthAll(2);
        border.BorderColor             = IsEquipped(spell) ? CardBorderEquipped : CardBorderIdle;
        border.ContentMarginLeft       = border.ContentMarginRight  = 6f;
        border.ContentMarginTop        = border.ContentMarginBottom = 6f;
        panel.AddThemeStyleboxOverride("panel", border);

        _libraryCards[spell.Name ?? spell.GetType().Name] = (panel, border);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panel.AddChild(vbox);

        var iconRect = new TextureRect();
        iconRect.Texture             = spell.Icon;
        iconRect.CustomMinimumSize   = new Vector2(CardIconSz, CardIconSz);
        iconRect.ExpandMode          = TextureRect.ExpandModeEnum.IgnoreSize;
        iconRect.StretchMode         = TextureRect.StretchModeEnum.KeepAspectCentered;
        iconRect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        iconRect.MouseFilter         = Control.MouseFilterEnum.Ignore;
        vbox.AddChild(iconRect);

        var schoolLabel = new Label();
        schoolLabel.Text                = spell.School.ToString();
        schoolLabel.HorizontalAlignment = HorizontalAlignment.Center;
        schoolLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        schoolLabel.AddThemeFontSizeOverride("font_size", 9);
        schoolLabel.AddThemeColorOverride("font_color", SpellSchoolColor(spell.School));
        schoolLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        vbox.AddChild(schoolLabel);

        var nameLabel = new Label();
        nameLabel.Text                = spell.Name ?? "";
        nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        nameLabel.AutowrapMode        = TextServer.AutowrapMode.WordSmart;
        nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.84f, 0.78f));
        nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        vbox.AddChild(nameLabel);

        var tooltip = GameTooltip.FormatSpellTooltip(spell);
        panel.MouseEntered += () =>
        {
            if (!IsEquipped(spell)) border.BorderColor = CardBorderHover;
            GameTooltip.Show(tooltip);
        };
        panel.MouseExited += () =>
        {
            border.BorderColor = IsEquipped(spell) ? CardBorderEquipped : CardBorderIdle;
            GameTooltip.Hide();
        };
        panel.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                ToggleSpell(spell);
                panel.AcceptEvent();
            }
        };

        return panel;
    }

    Control BuildLoadoutRow()
    {
        _loadoutSlots = new (PanelContainer, StyleBoxFlat, TextureRect)[Player.MaxSpellSlots];

        var hbox = new HBoxContainer();
        hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddThemeConstantOverride("separation", 6);

        var label = new Label();
        label.Text                = "Loadout";
        label.VerticalAlignment   = VerticalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
        hbox.AddChild(label);

        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(10f, 0f);
        hbox.AddChild(spacer);

        for (var i = 0; i < Player.MaxSpellSlots; i++)
        {
            var idx = i;
            var (slotPanel, slotBorder, iconRect) = BuildLoadoutSlot(i);
            _loadoutSlots[i] = (slotPanel, slotBorder, iconRect);

            slotPanel.MouseEntered += () =>
            {
                var s = _loadout[idx];
                if (s != null) GameTooltip.Show(GameTooltip.FormatSpellTooltip(s));
            };
            slotPanel.MouseExited  += () => GameTooltip.Hide();
            slotPanel.GuiInput     += (ev) =>
            {
                if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                {
                    ClearLoadoutSlot(idx);
                    slotPanel.AcceptEvent();
                }
            };
            hbox.AddChild(slotPanel);
        }

        var fill = new Control();
        fill.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(fill);

        RefreshSpellVisuals();
        return hbox;
    }

    (PanelContainer, StyleBoxFlat, TextureRect) BuildLoadoutSlot(int index)
    {
        var panel = new PanelContainer();
        panel.CustomMinimumSize       = new Vector2(52f, 52f);
        panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        var border = new StyleBoxFlat();
        border.BgColor = new Color(0.12f, 0.10f, 0.10f, 0.95f);
        border.SetCornerRadiusAll(4);
        border.SetBorderWidthAll(2);
        border.BorderColor             = SlotBorderEmpty;
        border.ContentMarginLeft       = border.ContentMarginRight  = 3f;
        border.ContentMarginTop        = border.ContentMarginBottom = 3f;
        panel.AddThemeStyleboxOverride("panel", border);

        var inner = new Control();
        inner.MouseFilter         = Control.MouseFilterEnum.Ignore;
        inner.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        inner.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        panel.AddChild(inner);

        var iconRect = new TextureRect();
        iconRect.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
        iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        iconRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
        iconRect.Visible     = false;
        inner.AddChild(iconRect);

        var keyLabel = new Label();
        keyLabel.Text = GetKeybindLabel($"spell_{index + 1}");
        keyLabel.AddThemeFontSizeOverride("font_size", 11);
        keyLabel.AddThemeColorOverride("font_color",        new Color(1f, 1f, 0.85f));
        keyLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
        keyLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        keyLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        keyLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        keyLabel.GrowHorizontal = Control.GrowDirection.Begin;
        keyLabel.GrowVertical   = Control.GrowDirection.Begin;
        keyLabel.MouseFilter    = Control.MouseFilterEnum.Ignore;
        inner.AddChild(keyLabel);

        return (panel, border, iconRect);
    }

    // ── spell equip helpers ───────────────────────────────────────────────────

    void ToggleSpell(SpellResource spell)
    {
        var slot = System.Array.FindIndex(_loadout, s => s?.Name == spell.Name);
        if (slot >= 0)
            _loadout[slot] = null;
        else
        {
            var empty = System.Array.FindIndex(_loadout, s => s == null);
            if (empty >= 0) _loadout[empty] = spell;
        }
        RefreshSpellVisuals();
        RunState.Instance.SetSpells(_loadout);
    }

    void ClearLoadoutSlot(int index)
    {
        _loadout[index] = null;
        RefreshSpellVisuals();
        RunState.Instance.SetSpells(_loadout);
    }

    bool IsEquipped(SpellResource spell) =>
        System.Array.FindIndex(_loadout, s => s?.Name == spell.Name) >= 0;

    void RefreshSpellVisuals()
    {
        foreach (var (name, (_, border)) in _libraryCards)
        {
            var equipped = _loadout.Any(s => s?.Name == name);
            border.BorderColor = equipped ? CardBorderEquipped : CardBorderIdle;
        }

        if (_loadoutSlots == null) return;
        for (var i = 0; i < Player.MaxSpellSlots; i++)
        {
            var spell = _loadout[i];
            var (_, border, iconRect) = _loadoutSlots[i];
            iconRect.Texture   = spell?.Icon;
            iconRect.Visible   = spell != null;
            border.BorderColor = spell != null ? SlotBorderFilled : SlotBorderEmpty;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TALENT PANE  (same logic as before — now shown inside an overlay)
    // ══════════════════════════════════════════════════════════════════════════

    Control BuildTalentPane()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   16);
        margin.AddThemeConstantOverride("margin_right",  16);
        margin.AddThemeConstantOverride("margin_top",    8);
        margin.AddThemeConstantOverride("margin_bottom", 8);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var hbox = new HBoxContainer();
        hbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        hbox.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(hbox);

        for (var i = 0; i < TalentSchoolOrder.Length; i++)
        {
            if (i > 0)
            {
                var vsep = new VSeparator();
                vsep.AddThemeColorOverride("color", SepColor);
                vsep.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                hbox.AddChild(vsep);
            }
            var (school, name, accent) = TalentSchoolOrder[i];
            hbox.AddChild(BuildTalentSchoolColumn(school, name, accent));
        }

        vbox.AddChild(scroll);

        var hint = new Label();
        hint.Text                = "Click to select a talent  •  Each row requires a selection in the row above";
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", HintColor);
        vbox.AddChild(hint);

        return margin;
    }

    Control BuildTalentSchoolColumn(SpellSchool school, string colName, Color accent)
    {
        var margin = new MarginContainer();
        margin.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        margin.AddThemeConstantOverride("margin_left",   20);
        margin.AddThemeConstantOverride("margin_right",  20);
        margin.AddThemeConstantOverride("margin_top",    16);
        margin.AddThemeConstantOverride("margin_bottom", 16);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        col.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        margin.AddChild(col);

        var header = new Label();
        header.Text                = colName;
        header.HorizontalAlignment = HorizontalAlignment.Center;
        header.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddThemeFontSizeOverride("font_size", 16);
        header.AddThemeColorOverride("font_color", accent);
        col.AddChild(header);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", new Color(accent.R, accent.G, accent.B, 0.45f));
        col.AddChild(sep);

        var rowGroups = TalentRegistry.AllTalents
            .Where(t => t.School == school)
            .GroupBy(t => t.TalentRow)
            .OrderBy(g => g.Key)
            .ToList();

        if (rowGroups.Count == 0)
        {
            var empty = new Label();
            empty.Text                = "Coming soon!";
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            empty.AddThemeFontSizeOverride("font_size", 12);
            empty.AddThemeColorOverride("font_color", HintColor);
            col.AddChild(empty);
            return margin;
        }

        for (var i = 0; i < rowGroups.Count; i++)
        {
            var rowGroup = rowGroups[i];
            var rowIndex = rowGroup.Key;

            var rowBox = new HBoxContainer();
            rowBox.Alignment           = BoxContainer.AlignmentMode.Center;
            rowBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            rowBox.AddThemeConstantOverride("separation", 12);

            foreach (var def in rowGroup)
            {
                var slot = new TalentSlot(def);
                slot.Toggled += OnTalentSlotToggled;
                _talentSlots.Add(slot);

                if (!_talentsBySchoolRow.ContainsKey(school))
                    _talentsBySchoolRow[school] = new Dictionary<int, List<TalentSlot>>();
                if (!_talentsBySchoolRow[school].ContainsKey(rowIndex))
                    _talentsBySchoolRow[school][rowIndex] = new List<TalentSlot>();
                _talentsBySchoolRow[school][rowIndex].Add(slot);

                rowBox.AddChild(slot);
            }

            col.AddChild(rowBox);

            if (i < rowGroups.Count - 1)
            {
                var arrow = new Label();
                arrow.Text                = "▼";
                arrow.HorizontalAlignment = HorizontalAlignment.Center;
                arrow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                arrow.AddThemeFontSizeOverride("font_size", 18);
                arrow.AddThemeColorOverride("font_color", ArrowColor);
                col.AddChild(arrow);
            }
        }

        return margin;
    }

    // ── talent helpers ────────────────────────────────────────────────────────

    void OnTalentSlotToggled(TalentSlot slot)
    {
        ValidateTalentTree(slot.Definition.School);
        CommitTalentsToRunState();
    }

    void ValidateTalentTree(SpellSchool school)
    {
        if (!_talentsBySchoolRow.TryGetValue(school, out var rowDict)) return;
        foreach (var row in rowDict.Keys.OrderBy(r => r))
        {
            var unlocked = row == 0
                || (rowDict.TryGetValue(row - 1, out var prev) && prev.Any(s => s.IsSelected));
            foreach (var slot in rowDict[row])
            {
                if (!unlocked && slot.IsSelected) slot.SetSelected(false);
                slot.SetLocked(!unlocked);
            }
        }
    }

    void CommitTalentsToRunState()
    {
        RunState.Instance.SetTalents(
            _talentSlots.Where(s => s.IsSelected).Select(s => s.Definition));
    }

    void SyncTalentSlotsFromRunState()
    {
        var active = new HashSet<string>(
            RunState.Instance.SelectedTalentDefs.Select(d => d.Name));
        foreach (var slot in _talentSlots)
            slot.SetSelected(active.Contains(slot.Definition.Name));
        foreach (var (school, _, _) in TalentSchoolOrder)
            ValidateTalentTree(school);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RUN HISTORY PANE
    // ══════════════════════════════════════════════════════════════════════════

    Control BuildRunHistoryPane()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   16);
        margin.AddThemeConstantOverride("margin_right",  16);
        margin.AddThemeConstantOverride("margin_top",    8);
        margin.AddThemeConstantOverride("margin_bottom", 8);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        margin.AddChild(scroll);

        _historyContent = new VBoxContainer();
        _historyContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _historyContent.AddThemeConstantOverride("separation", 16);
        scroll.AddChild(_historyContent);

        // Populated lazily when the panel opens via RebuildHistoryContent()
        RebuildHistoryContent();

        return margin;
    }

    void RebuildHistoryContent()
    {
        if (_historyContent == null) return;

        foreach (var child in _historyContent.GetChildren())
            child.QueueFree();

        var runs = RunHistoryStore.History;

        if (runs.Count == 0)
        {
            var empty = new Label();
            empty.Text                = "No runs recorded yet.\nComplete or attempt a run to see your history here.";
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.AutowrapMode        = TextServer.AutowrapMode.Word;
            empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            empty.AddThemeFontSizeOverride("font_size", 15);
            empty.AddThemeColorOverride("font_color", HintColor);
            _historyContent.AddChild(empty);
            return;
        }

        // Newest runs first
        for (var i = runs.Count - 1; i >= 0; i--)
        {
            _historyContent.AddChild(BuildRunEntry(i + 1, runs[i]));
            if (i > 0)
            {
                var sep = new HSeparator();
                sep.AddThemeColorOverride("color", SepColor);
                _historyContent.AddChild(sep);
            }
        }
    }

    Control BuildRunEntry(int runNumber, RunHistoryStore.RunRecord run)
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // ── Header row ────────────────────────────────────────────────────────
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(header);

        var runLabel = new Label();
        runLabel.Text = $"Run #{runNumber}  •  {run.CompletedAt:MMM d, yyyy  h:mm tt}";
        runLabel.AddThemeFontSizeOverride("font_size", 14);
        runLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.78f, 0.72f));
        runLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(runLabel);

        // Duration
        var durationLabel = new Label();
        var m = (int)run.Duration.TotalMinutes;
        var s = run.Duration.Seconds;
        durationLabel.Text = $"{m}:{s:D2}";
        durationLabel.AddThemeFontSizeOverride("font_size", 13);
        durationLabel.AddThemeColorOverride("font_color", HintColor);
        header.AddChild(durationLabel);

        // Outcome badge
        var outcome = new Label();
        outcome.Text = run.IsVictory ? "VICTORY" : "DEFEAT";
        outcome.AddThemeFontSizeOverride("font_size", 14);
        outcome.AddThemeColorOverride("font_color",
            run.IsVictory ? new Color(0.40f, 0.85f, 0.35f)
                          : new Color(0.85f, 0.28f, 0.22f));
        header.AddChild(outcome);

        // ── Per-boss rows ─────────────────────────────────────────────────────
        foreach (var enc in run.BossEncounters)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 16);
            vbox.AddChild(row);

            var pad = new Control();
            pad.CustomMinimumSize = new Vector2(24f, 0f);
            row.AddChild(pad);

            var bossName = new Label();
            bossName.Text                = enc.BossName;
            bossName.CustomMinimumSize   = new Vector2(160f, 0f);
            bossName.AddThemeFontSizeOverride("font_size", 13);
            bossName.AddThemeColorOverride("font_color", new Color(0.88f, 0.84f, 0.78f));
            row.AddChild(bossName);

            var healLabel = new Label();
            healLabel.Text = $"Healing: {enc.TotalHealing:N0}";
            healLabel.AddThemeFontSizeOverride("font_size", 13);
            healLabel.AddThemeColorOverride("font_color", new Color(0.40f, 0.85f, 0.55f));
            row.AddChild(healLabel);

            var dmgLabel = new Label();
            dmgLabel.Text = $"Damage: {enc.TotalDamage:N0}";
            dmgLabel.AddThemeFontSizeOverride("font_size", 13);
            dmgLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.44f, 0.28f));
            row.AddChild(dmgLabel);
        }

        return vbox;
    }

    // ── shared helpers ────────────────────────────────────────────────────────

    void AddHSep(VBoxContainer parent)
    {
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", SepColor);
        parent.AddChild(sep);
    }

    static string GetKeybindLabel(string actionName)
    {
        var events = InputMap.ActionGetEvents(actionName);
        if (events.Count > 0 && events[0] is InputEventKey key)
            return OS.GetKeycodeString(key.PhysicalKeycode);
        return actionName.StartsWith("spell_") ? actionName["spell_".Length..] : actionName;
    }

    static Color SpellSchoolColor(SpellSchool school) => school switch
    {
        SpellSchool.Holy        => new Color(0.95f, 0.85f, 0.40f),
        SpellSchool.Nature      => new Color(0.40f, 0.80f, 0.35f),
        SpellSchool.Void        => new Color(0.65f, 0.35f, 0.85f),
        SpellSchool.Chronomancy => new Color(0.35f, 0.75f, 0.90f),
        _                       => new Color(0.70f, 0.65f, 0.60f),
    };
}
