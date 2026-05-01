#nullable enable
using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.Runes;

namespace healerfantasy.UI;

/// <summary>
/// Overlay panel for the Rune Table interactible in the Overworld.
///
/// Shows up to <see cref="RuneStore.TotalRunes"/> rune slots.
/// Acquired runes can be toggled on/off (activation is sequential — enabling
/// rune 3 automatically enables runes 1 and 2).  Once the run has started
/// (<see cref="RunState.RuneSelectionLocked"/>) the panel becomes read-only.
///
/// Opened by <see cref="OverworldController"/> when the player clicks the
/// rune-table interactible.
/// </summary>
public partial class RuneTablePanel : CanvasLayer
{
    // ── colours ───────────────────────────────────────────────────────────────
    static readonly Color PanelBg       = new(0.07f, 0.06f, 0.06f, 0.97f);
    static readonly Color PanelBorder   = new(0.65f, 0.52f, 0.28f);
    static readonly Color TitleColor    = new(0.95f, 0.84f, 0.50f);
    static readonly Color ActiveBorder  = new(0.75f, 0.30f, 0.90f);  // purple — rune active
    static readonly Color InactiveBorder = new(0.35f, 0.28f, 0.22f); // dim — not active
    static readonly Color LockedTint    = new(0.40f, 0.40f, 0.40f, 0.55f); // greyed locked slots

    // ── rune display info ─────────────────────────────────────────────────────
    record RuneInfo(RuneIndex Index, string Name, string Description);

    static readonly RuneInfo[] Runes =
    {
        new(RuneIndex.Void,   "Rune of the Void",
            "Enemy damage applies a Healing Absorption equal to 10% of the damage dealt to the target.\nHealing cannot restore health until the absorption is fully consumed.\nIndicated by a dark purple bar on party frames."),
        new(RuneIndex.Nature, "Rune of Nature",
            "Growing Vines attach to a party member every 8 seconds during boss fights, dealing 12 damage/s until killed.\nThe vines have their own health bars displayed below the boss bar."),
        new(RuneIndex.Time,   "Rune of Time",
            "All bosses gain +10% Haste, causing their abilities to fire 10% more frequently."),
        new(RuneIndex.Purity, "Rune of Purity",
            "Enables the 'purest' form of each boss, unlocking extra mechanics where available."),
    };

    // ── node refs ─────────────────────────────────────────────────────────────
    readonly List<RuneSlotControl> _slots = new();
    Label _statusLabel = null!;

    // ── lifecycle ─────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        Layer   = 10;
        Visible = false;

        // ── backdrop dimmer ───────────────────────────────────────────────────
        var dimmer = new ColorRect();
        dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dimmer.Color       = new Color(0f, 0f, 0f, 0.72f);
        dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(dimmer);

        // ── centred panel ─────────────────────────────────────────────────────
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left",   260);
        margin.AddThemeConstantOverride("margin_right",  260);
        margin.AddThemeConstantOverride("margin_top",    80);
        margin.AddThemeConstantOverride("margin_bottom", 80);
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(margin);

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = PanelBg;
        panelStyle.SetCornerRadiusAll(8);
        panelStyle.SetBorderWidthAll(2);
        panelStyle.BorderColor = PanelBorder;
        panelStyle.ContentMarginLeft = panelStyle.ContentMarginRight = 24f;
        panelStyle.ContentMarginTop  = panelStyle.ContentMarginBottom = 20f;

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panel.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        margin.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(vbox);

        // ── title row ─────────────────────────────────────────────────────────
        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(titleRow);

        var titleLabel = new Label();
        titleLabel.Text = "Rune Table";
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleLabel.AddThemeColorOverride("font_color", TitleColor);
        titleRow.AddChild(titleLabel);

        var closeBtn = new Button();
        closeBtn.Text = "✕  Close";
        closeBtn.Flat = true;
        closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        closeBtn.AddThemeFontSizeOverride("font_size", 14);
        closeBtn.AddThemeColorOverride("font_color", new Color(0.55f, 0.50f, 0.44f));
        closeBtn.Pressed += () => Visible = false;
        titleRow.AddChild(closeBtn);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", new Color(0.50f, 0.40f, 0.22f, 0.55f));
        vbox.AddChild(sep);

        // ── status label ──────────────────────────────────────────────────────
        _statusLabel = new Label();
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        _statusLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.50f, 0.44f));
        vbox.AddChild(_statusLabel);

        // ── rune health baseline info ─────────────────────────────────────────
        var baselineLabel = new Label();
        baselineLabel.Text = "Each active rune increases all boss health by +10%.";
        baselineLabel.HorizontalAlignment = HorizontalAlignment.Center;
        baselineLabel.AddThemeFontSizeOverride("font_size", 12);
        baselineLabel.AddThemeColorOverride("font_color", new Color(0.60f, 0.55f, 0.50f));
        vbox.AddChild(baselineLabel);

        // ── rune slots grid ───────────────────────────────────────────────────
        var slotsContainer = new VBoxContainer();
        slotsContainer.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(slotsContainer);

        var acquired = RuneStore.AcquiredRuneCount;
        for (var i = 0; i < RuneStore.TotalRunes; i++)
        {
            var info       = Runes[i];
            var runeNum    = i + 1;
            var isAcquired = runeNum <= acquired;
            var slot       = new RuneSlotControl(info.Index, runeNum, info.Name, info.Description, isAcquired);
            slot.Toggled  += OnRuneToggled;
            _slots.Add(slot);
            slotsContainer.AddChild(slot);
        }

        // Apply current active rune state.
        RefreshSlotsFromState();
    }

    // ── public API ────────────────────────────────────────────────────────────

    /// <summary>Show or hide the panel, refreshing rune state from RunState.</summary>
    public void Toggle()
    {
        Visible = !Visible;
        if (Visible) RefreshSlotsFromState();
    }

    public void Open()
    {
        Visible = true;
        RefreshSlotsFromState();
    }

    // ── private ───────────────────────────────────────────────────────────────

    void RefreshSlotsFromState()
    {
        var locked   = RunState.Instance.RuneSelectionLocked;
        var acquired = RuneStore.AcquiredRuneCount;

        _statusLabel.Text = locked
            ? "⚠  Run in progress — rune selection is locked."
            : acquired == 0
                ? "Defeat the final boss to unlock your first rune."
                : "Click an acquired rune to activate or deactivate it.";

        _statusLabel.AddThemeColorOverride("font_color",
            locked ? new Color(0.85f, 0.55f, 0.20f) : new Color(0.55f, 0.50f, 0.44f));

        for (var i = 0; i < _slots.Count; i++)
        {
            var runeNum    = i + 1;
            var isAcquired = runeNum <= acquired;
            var isActive   = RunState.Instance.IsRuneActive(Runes[i].Index);
            _slots[i].SetState(isAcquired, isActive, locked);
        }
    }

    void OnRuneToggled(RuneIndex rune)
    {
        if (RunState.Instance.RuneSelectionLocked) return;

        var runeNum  = (int)rune;
        var acquired = RuneStore.AcquiredRuneCount;
        if (runeNum > acquired) return; // safety check

        // Determine current active rune count.
        var currentActive = RunState.Instance.ActiveRuneCount;

        // Sequential rule: activating rune N also activates 1..N-1.
        // Deactivating rune N also deactivates N+1..4.
        if (RunState.Instance.IsRuneActive(rune))
        {
            // Deactivate this rune and all above it.
            var newActive = new List<RuneIndex>();
            for (var i = 1; i < runeNum; i++)
                newActive.Add((RuneIndex)i);
            RunState.Instance.SetActiveRunes(newActive);
        }
        else
        {
            // Activate this rune and all below it.
            var newActive = new List<RuneIndex>();
            for (var i = 1; i <= runeNum; i++)
                if (i <= acquired) newActive.Add((RuneIndex)i);
            RunState.Instance.SetActiveRunes(newActive);
        }

        RefreshSlotsFromState();
    }

    // ── inner control ─────────────────────────────────────────────────────────

    /// <summary>A single rune slot: icon + name + description + toggle button.</summary>
    sealed partial class RuneSlotControl : PanelContainer
    {
        public event System.Action<RuneIndex>? Toggled;

        readonly RuneIndex _index;
        readonly int       _runeNum;
        readonly bool      _isAcquired;
        readonly StyleBoxFlat _borderStyle;
        readonly Button    _toggleBtn;
        readonly Label     _descLabel;
        readonly TextureRect _runeIcon;
        readonly ColorRect   _lockOverlay;

        public RuneSlotControl(RuneIndex index, int runeNum, string name,
                               string description, bool isAcquired)
        {
            _index      = index;
            _runeNum    = runeNum;
            _isAcquired = isAcquired;

            CustomMinimumSize = new Vector2(0f, 80f);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _borderStyle = new StyleBoxFlat();
            _borderStyle.BgColor = new Color(0.10f, 0.09f, 0.08f, 0.85f);
            _borderStyle.SetCornerRadiusAll(6);
            _borderStyle.SetBorderWidthAll(2);
            _borderStyle.BorderColor = InactiveBorder;
            _borderStyle.ContentMarginLeft = _borderStyle.ContentMarginRight = 14f;
            _borderStyle.ContentMarginTop  = _borderStyle.ContentMarginBottom = 10f;
            AddThemeStyleboxOverride("panel", _borderStyle);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 14);
            AddChild(row);

            // ── rune icon ─────────────────────────────────────────────────────
            _runeIcon = new TextureRect();
            _runeIcon.CustomMinimumSize = new Vector2(52f, 52f);
            _runeIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _runeIcon.ExpandMode  = TextureRect.ExpandModeEnum.FitWidth;
            if (isAcquired)
                _runeIcon.Texture = GD.Load<Texture2D>(AssetConstants.RuneIconPath(runeNum));
            else
                _runeIcon.Modulate = new Color(0.25f, 0.25f, 0.25f);
            row.AddChild(_runeIcon);

            // ── text column ───────────────────────────────────────────────────
            var textCol = new VBoxContainer();
            textCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            textCol.AddThemeConstantOverride("separation", 3);
            row.AddChild(textCol);

            var nameLabel = new Label();
            nameLabel.Text = isAcquired ? name : $"??? (Defeat the Queen with {runeNum - 1} rune(s) active to unlock)";
            nameLabel.AddThemeFontSizeOverride("font_size", 15);
            nameLabel.AddThemeColorOverride("font_color",
                isAcquired ? new Color(0.92f, 0.88f, 0.82f) : new Color(0.45f, 0.40f, 0.35f));
            textCol.AddChild(nameLabel);

            _descLabel = new Label();
            _descLabel.Text = isAcquired ? description : "";
            _descLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            _descLabel.AddThemeFontSizeOverride("font_size", 11);
            _descLabel.AddThemeColorOverride("font_color", new Color(0.60f, 0.56f, 0.50f));
            textCol.AddChild(_descLabel);

            // ── toggle button ─────────────────────────────────────────────────
            _toggleBtn = new Button();
            _toggleBtn.Text = "Activate";
            _toggleBtn.CustomMinimumSize = new Vector2(90f, 0f);
            _toggleBtn.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            _toggleBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            _toggleBtn.AddThemeFontSizeOverride("font_size", 13);
            _toggleBtn.Visible = isAcquired;
            _toggleBtn.Pressed += () => Toggled?.Invoke(_index);
            row.AddChild(_toggleBtn);

            // ── locked overlay ────────────────────────────────────────────────
            _lockOverlay = new ColorRect();
            _lockOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _lockOverlay.Color = LockedTint;
            _lockOverlay.MouseFilter = MouseFilterEnum.Ignore;
            _lockOverlay.Visible = false;
            AddChild(_lockOverlay);
        }

        public void SetState(bool isAcquired, bool isActive, bool locked)
        {
            _borderStyle.BorderColor = (isAcquired && isActive) ? ActiveBorder : InactiveBorder;

            if (_toggleBtn != null)
            {
                _toggleBtn.Visible  = isAcquired && !locked;
                _toggleBtn.Text     = isActive ? "Deactivate" : "Activate";
                var btnStyle = new StyleBoxFlat();
                btnStyle.BgColor = isActive
                    ? new Color(0.30f, 0.10f, 0.45f)
                    : new Color(0.14f, 0.11f, 0.09f);
                btnStyle.SetCornerRadiusAll(5);
                btnStyle.SetBorderWidthAll(1);
                btnStyle.BorderColor = isActive
                    ? new Color(0.60f, 0.20f, 0.80f)
                    : new Color(0.45f, 0.38f, 0.22f);
                btnStyle.ContentMarginLeft  = btnStyle.ContentMarginRight  = 10f;
                btnStyle.ContentMarginTop   = btnStyle.ContentMarginBottom = 6f;
                _toggleBtn.AddThemeStyleboxOverride("normal",  btnStyle);
                _toggleBtn.AddThemeStyleboxOverride("hover",   btnStyle);
                _toggleBtn.AddThemeStyleboxOverride("pressed", btnStyle);
                _toggleBtn.AddThemeStyleboxOverride("focus",   btnStyle);
                _toggleBtn.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
            }

            if (_lockOverlay != null)
                _lockOverlay.Visible = locked && isAcquired;

            if (_runeIcon != null && isAcquired && _runeIcon.Texture == null)
                _runeIcon.Texture = GD.Load<Texture2D>(AssetConstants.RuneIconPath(_runeNum));
        }
    }
}
