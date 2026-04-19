using Godot;
using healerfantasy;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// Party health-frame bar at the bottom of the screen.
/// Each frame has a dark background; the coloured fill IS the health bar —
/// as health drops the fill shrinks left, revealing the dark empty portion.
///
/// Member order: 0 = Templar, 1 = Healer, 2 = Assassin, 3 = Wizard
/// </summary>
public partial class GameUI : CanvasLayer
{
	CastBar      _castBar;
	ProgressBar  _manaBar;
	ActionBar    _actionBar;

	// ── per-member config: name + the colour the bar fills with ─────────────
	static readonly (string Name, Color BarColor, float MaxHp)[] MemberDefs =
	{
		("Templar",  new Color(0.88f, 0.30f, 0.50f), 150f), // rose-red
		("Healer",   new Color(0.35f, 0.78f, 0.22f),  80f), // poison-green
		("Assassin", new Color(0.85f, 0.78f, 0.15f), 100f), // golden-yellow
		("Wizard",   new Color(0.20f, 0.50f, 0.95f),  70f), // sapphire-blue
	};

	static readonly Color BorderDefault = new Color(0.32f, 0.26f, 0.26f);
	static readonly Color BorderHovered = new Color(0.90f, 0.80f, 0.20f); // gold highlight

	readonly ProgressBar[]    _bars        = new ProgressBar[4];
	readonly PanelContainer[] _panels      = new PanelContainer[4];
	readonly StyleBoxFlat[]   _panelStyles = new StyleBoxFlat[4];
	readonly Character[]      _characters  = new Character[4];

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		Layer = 10;

		// Full-screen transparent control used only for edge-anchoring
		var anchor = new Control();
		anchor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		anchor.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(anchor);

		_castBar = new CastBar();
		_castBar.Size = new Vector2(200, 14);
		_castBar.AnchorLeft   = 0.5f;
		_castBar.AnchorTop    = 0.6f;
		_castBar.OffsetLeft   = -100;
		_castBar.OffsetRight  =  100;
		_castBar.OffsetTop    =   20;
		_castBar.OffsetBottom =   40;
		anchor.AddChild(_castBar);

		_manaBar = new ManaBar();
		_manaBar.Size = new Vector2(200, 14);
		_manaBar.AnchorLeft   = 0.5f;
		_manaBar.AnchorRight  = 0.5f;
		_manaBar.AnchorTop    = 0.6f;
		_manaBar.OffsetLeft   = -100;
		_manaBar.OffsetRight  =  100;
		_manaBar.OffsetTop    =   50;
		_manaBar.OffsetBottom =   70;

		var fillStyle = new StyleBoxFlat();
		fillStyle.BgColor = Colors.Blue;
		_manaBar.AddThemeColorOverride("fill_color", Colors.Blue);
		_manaBar.AddThemeStyleboxOverride("fill", fillStyle);
		anchor.AddChild(_manaBar);

		// Horizontal row pinned to the bottom-left corner
		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 6);
		hbox.AnchorLeft   = 0f;
		hbox.AnchorTop    = 1f;
		hbox.AnchorRight  = 0f;
		hbox.AnchorBottom = 1f;
		hbox.GrowHorizontal = Control.GrowDirection.End;
		hbox.GrowVertical   = Control.GrowDirection.Begin;
		hbox.OffsetLeft   =   10f;
		hbox.OffsetTop    =  -74f;
		hbox.OffsetBottom =  -10f;
		anchor.AddChild(hbox);

		for (var i = 0; i < MemberDefs.Length; i++)
		{
			var (name, barColor, maxHp) = MemberDefs[i];
			var panel = BuildFrame(name, barColor, maxHp, out _bars[i], out _panelStyles[i]);
			_panels[i] = panel;

			// Capture the style reference (not the loop index) to avoid the
			// classic C# closure-in-loop capture bug.
			var style = _panelStyles[i];
			panel.MouseEntered += () => style.BorderColor = BorderHovered;
			panel.MouseExited  += () => style.BorderColor = BorderDefault;

			hbox.AddChild(panel);
		}

		// Action bar — centered above the party frames.
		// Party frames sit at OffsetBottom=-10, OffsetTop=-74 (64px tall).
		// We leave a 10px gap, so the action bar bottom is at -84, top at -136 (52px tall).
		_actionBar = new ActionBar();
		_actionBar.AnchorLeft   = 0.5f;
		_actionBar.AnchorRight  = 0.5f;
		_actionBar.AnchorTop    = 1f;
		_actionBar.AnchorBottom = 1f;
		_actionBar.GrowHorizontal = Control.GrowDirection.Both;
		_actionBar.GrowVertical   = Control.GrowDirection.Begin;
		_actionBar.OffsetLeft   = -55f; // half of (2 × 52px slots + 6px gap) = 55
		_actionBar.OffsetRight  =  55f;
		_actionBar.OffsetTop    = -136f;
		_actionBar.OffsetBottom =  -84f;
		anchor.AddChild(_actionBar);
	}

	// ── public API ───────────────────────────────────────────────────────────
	/// <summary>Update a member's displayed health. Index matches MemberDefs order.</summary>
	public void SetHealth(int index, float current, float max)
	{
		if (index < 0 || index >= _bars.Length) return;
		_bars[index].MaxValue = max;
		_bars[index].Value    = current;
	}

	/// <summary>
	/// Populate the action bar from the player's spell bindings.
	/// Call this from World after the scene is ready.
	/// </summary>
	public void SetupActionBar((SpellResource Spell, string Action)[] bindings)
	{
		foreach (var (spell, action) in bindings)
			_actionBar.AddSlot(spell, action);
	}

	/// <summary>
	/// Associate a Character with a UI slot so hover-targeting can resolve it.
	/// Slot order must match MemberDefs: 0=Templar, 1=Healer, 2=Assassin, 3=Wizard.
	/// </summary>
	public void BindCharacter(int slot, Character character)
	{
		if (slot < 0 || slot >= _characters.Length) return;
		_characters[slot] = character;
	}

	/// <summary>
	/// Returns the Character whose health frame the mouse is currently over,
	/// or null if the cursor is not hovering any frame.
	/// </summary>
	public Character GetHoveredCharacter()
	{
		var mousePos = GetViewport().GetMousePosition();
		for (var i = 0; i < _panels.Length; i++)
		{
			if (_panels[i] != null && _panels[i].GetGlobalRect().HasPoint(mousePos))
				return _characters[i];
		}
		return null;
	}

	// ── frame builder ────────────────────────────────────────────────────────
	static PanelContainer BuildFrame(
		string name, Color barColor, float maxHp,
		out ProgressBar bar, out StyleBoxFlat panelStyle)
	{
		// Dark outer frame
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(138, 54);

		panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.11f, 0.09f, 0.09f, 0.95f);
		panelStyle.SetCornerRadiusAll(6);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor         = BorderDefault;
		panelStyle.ContentMarginLeft   = 8f;
		panelStyle.ContentMarginRight  = 8f;
		panelStyle.ContentMarginTop    = 5f;
		panelStyle.ContentMarginBottom = 5f;
		panel.AddThemeStyleboxOverride("panel", panelStyle);

		var progressBar = new ProgressBar();
		progressBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		progressBar.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
		progressBar.AddThemeConstantOverride("separation", 4);
		// Ignore mouse so the PanelContainer keeps focus and MouseExited
		// doesn't fire when the cursor moves over the bar or label.
		progressBar.MouseFilter = Control.MouseFilterEnum.Ignore;
		panel.AddChild(progressBar);

		// Name label — light text on dark background
		var label = new Label();
		label.Text = name;
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		label.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment   = VerticalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		panel.AddChild(label);

		// Health bar — coloured fill, dark empty portion
		var barBg = new StyleBoxFlat();
		barBg.BgColor = new Color(0.16f, 0.13f, 0.13f);
		progressBar.AddThemeStyleboxOverride("background", barBg);

		var barFill = new StyleBoxFlat();
		barFill.BgColor = barColor;
		progressBar.AddThemeStyleboxOverride("fill", barFill);

		progressBar.ShowPercentage = false;
		progressBar.MaxValue = maxHp;
		progressBar.Value    = maxHp;

		bar = progressBar;
		return panel;
	}
}
