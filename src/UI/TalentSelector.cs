using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.Talents;

/// <summary>
/// Full-screen talent selection panel.
///
/// • Press T (or click ✕) to open/close.
/// • While open the game is paused so the player cannot cast spells.
/// • All schools are displayed side-by-side in a single scrollable view
///   so the player can compare talents across schools at a glance.
///   Each school column has a coloured header and a per-school accent
///   separator; columns are divided by vertical rules.
/// • Within each school, talents are arranged in numbered rows.
///   A talent at row N is locked until at least one talent in row N−1
///   of the same school is selected — cascading automatically when
///   higher rows are deselected.
/// • Selecting / deselecting updates the player's <see cref="Character.Talents"/>
///   list when the panel closes.
///
/// Wiring: call <see cref="Init"/> from <c>World._Ready</c> after the
/// Player node is resolved, then add this node as a child of the scene root.
/// </summary>
public partial class TalentSelector : CanvasLayer
{
    // ── colours ───────────────────────────────────────────────────────────────
    static readonly Color OverlayBg   = new(0.00f, 0.00f, 0.00f, 0.72f);
    static readonly Color PanelBg     = new(0.10f, 0.08f, 0.07f, 0.98f);
    static readonly Color PanelBorder = new(0.65f, 0.52f, 0.28f);
    static readonly Color TitleColor  = new(0.95f, 0.84f, 0.50f);
    static readonly Color HintColor   = new(0.45f, 0.42f, 0.38f);
    static readonly Color SepColor    = new(0.50f, 0.40f, 0.22f, 0.55f);
    static readonly Color ArrowColor  = new(0.45f, 0.40f, 0.35f, 0.75f);

    /// <summary>School display order and per-school accent colours.</summary>
    static readonly (SpellSchool School, string Name, Color Accent)[] SchoolOrder =
    {
        (SpellSchool.Generic,     "General",     new Color(0.70f, 0.65f, 0.60f)),
        (SpellSchool.Holy,        "Holy",         new Color(0.95f, 0.85f, 0.40f)),
        (SpellSchool.Nature,      "Nature",       new Color(0.40f, 0.80f, 0.35f)),
        (SpellSchool.Void,        "Void",         new Color(0.65f, 0.35f, 0.85f)),
        (SpellSchool.Chronomancy, "Chronomancy",  new Color(0.35f, 0.75f, 0.90f)),
    };

    // ── state ─────────────────────────────────────────────────────────────────
    Player  _player;
    Control _overlay;
    bool    _isOpen;

    /// <summary>Flat list of every slot — used for sync and apply passes.</summary>
    readonly List<TalentSlot> _slots = new();

    /// <summary>
    /// Structured lookup used for unlock validation:
    /// school → row index → slots in that row.
    /// </summary>
    readonly Dictionary<SpellSchool, Dictionary<int, List<TalentSlot>>> _slotsBySchoolAndRow = new();

    // ── public API ────────────────────────────────────────────────────────────
    public void Init(Player player) => _player = player;

    // ── lifecycle ─────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        Layer = 15;
        ProcessMode = ProcessModeEnum.Always;

        _overlay = BuildOverlay();
        AddChild(_overlay);
        _overlay.Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

        if (key.PhysicalKeycode == Key.T)
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
        else if (key.PhysicalKeycode == Key.Escape && _isOpen)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // ── open / close ──────────────────────────────────────────────────────────
    void Toggle() { if (_isOpen) Close(); else Open(); }

    void Open()
    {
        if (_player == null) return;
        if (GetTree().Paused) return; // another panel is already open
        SyncSlotsFromPlayer();
        _isOpen = true;
        _overlay.Visible = true;
        GetTree().Paused = true;
    }

    void Close()
    {
        _isOpen = false;
        _overlay.Visible = false;
        GetTree().Paused = false;
        ApplyTalentsToPlayer();
    }

    // ── talent synchronisation ─────────────────────────────────────────────────
    void SyncSlotsFromPlayer()
    {
        var active = new HashSet<string>();
        foreach (var t in _player.Talents)
            active.Add(t.Name);

        foreach (var slot in _slots)
            slot.SetSelected(active.Contains(slot.Definition.Name));

        foreach (var (school, _, _) in SchoolOrder)
            ValidateTree(school);
    }

    void ApplyTalentsToPlayer()
    {
        _player.Talents.Clear();
        foreach (var slot in _slots)
            if (slot.IsSelected)
                _player.Talents.Add(slot.Definition.CreateTalent());
    }

    // ── unlock validation ──────────────────────────────────────────────────────
    /// <summary>
    /// Recalculates locked states for every row in <paramref name="school"/>'s tree.
    /// Row 0 is always unlocked; row N requires at least one selection in row N−1.
    /// Processing ascending rows cascades correctly in a single pass.
    /// </summary>
    void ValidateTree(SpellSchool school)
    {
        if (!_slotsBySchoolAndRow.TryGetValue(school, out var rowDict)) return;

        foreach (var row in rowDict.Keys.OrderBy(r => r))
        {
            var unlocked = row == 0
                || (rowDict.TryGetValue(row - 1, out var prevRow) && prevRow.Any(s => s.IsSelected));

            foreach (var slot in rowDict[row])
            {
                if (!unlocked && slot.IsSelected)
                    slot.SetSelected(false);
                slot.SetLocked(!unlocked);
            }
        }
    }

    void OnSlotToggled(TalentSlot slot) => ValidateTree(slot.Definition.School);

    // ── UI construction ───────────────────────────────────────────────────────
    Control BuildOverlay()
    {
        var overlay = new ColorRect();
        overlay.Color = OverlayBg;
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;
        overlay.AddChild(BuildPanel());
        return overlay;
    }

    PanelContainer BuildPanel()
    {
        var panel = new PanelContainer();

        var style = new StyleBoxFlat();
        style.BgColor = PanelBg;
        style.SetCornerRadiusAll(10);
        style.SetBorderWidthAll(2);
        style.BorderColor             = PanelBorder;
        style.ContentMarginLeft       = 24f;
        style.ContentMarginRight      = 24f;
        style.ContentMarginTop        = 18f;
        style.ContentMarginBottom     = 24f;
        panel.AddThemeStyleboxOverride("panel", style);

        // Fill the viewport with a comfortable margin on each side
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.OffsetLeft   =  40f;
        panel.OffsetRight  = -40f;
        panel.OffsetTop    =  30f;
        panel.OffsetBottom = -30f;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vbox);

        // ── Title row ────────────────────────────────────────────────────────
        vbox.AddChild(BuildTitleRow());
        AddSeparator(vbox, SepColor);

        // ── All schools side-by-side ─────────────────────────────────────────
        // The scroll container expands to fill the space between title and footer.
        var schoolView = BuildSchoolColumns();
        schoolView.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        vbox.AddChild(schoolView);

        AddSeparator(vbox, SepColor);

        // ── Footer hint ──────────────────────────────────────────────────────
        var hint = new Label();
        hint.Text = "Click to select a talent  •  Each row requires a selection in the row above  •  [T] or [Esc] to close";
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", HintColor);
        vbox.AddChild(hint);

        return panel;
    }

    // ── school column layout ──────────────────────────────────────────────────

    /// <summary>
    /// Returns a <see cref="ScrollContainer"/> containing all school columns
    /// laid out horizontally, separated by vertical rules.
    /// </summary>
    Control BuildSchoolColumns()
    {
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;

        var hbox = new HBoxContainer();
        hbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        hbox.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(hbox);

        for (var i = 0; i < SchoolOrder.Length; i++)
        {
            if (i > 0)
            {
                // Vertical rule between schools
                var vsep = new VSeparator();
                vsep.AddThemeColorOverride("color", SepColor);
                vsep.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                hbox.AddChild(vsep);
            }

            var (school, name, accent) = SchoolOrder[i];
            hbox.AddChild(BuildSchoolColumn(school, name, accent));
        }

        return scroll;
    }

    /// <summary>
    /// Builds one school column: a coloured header, a tinted separator,
    /// then talent rows separated by ▼ connectors.
    /// All created slots are registered in <see cref="_slots"/> and
    /// <see cref="_slotsBySchoolAndRow"/> for later sync / validation.
    /// </summary>
    Control BuildSchoolColumn(SpellSchool school, string colName, Color accent)
    {
        // Margin container gives each column breathing room
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

        // ── School header ─────────────────────────────────────────────────────
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

        // ── Talent rows ───────────────────────────────────────────────────────
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

            // Row of talent slots
            var hbox = new HBoxContainer();
            hbox.Alignment           = BoxContainer.AlignmentMode.Center;
            hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hbox.AddThemeConstantOverride("separation", 12);

            foreach (var def in rowGroup)
            {
                var slot = new TalentSlot(def);
                slot.Toggled += OnSlotToggled;
                _slots.Add(slot);

                if (!_slotsBySchoolAndRow.ContainsKey(school))
                    _slotsBySchoolAndRow[school] = new Dictionary<int, List<TalentSlot>>();
                if (!_slotsBySchoolAndRow[school].ContainsKey(rowIndex))
                    _slotsBySchoolAndRow[school][rowIndex] = new List<TalentSlot>();
                _slotsBySchoolAndRow[school][rowIndex].Add(slot);

                hbox.AddChild(slot);
            }

            col.AddChild(hbox);

            // ▼ connector between rows
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

    // ── title row ─────────────────────────────────────────────────────────────
    Control BuildTitleRow()
    {
        var hbox = new HBoxContainer();

        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(28, 0);
        hbox.AddChild(spacer);

        var title = new Label();
        title.Text                = "Talents";
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", TitleColor);
        hbox.AddChild(title);

        var closeBtn = new Button();
        closeBtn.Text                    = "✕";
        closeBtn.Flat                    = true;
        closeBtn.CustomMinimumSize       = new Vector2(28, 28);
        closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        closeBtn.AddThemeFontSizeOverride("font_size", 16);
        closeBtn.AddThemeColorOverride("font_color",       new Color(0.72f, 0.68f, 0.62f));
        closeBtn.AddThemeColorOverride("font_hover_color", new Color(1.00f, 0.90f, 0.55f));
        closeBtn.Pressed += Close;
        hbox.AddChild(closeBtn);

        return hbox;
    }

    void AddSeparator(VBoxContainer parent, Color color)
    {
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", color);
        parent.AddChild(sep);
    }
}
