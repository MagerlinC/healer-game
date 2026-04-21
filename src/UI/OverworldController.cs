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
/// Provides a pre-run loadout screen with two tabs:
///   • Spellbook — browse the spell library and assign up to 6 spells.
///   • Talents   — select talents across all schools (same tree as in-game).
///
/// All selections write directly to <see cref="RunState"/> in real-time.
/// The "Start Run" button calls <see cref="GlobalAutoLoad.Reset"/> (clearing
/// any stale signal state from a previous run) then loads World.tscn.
/// </summary>
public partial class OverworldController : Node2D
{
    // ── shared colours (mirror the in-game panel palette) ─────────────────────
    static readonly Color BgColor       = new(0.07f, 0.06f, 0.06f);
    static readonly Color PanelBg       = new(0.10f, 0.08f, 0.07f, 0.98f);
    static readonly Color PanelBorder   = new(0.65f, 0.52f, 0.28f);
    static readonly Color TitleColor    = new(0.95f, 0.84f, 0.50f);
    static readonly Color HintColor     = new(0.45f, 0.42f, 0.38f);
    static readonly Color SepColor      = new(0.50f, 0.40f, 0.22f, 0.55f);
    static readonly Color ArrowColor    = new(0.45f, 0.40f, 0.35f, 0.75f);

    // ── spell panel colours ───────────────────────────────────────────────────
    static readonly Color CardBorderIdle     = new(0.28f, 0.22f, 0.16f);
    static readonly Color CardBorderHover    = new(0.70f, 0.58f, 0.30f);
    static readonly Color CardBorderEquipped = new(0.98f, 0.82f, 0.15f);
    static readonly Color SlotBorderEmpty    = new(0.22f, 0.18f, 0.14f);
    static readonly Color SlotBorderFilled   = new(0.60f, 0.48f, 0.22f);

    const float CardW       = 92f;
    const float CardH       = 116f;
    const float CardIconSz  = 64f;

    // ── school definitions ────────────────────────────────────────────────────
    static readonly (SpellSchool? School, string Name)[] SpellSchoolTabs =
    {
        (null,                   "All"),
        (SpellSchool.Holy,       "Holy"),
        (SpellSchool.Nature,     "Nature"),
        (SpellSchool.Void,       "Void"),
        (SpellSchool.Chronomancy,"Chronomancy"),
    };

    static readonly (SpellSchool School, string Name, Color Accent)[] TalentSchoolOrder =
    {
        (SpellSchool.Generic,     "General",     new Color(0.70f, 0.65f, 0.60f)),
        (SpellSchool.Holy,        "Holy",         new Color(0.95f, 0.85f, 0.40f)),
        (SpellSchool.Nature,      "Nature",       new Color(0.40f, 0.80f, 0.35f)),
        (SpellSchool.Void,        "Void",         new Color(0.65f, 0.35f, 0.85f)),
        (SpellSchool.Chronomancy, "Chronomancy",  new Color(0.35f, 0.75f, 0.90f)),
    };

    // ── runtime state ─────────────────────────────────────────────────────────

    /// <summary>Working copy of the spell loadout. Written to RunState on every change.</summary>
    readonly SpellResource?[] _loadout = new SpellResource?[Player.MaxSpellSlots];

    /// <summary>Library card UI refs, keyed by spell name for quick visual refresh.</summary>
    readonly Dictionary<string, (PanelContainer Panel, StyleBoxFlat Border)> _libraryCards = new();

    /// <summary>Loadout slot UI refs, one per index.</summary>
    (PanelContainer Panel, StyleBoxFlat Border, TextureRect Icon)[]? _loadoutSlots;

    /// <summary>All talent slots — used for sync and ValidateTree passes.</summary>
    readonly List<TalentSlot> _talentSlots = new();

    /// <summary>school → row → slots, for unlock validation.</summary>
    readonly Dictionary<SpellSchool, Dictionary<int, List<TalentSlot>>> _talentsBySchoolRow = new();

    // ── lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        // Initialise working loadout from RunState
        System.Array.Copy(RunState.Instance.SelectedSpells, _loadout, Player.MaxSpellSlots);

        // Tooltip singleton must exist so spell/talent cards can show tooltips
        AddChild(new GameTooltip());

        var canvas = new CanvasLayer();
        AddChild(canvas);

        // Background
        var bg = new ColorRect();
        bg.Color       = BgColor;
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.MouseFilter = Control.MouseFilterEnum.Stop;
        canvas.AddChild(bg);

        // Root layout: top bar | tabs | bottom bar
        var root = new VBoxContainer();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        bg.AddChild(root);

        root.AddChild(BuildTopBar());
        root.AddChild(BuildTabs());
        root.AddChild(BuildBottomBar());

        // Sync talent slots from RunState (in case player re-visits the Overworld)
        SyncTalentSlotsFromRunState();
    }

    // ── top bar ───────────────────────────────────────────────────────────────

    Control BuildTopBar()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   32);
        margin.AddThemeConstantOverride("margin_right",  32);
        margin.AddThemeConstantOverride("margin_top",    20);
        margin.AddThemeConstantOverride("margin_bottom", 12);

        var hbox = new HBoxContainer();
        margin.AddChild(hbox);

        var title = new Label();
        title.Text                = "PREPARE FOR BATTLE";
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", TitleColor);
        hbox.AddChild(title);

        var menuBtn = new Button();
        menuBtn.Text                    = "← Main Menu";
        menuBtn.Flat                    = true;
        menuBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        menuBtn.AddThemeFontSizeOverride("font_size", 14);
        menuBtn.AddThemeColorOverride("font_color",       new Color(0.72f, 0.68f, 0.62f));
        menuBtn.AddThemeColorOverride("font_hover_color", TitleColor);
        menuBtn.Pressed += () => GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
        hbox.AddChild(menuBtn);

        return margin;
    }

    // ── tab container ─────────────────────────────────────────────────────────

    Control BuildTabs()
    {
        var tabs = new TabContainer();
        tabs.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        // Give the tabs header a matching dark style
        var tabStyle = new StyleBoxFlat();
        tabStyle.BgColor = new Color(0.12f, 0.10f, 0.09f);
        tabs.AddThemeStyleboxOverride("panel", tabStyle);

        var spellPane = BuildSpellbookPane();
        spellPane.Name = "Spellbook";
        tabs.AddChild(spellPane);

        var talentPane = BuildTalentPane();
        talentPane.Name = "Talents";
        tabs.AddChild(talentPane);

        return tabs;
    }

    // ── bottom bar ────────────────────────────────────────────────────────────

    Control BuildBottomBar()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   32);
        margin.AddThemeConstantOverride("margin_right",  32);
        margin.AddThemeConstantOverride("margin_top",    12);
        margin.AddThemeConstantOverride("margin_bottom", 20);

        var hbox = new HBoxContainer();
        margin.AddChild(hbox);

        var fill = new Control();
        fill.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(fill);

        var startBtn = new Button();
        startBtn.Text                    = "Start Run  ▶";
        startBtn.CustomMinimumSize       = new Vector2(200f, 52f);
        startBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        startBtn.AddThemeFontSizeOverride("font_size", 20);
        startBtn.AddThemeColorOverride("font_color",       new Color(0.10f, 0.08f, 0.06f));
        startBtn.AddThemeColorOverride("font_hover_color", new Color(0.06f, 0.04f, 0.02f));

        var normalStyle = new StyleBoxFlat();
        normalStyle.BgColor = TitleColor;
        normalStyle.SetCornerRadiusAll(6);
        normalStyle.SetBorderWidthAll(0);
        normalStyle.ContentMarginLeft = normalStyle.ContentMarginRight = 24f;
        normalStyle.ContentMarginTop  = normalStyle.ContentMarginBottom = 12f;

        var hoverStyle = new StyleBoxFlat();
        hoverStyle.BgColor = new Color(1.00f, 0.92f, 0.60f);
        hoverStyle.SetCornerRadiusAll(6);
        hoverStyle.ContentMarginLeft = hoverStyle.ContentMarginRight = 24f;
        hoverStyle.ContentMarginTop  = hoverStyle.ContentMarginBottom = 12f;

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
        // Commit loadout to RunState (talents are already synced in real-time)
        RunState.Instance.SetSpells(_loadout);

        // Clear any stale signal registrations from a previous run
        GlobalAutoLoad.Reset();

        GetTree().ChangeSceneToFile("res://levels/World.tscn");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SPELLBOOK PANE
    // ══════════════════════════════════════════════════════════════════════════

    Control BuildSpellbookPane()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   24);
        margin.AddThemeConstantOverride("margin_right",  24);
        margin.AddThemeConstantOverride("margin_top",    16);
        margin.AddThemeConstantOverride("margin_bottom", 16);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        // Library tabs
        var libTabs = new TabContainer();
        libTabs.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        libTabs.CustomMinimumSize = new Vector2(0, 300f);
        foreach (var (school, name) in SpellSchoolTabs)
        {
            var pane = BuildSpellLibraryPane(school);
            pane.Name = name;
            libTabs.AddChild(pane);
        }
        vbox.AddChild(libTabs);

        AddHSep(vbox);

        // Loadout row
        vbox.AddChild(BuildLoadoutRow());

        // Hint
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
        iconRect.Texture           = spell.Icon;
        iconRect.CustomMinimumSize = new Vector2(CardIconSz, CardIconSz);
        iconRect.ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize;
        iconRect.StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered;
        iconRect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        iconRect.MouseFilter       = Control.MouseFilterEnum.Ignore;
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
            slotPanel.MouseExited += () => GameTooltip.Hide();
            slotPanel.GuiInput += (ev) =>
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
        inner.MouseFilter            = Control.MouseFilterEnum.Ignore;
        inner.SizeFlagsHorizontal    = Control.SizeFlags.ExpandFill;
        inner.SizeFlagsVertical      = Control.SizeFlags.ExpandFill;
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
    // TALENT PANE
    // ══════════════════════════════════════════════════════════════════════════

    Control BuildTalentPane()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   24);
        margin.AddThemeConstantOverride("margin_right",  24);
        margin.AddThemeConstantOverride("margin_top",    16);
        margin.AddThemeConstantOverride("margin_bottom", 16);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        // School columns in a scroll view
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

            var hbox = new HBoxContainer();
            hbox.Alignment           = BoxContainer.AlignmentMode.Center;
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddThemeConstantOverride("separation", 12);

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

                hbox.AddChild(slot);
            }

            col.AddChild(hbox);

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
        var active = new HashSet<string>(RunState.Instance.SelectedTalentDefs.Select(d => d.Name));
        foreach (var slot in _talentSlots)
            slot.SetSelected(active.Contains(slot.Definition.Name));
        foreach (var (school, _, _) in TalentSchoolOrder)
            ValidateTalentTree(school);
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
