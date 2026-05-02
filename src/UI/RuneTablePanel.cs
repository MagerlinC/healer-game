#nullable enable
using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.Runes;

namespace healerfantasy.UI;

/// <summary>
/// Overlay panel for the Rune Table interactible in the Overworld.
///
/// Displays a 2×2 grid of rune slots.  Each slot shows the rune icon inside a
/// decorative frame (<see cref="AssetConstants.RuneFramePath"/> /
/// <see cref="AssetConstants.RuneFrameActivePath"/>).  Hovering a slot shows
/// the rune description (plus the +10% boss-health note) via
/// <see cref="GameTooltip"/>; unacquired runes show a ??? tooltip.
///
/// Toggling a rune plays <see cref="AssetConstants.RuneSfxPath"/>.
/// Rune selection is sequential and locks once the run starts.
/// </summary>
public partial class RuneTablePanel : CanvasLayer
{
	// ── colours ───────────────────────────────────────────────────────────────
	static readonly Color PanelBg = new(0.07f, 0.06f, 0.06f, 0.97f);
	static readonly Color PanelBorder = new(0.65f, 0.52f, 0.28f);
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);

	// Appended to every acquired rune's tooltip description.
	const string HealthBonusLine = "\n\nEach active rune increases all boss health by +10%.";

	// ── rune display info ─────────────────────────────────────────────────────
	record RuneInfo(RuneIndex Index, string Name, string Description);

	static readonly RuneInfo[] Runes =
	{
		new(RuneIndex.Void, "Rune of the Void",
			"Seeps the world in the energy of the void, making enemy damage apply a Healing Absorption equal to 10% of the damage dealt to the target.\nHealing cannot restore health until the absorption is fully consumed."),
		new(RuneIndex.Nature, "Rune of Nature",
			"Embraces the violent growth of nature, causing Growing Vines to appear, attaching to party members and dealing damage until killed."),
		new(RuneIndex.Time, "Rune of Time",
			"Energizes the flow of time, granting all bosses +10% Haste, causing their attacks and abilities to fire 10% more frequently."),
		new(RuneIndex.Purity, "Rune of Purity",
			"Enables the purest form of each boss, unlocking their true strength.")
	};

	// ── node refs ─────────────────────────────────────────────────────────────
	readonly List<RuneSlotControl> _slots = new();
	Label _statusLabel = null!;
	AudioStreamPlayer _sfxPlayer = null!;

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		Layer = 10;
		Visible = false;

		// ── backdrop dimmer ───────────────────────────────────────────────────
		var dimmer = new ColorRect();
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.Color = new Color(0f, 0f, 0f, 0.72f);
		dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(dimmer);

		// ── centred panel ─────────────────────────────────────────────────────
		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 350);
		margin.AddThemeConstantOverride("margin_right", 350);
		margin.AddThemeConstantOverride("margin_top", 100);
		margin.AddThemeConstantOverride("margin_bottom", 100);
		margin.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(margin);

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = PanelBg;
		panelStyle.SetCornerRadiusAll(8);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = PanelBorder;
		panelStyle.ContentMarginLeft = panelStyle.ContentMarginRight = 24f;
		panelStyle.ContentMarginTop = panelStyle.ContentMarginBottom = 20f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
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

		// ── 2×2 rune icon grid ────────────────────────────────────────────────
		// Centred horizontally inside the panel via an aligning HBoxContainer.
		var rowWrapper = new HBoxContainer();
		rowWrapper.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.AddChild(rowWrapper);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("h_separation", 16);
		row.AddThemeConstantOverride("v_separation", 16);
		rowWrapper.AddChild(row);

		var acquired = RuneStore.AcquiredRuneCount;
		for (var i = 0; i < RuneStore.TotalRunes; i++)
		{
			var info = Runes[i];
			var runeNum = i + 1;
			var isAcquired = runeNum <= acquired;

			var tooltipTitle = isAcquired ? info.Name : "???";
			var tooltipDesc = isAcquired
				? info.Description + HealthBonusLine
				: $"Defeat the Queen with {runeNum - 1} rune(s) active to unlock.";

			var slot = new RuneSlotControl(info.Index, runeNum, isAcquired, tooltipTitle, tooltipDesc);
			slot.Toggled += OnRuneToggled;
			_slots.Add(slot);
			row.AddChild(slot);
		}

		RefreshSlotsFromState();

		// ── SFX player ────────────────────────────────────────────────────────
		_sfxPlayer = new AudioStreamPlayer();
		_sfxPlayer.Stream = GD.Load<AudioStream>(AssetConstants.RuneSfxPath);
		_sfxPlayer.VolumeDb = -6f;
		AddChild(_sfxPlayer);
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>Show the panel, refreshing rune state.</summary>
	public void Open()
	{
		Visible = true;
		RefreshSlotsFromState();
	}

	/// <summary>Toggle visibility, refreshing on show.</summary>
	public void Toggle()
	{
		Visible = !Visible;
		if (Visible) RefreshSlotsFromState();
	}

	// ── private ───────────────────────────────────────────────────────────────

	void RefreshSlotsFromState()
	{
		var locked = RunState.Instance.RuneSelectionLocked;
		var acquired = RuneStore.AcquiredRuneCount;

		_statusLabel.Text = locked
			? "⚠  Run in progress — rune selection is locked."
			: acquired == 0
				? "Defeat the final boss to unlock your first rune."
				: "Click a rune to activate or deactivate it.";

		_statusLabel.AddThemeColorOverride("font_color",
			locked ? new Color(0.85f, 0.55f, 0.20f) : new Color(0.55f, 0.50f, 0.44f));

		for (var i = 0; i < _slots.Count; i++)
		{
			var runeNum = i + 1;
			var isAcquired = runeNum <= acquired;
			var isActive = RunState.Instance.IsRuneActive(Runes[i].Index);
			_slots[i].SetState(isAcquired, isActive, locked);
		}
	}

	void OnRuneToggled(RuneIndex rune)
	{
		if (RunState.Instance.RuneSelectionLocked) return;

		var runeNum = (int)rune;
		var acquired = RuneStore.AcquiredRuneCount;
		if (runeNum > acquired) return;

		// Sequential rule: activating rune N activates 1..N; deactivating N deactivates N..4.
		List<RuneIndex> newActive;
		if (RunState.Instance.IsRuneActive(rune))
		{
			newActive = new List<RuneIndex>();
			for (var i = 1; i < runeNum; i++)
				newActive.Add((RuneIndex)i);
		}
		else
		{
			newActive = new List<RuneIndex>();
			for (var i = 1; i <= runeNum; i++)
				if (i <= acquired)
					newActive.Add((RuneIndex)i);
		}

		RunState.Instance.SetActiveRunes(newActive);
		LoadoutPreferences.SaveActiveRunes(newActive);

		_sfxPlayer.Play();
		RefreshSlotsFromState();
	}

	// ── inner slot control ────────────────────────────────────────────────────

	/// <summary>
	/// A square slot: decorative frame texture with the rune icon layered on top.
	/// Clicking (when acquired and unlocked) fires <see cref="Toggled"/>.
	/// Hovering shows a <see cref="GameTooltip"/>.
	/// </summary>
	sealed partial class RuneSlotControl : Control
	{
		public event System.Action<RuneIndex>? Toggled;

		const float SlotSize = 140f;
		const float IconInset = 20f;

		readonly RuneIndex _index;
		readonly int _runeNum;
		readonly string _tooltipTitle;
		readonly string _tooltipDesc;

		TextureRect _frame = null!;
		TextureRect _icon = null!;

		bool _isAcquired;
		bool _locked;

		public RuneSlotControl(RuneIndex index, int runeNum, bool isAcquired,
			string tooltipTitle, string tooltipDesc)
		{
			_index = index;
			_runeNum = runeNum;
			_isAcquired = isAcquired;
			_tooltipTitle = tooltipTitle;
			_tooltipDesc = tooltipDesc;

			CustomMinimumSize = new Vector2(SlotSize, SlotSize);
			MouseFilter = MouseFilterEnum.Stop;
			MouseDefaultCursorShape = isAcquired ? CursorShape.PointingHand : CursorShape.Arrow;

			// ── frame ─────────────────────────────────────────────────────────
			_frame = new TextureRect();
			_frame.SetAnchorsPreset(LayoutPreset.FullRect);
			_frame.StretchMode = TextureRect.StretchModeEnum.Scale;
			_frame.MouseFilter = MouseFilterEnum.Ignore;
			_frame.Texture = GD.Load<Texture2D>(AssetConstants.RuneFramePath);
			AddChild(_frame);

			// ── icon (inset via a MarginContainer) ────────────────────────────
			var iconMargin = new MarginContainer();
			iconMargin.SetAnchorsPreset(LayoutPreset.FullRect);
			iconMargin.AddThemeConstantOverride("margin_left", (int)IconInset);
			iconMargin.AddThemeConstantOverride("margin_right", (int)IconInset);
			iconMargin.AddThemeConstantOverride("margin_top", (int)IconInset);
			iconMargin.AddThemeConstantOverride("margin_bottom", (int)IconInset);
			iconMargin.MouseFilter = MouseFilterEnum.Ignore;
			AddChild(iconMargin);

			_icon = new TextureRect();
			_icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			_icon.ExpandMode = TextureRect.ExpandModeEnum.FitWidth;
			_icon.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_icon.SizeFlagsVertical = SizeFlags.ExpandFill;
			_icon.MouseFilter = MouseFilterEnum.Ignore;

			if (isAcquired)
				_icon.Texture = GD.Load<Texture2D>(AssetConstants.RuneIconPath(runeNum));
			else
				_icon.Modulate = new Color(0.18f, 0.18f, 0.18f);

			iconMargin.AddChild(_icon);

			// ── events ────────────────────────────────────────────────────────
			MouseEntered += () => GameTooltip.Show(_tooltipTitle, _tooltipDesc);
			MouseExited += () => GameTooltip.Hide();
			GuiInput += OnGuiInput;
		}

		void OnGuiInput(InputEvent ev)
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				if (_isAcquired && !_locked)
					Toggled?.Invoke(_index);
		}

		public void SetState(bool isAcquired, bool isActive, bool locked)
		{
			_isAcquired = isAcquired;
			_locked = locked;

			// Swap frame texture.
			_frame.Texture = GD.Load<Texture2D>(
				isAcquired && isActive ? AssetConstants.RuneFrameActivePath : AssetConstants.RuneFramePath);

			// Icon tint: dark = not acquired, slightly grey = locked, white = normal.
			_icon.Modulate = !isAcquired
				? new Color(0.18f, 0.18f, 0.18f)
				: locked
					? new Color(0.60f, 0.60f, 0.60f, 0.80f)
					: Colors.White;

			// Lazily load the icon texture once the rune is first acquired.
			if (isAcquired && _icon.Texture == null)
				_icon.Texture = GD.Load<Texture2D>(AssetConstants.RuneIconPath(_runeNum));

			MouseDefaultCursorShape = isAcquired && !locked ? CursorShape.PointingHand : CursorShape.Arrow;
		}
	}
}