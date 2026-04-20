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
/// • Talents are organised into per-school tabs (General / Holy / Nature /
///   Void / Chronomancy). Within each tab, talents are arranged in numbered
///   rows. A talent at row N is locked until at least one talent in row N−1
///   of the same school is selected — cascading automatically when higher
///   rows are unlocked.
/// • Selecting / deselecting updates the player's <see cref="Character.Talents"/>
///   list when the panel closes.
///
/// Wiring: call <see cref="Init"/> from <c>World._Ready</c> after the
/// Player node is resolved, then add this node as a child of the scene root.
/// </summary>
public partial class TalentSelector : CanvasLayer
{
    // ── colours / sizes ──────────────────────────────────────────────────────
    static readonly Color OverlayBg    = new(0.00f, 0.00f, 0.00f, 0.72f);
    static readonly Color PanelBg      = new(0.10f, 0.08f, 0.07f, 0.98f);
    static readonly Color PanelBorder  = new(0.65f, 0.52f, 0.28f);
    static readonly Color TitleColor   = new(0.95f, 0.84f, 0.50f);
    static readonly Color HintColor    = new(0.45f, 0.42f, 0.38f);
    static readonly Color SepColor     = new(0.50f, 0.40f, 0.22f, 0.55f);
    static readonly Color ArrowColor   = new(0.45f, 0.40f, 0.35f, 0.75f);

    /// <summary>School display order, tab names, and per-school accent colours.</summary>
    static readonly (SpellSchool School, string TabName, Color Accent)[] SchoolOrder =
    {
        (SpellSchool.Generic,     "General",      new Color(0.70f, 0.65f, 0.60f)),
        (SpellSchool.Holy,        "Holy",          new Color(0.95f, 0.85f, 0.40f)),
        (SpellSchool.Nature,      "Nature",        new Color(0.40f, 0.80f, 0.35f)),
        (SpellSchool.Void,        "Void",          new Color(0.65f, 0.35f, 0.85f)),
        (SpellSchool.Chronomancy, "Chronomancy",   new Color(0.35f, 0.75f, 0.90f)),
    };

    // ── state ────────────────────────────────────────────────────────────────
    Player  _player;
    Control _overlay;
    bool    _isOpen;

    /// <summary>Flat list of every slot — used for sync and apply passes.</summary>
    readonly List<TalentSlot> _slots = new();

    /// <summary>
    /// Structured lookup used for unlock validation:
    /// school → row index → slots in that row.
    /// Built once during <see cref="BuildSchoolPane"/>.
    /// </summary>
    readonly Dictionary<SpellSchool, Dictionary<int, List<TalentSlot>>> _slotsBySchoolAndRow = new();

    // ── public API ───────────────────────────────────────────────────────────
    /// <summary>
    /// Must be called once after the node is added to the scene tree.
    /// Links the panel to the <paramref name="player"/> whose talents it manages.
    /// </summary>
    public void Init(Player player)
    {
        _player = player;
    }

    // ── lifecycle ────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        // Layer above GameUI (10) so the talent panel covers everything.
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

    // ── open / close ─────────────────────────────────────────────────────────
    void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    void Open()
    {
        if (_player == null) return;
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

    // ── talent synchronisation ────────────────────────────────────────────────
    void SyncSlotsFromPlayer()
    {
        var active = new HashSet<string>();
        foreach (var t in _player.Talents)
            active.Add(t.Name);

        foreach (var slot in _slots)
            slot.SetSelected(active.Contains(slot.Definition.Name));

        // Re-validate every tree so locked states match the current selection
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

    // ── unlock validation ─────────────────────────────────────────────────────

    /// <summary>
    /// Recalculates locked states for every row in <paramref name="school"/>'s tree.
    ///
    /// <para>Row 0 is always unlocked. Row N (N &gt; 0) is unlocked if and only if
    /// at least one slot in row N−1 of the same school is currently selected.</para>
    ///
    /// <para>Processing rows in ascending order means a single pass cascades
    /// correctly: if row 1 becomes locked (deselected), the check for row 2
    /// immediately sees no selected row-1 slots and locks row 2 as well.</para>
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
                // Deselect before locking so ApplyVisuals sees the correct state
                if (!unlocked && slot.IsSelected)
                    slot.SetSelected(false);

                slot.SetLocked(!unlocked);
            }
        }
    }

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
        style.BorderColor = PanelBorder;
        style.ContentMarginLeft   = 24f;
        style.ContentMarginRight  = 24f;
        style.ContentMarginTop    = 18f;
        style.ContentMarginBottom = 24f;
        panel.AddThemeStyleboxOverride("panel", style);

        // Anchor to centre and grow outward to fit content
        panel.AnchorLeft   = 0.5f;
        panel.AnchorRight  = 0.5f;
        panel.AnchorTop    = 0.5f;
        panel.AnchorBottom = 0.5f;
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical   = Control.GrowDirection.Both;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vbox);

        // ── Title row ────────────────────────────────────────────────────────
        vbox.AddChild(BuildTitleRow());

        // ── Gold separator ───────────────────────────────────────────────────
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", SepColor);
        vbox.AddChild(sep);

        // ── School tabs ──────────────────────────────────────────────────────
        var tabs = new TabContainer();
        tabs.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        tabs.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        // Minimum size ensures the panel is large enough for two rows of slots
        // even when a tab contains only one row.
        tabs.CustomMinimumSize = new Vector2(800f, 580f);
        vbox.AddChild(tabs);

        foreach (var (school, tabName, accent) in SchoolOrder)
        {
            var pane = BuildSchoolPane(school, accent);
            pane.Name = tabName;
            tabs.AddChild(pane);
        }

        // ── Footer hint ──────────────────────────────────────────────────────
        var hint = new Label();
        hint.Text = "Click to select a talent  •  Each row requires a selection in the row above  •  [T] or [Esc] to close";
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", HintColor);
        vbox.AddChild(hint);

        return panel;
    }

    /// <summary>
    /// Builds the scroll pane for a single school tab.
    /// Talents are grouped into horizontal rows by <see cref="TalentDefinition.TalentRow"/>,
    /// sorted ascending. A ▼ connector is inserted between rows.
    /// All slots are registered in <see cref="_slots"/> and <see cref="_slotsBySchoolAndRow"/>.
    /// </summary>
    Control BuildSchoolPane(SpellSchool school, Color accentColor)
    {
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;

        var outer = new VBoxContainer();
        outer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        outer.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(outer);

        // Top padding
        var topPad = new Control();
        topPad.CustomMinimumSize = new Vector2(0f, 16f);
        outer.AddChild(topPad);

        var rowGroups = TalentRegistry.AllTalents
            .Where(t => t.School == school)
            .GroupBy(t => t.TalentRow)
            .OrderBy(g => g.Key)
            .ToList();

        if (rowGroups.Count == 0)
        {
            var empty = new Label();
            empty.Text                = "No talents yet — coming soon!";
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            empty.AddThemeFontSizeOverride("font_size", 13);
            empty.AddThemeColorOverride("font_color", HintColor);
            outer.AddChild(empty);
            return scroll;
        }

        for (var i = 0; i < rowGroups.Count; i++)
        {
            var rowGroup = rowGroups[i];
            var rowIndex = rowGroup.Key;

            // ── Row of slots ──────────────────────────────────────────────────
            var hbox = new HBoxContainer();
            hbox.Alignment             = BoxContainer.AlignmentMode.Center;
            hbox.SizeFlagsHorizontal   = Control.SizeFlags.ExpandFill;
            hbox.AddThemeConstantOverride("separation", 12);

            foreach (var def in rowGroup)
            {
                var slot = new TalentSlot(def);
                slot.Toggled += OnSlotToggled;

                // Register in flat list
                _slots.Add(slot);

                // Register in structured lookup
                if (!_slotsBySchoolAndRow.ContainsKey(school))
                    _slotsBySchoolAndRow[school] = new Dictionary<int, List<TalentSlot>>();
                if (!_slotsBySchoolAndRow[school].ContainsKey(rowIndex))
                    _slotsBySchoolAndRow[school][rowIndex] = new List<TalentSlot>();
                _slotsBySchoolAndRow[school][rowIndex].Add(slot);

                hbox.AddChild(slot);
            }

            outer.AddChild(hbox);

            // ── Connector arrow between rows ──────────────────────────────────
            if (i < rowGroups.Count - 1)
            {
                var arrow = new Label();
                arrow.Text                = "▼";
                arrow.HorizontalAlignment = HorizontalAlignment.Center;
                arrow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                arrow.AddThemeFontSizeOverride("font_size", 18);
                arrow.AddThemeColorOverride("font_color", ArrowColor);
                outer.AddChild(arrow);
            }
        }

        // Bottom padding
        var botPad = new Control();
        botPad.CustomMinimumSize = new Vector2(0f, 16f);
        outer.AddChild(botPad);

        return scroll;
    }

    void OnSlotToggled(TalentSlot slot)
    {
        ValidateTree(slot.Definition.School);
    }

    // ── title row ─────────────────────────────────────────────────────────────
    Control BuildTitleRow()
    {
        var hbox = new HBoxContainer();

        // Spacer so the title centres despite the close button on the right
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
}
