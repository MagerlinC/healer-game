using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

/// <summary>
/// Full-screen talent selection panel.
///
/// • Press T (or click ✕) to open/close.
/// • While open the game is paused so the player cannot cast spells.
/// • Selecting / deselecting a slot immediately updates the player's
///   <see cref="Character.Talents"/> list on close.
/// • Syncs slot visual states with the player's current talents on open,
///   so re-opening always reflects prior choices.
///
/// Wiring: call <see cref="Init"/> from <c>World._Ready</c> after the
/// Player node is resolved, then add this node as a child of the scene root.
/// </summary>
public partial class TalentSelector : CanvasLayer
{
	// ── colours / sizes ──────────────────────────────────────────────────────
	static readonly Color OverlayBg = new(0.00f, 0.00f, 0.00f, 0.72f);
	static readonly Color PanelBg = new(0.10f, 0.08f, 0.07f, 0.98f);
	static readonly Color PanelBorder = new(0.65f, 0.52f, 0.28f); // warm gold
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f); // bright gold
	static readonly Color HintColor = new(0.45f, 0.42f, 0.38f);
	static readonly Color SepColor = new(0.50f, 0.40f, 0.22f, 0.55f);

	// ── state ────────────────────────────────────────────────────────────────
	Player _player;
	Control _overlay; // root visible node (full-screen)
	readonly List<TalentSlot> _slots = new();
	bool _isOpen;

	// ── public API ───────────────────────────────────────────────────────────
	/// <summary>
	/// Must be called once after the node is added to the scene tree.
	/// Builds the full UI and links to the <paramref name="player"/>.
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

		// Keep running while the game is paused so we can handle T-key to close.
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
		if (_player == null) return; // safety: Init not called yet

		// Sync slot states to whatever talents the player currently has
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

		// Rebuild the player's talent list from selected slots
		ApplyTalentsToPlayer();
	}

	// ── talent synchronisation ────────────────────────────────────────────────
	void SyncSlotsFromPlayer()
	{
		// Collect names of talents currently active on the player
		var active = new HashSet<string>();
		foreach (var t in _player.Talents)
			active.Add(t.Name);

		foreach (var slot in _slots)
			slot.SetSelected(active.Contains(slot.Definition.Name));
	}

	void ApplyTalentsToPlayer()
	{
		_player.Talents.Clear();
		foreach (var slot in _slots)
			if (slot.IsSelected)
				_player.Talents.Add(slot.Definition.CreateTalent());
	}

	// ── UI construction ───────────────────────────────────────────────────────
	Control BuildOverlay()
	{
		// Full-screen mouse-blocking dim layer
		var overlay = new ColorRect();
		overlay.Color = OverlayBg;
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;

		// Centred panel
		var panel = BuildPanel();
		overlay.AddChild(panel);

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
		style.ContentMarginLeft = 24f;
		style.ContentMarginRight = 24f;
		style.ContentMarginTop = 18f;
		style.ContentMarginBottom = 24f;
		panel.AddThemeStyleboxOverride("panel", style);

		// Anchor to centre of screen
		panel.AnchorLeft = 0.5f;
		panel.AnchorRight = 0.5f;
		panel.AnchorTop = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical = Control.GrowDirection.Both;

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		panel.AddChild(vbox);

		// ── Title row ────────────────────────────────────────────────────────
		vbox.AddChild(BuildTitleRow());

		// ── Gold separator ───────────────────────────────────────────────────
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		vbox.AddChild(sep);

		// ── Talent grid ──────────────────────────────────────────────────────
		var grid = new GridContainer();
		grid.Columns = 3;
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 12);

		foreach (var def in TalentRegistry.All)
		{
			var slot = new TalentSlot(def);
			_slots.Add(slot);
			grid.AddChild(slot);
		}

		vbox.AddChild(grid);

		// ── Footer hint ──────────────────────────────────────────────────────
		var hint = new Label();
		hint.Text = "Click a talent to toggle it on or off  •  [T] or [Esc] to close";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		vbox.AddChild(hint);

		return panel;
	}

	Control BuildTitleRow()
	{
		var hbox = new HBoxContainer();

		// Spacer so the title centres despite the close button on the right
		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(28, 0);
		hbox.AddChild(spacer);

		var title = new Label();
		title.Text = "Talents";
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 22);
		title.AddThemeColorOverride("font_color", TitleColor);
		hbox.AddChild(title);

		var closeBtn = new Button();
		closeBtn.Text = "✕";
		closeBtn.Flat = true;
		closeBtn.CustomMinimumSize = new Vector2(28, 28);
		closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		closeBtn.AddThemeFontSizeOverride("font_size", 16);
		closeBtn.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		closeBtn.AddThemeColorOverride("font_hover_color", new Color(1.00f, 0.90f, 0.55f));
		closeBtn.Pressed += Close;
		hbox.AddChild(closeBtn);

		return hbox;
	}
}