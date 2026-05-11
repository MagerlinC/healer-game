using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources.Void;
using SpellResource = healerfantasy.SpellResources.SpellResource;

namespace healerfantasy.UI;

/// <summary>
/// A single action-bar slot dedicated to the player's equipped ultimate spell.
///
/// Visual states
/// ─────────────
///   Inactive — requirement not yet met.
///             Dark border, slightly dimmed icon, arc showing current progress.
///   Ready    — requirement met, spell not on cooldown.
///             Gold glowing border, full-brightness icon, no progress arc.
///   Active   — ultimate's buff is currently running on the caster.
///             Purple arcane border, bright icon, pulsing glow.
///
/// The slot polls the Player reference each frame (no additional signals needed)
/// to determine which state it is in and updates visuals accordingly.
/// </summary>
public partial class UltimateSlot : Control
{
	// ── colours ──────────────────────────────────────────────────────────────
	static readonly Color BorderInactive = new(0.20f, 0.14f, 0.28f);
	static readonly Color BorderReady = new(0.95f, 0.80f, 0.10f); // gold
	static readonly Color BorderActive = new(0.65f, 0.22f, 0.90f); // arcane purple

	static readonly Color BgColor = new(0.10f, 0.07f, 0.14f, 0.95f);
	static readonly Color ArcColor = new(0.65f, 0.22f, 0.90f, 0.55f); // progress arc
	static readonly Color OverlayDim = new(0f, 0f, 0f, 0.42f); // inactive dim

	// ── child refs ───────────────────────────────────────────────────────────
	PanelContainer _panel = null!;
	StyleBoxFlat _border = null!;
	TextureRect? _icon = null;
	Control _inner = null!;
	ColorRect? _dimOverlay = null;
	CooldownOverlay? _cooldown = null;

	// ── state ─────────────────────────────────────────────────────────────────
	Player? _player;
	UltimateSpellResource? _ultimate;
	float _pulseTimer = 0f;

	enum SlotState
	{
		Empty,
		Inactive,
		Ready,
		Active
	}

	SlotState _state = SlotState.Empty;

	// Progress arc geometry
	const int ArcSegments = 40;

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		// Wrap everything in a fixed-size panel so the slot matches the action bar.
		CustomMinimumSize = new Vector2(58, 58);
		MouseFilter = MouseFilterEnum.Ignore;

		_panel = new PanelContainer();
		_panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_border = new StyleBoxFlat();
		_border.BgColor = BgColor;
		_border.SetCornerRadiusAll(5);
		_border.SetBorderWidthAll(2);
		_border.BorderColor = BorderInactive;
		_border.ContentMarginLeft = 3f;
		_border.ContentMarginRight = 3f;
		_border.ContentMarginTop = 3f;
		_border.ContentMarginBottom = 3f;
		_panel.AddThemeStyleboxOverride("panel", _border);

		AddChild(_panel);

		_inner = new Control();
		_inner.MouseFilter = MouseFilterEnum.Ignore;
		_inner.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_inner.SizeFlagsVertical = SizeFlags.ExpandFill;
		_inner.ClipContents = true;
		_panel.AddChild(_inner);
	}

	/// <summary>
	/// Bind the slot to a player and their equipped ultimate.
	/// Call from GameUI after the Player node is resolved.
	/// </summary>
	public void Bind(Player player, UltimateSpellResource? ultimate)
	{
		_player = player;
		_ultimate = ultimate;

		// Rebuild icon and overlays.
		foreach (var child in _inner.GetChildren())
			child.QueueFree();
		_icon = null;
		_dimOverlay = null;
		_cooldown = null;

		if (ultimate == null)
		{
			_state = SlotState.Empty;
			_border.BorderColor = BorderInactive;
			return;
		}

		// Icon
		if (ultimate.Icon != null)
		{
			_icon = new TextureRect();
			_icon.Texture = ultimate.Icon;
			_icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			_icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			_icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			_inner.AddChild(_icon);
		}

		// Cooldown overlay (reuses the same component as regular action-bar slots).
		_cooldown = new CooldownOverlay();
		_cooldown.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_cooldown.MouseFilter = MouseFilterEnum.Ignore;
		_inner.AddChild(_cooldown);

		// Dim overlay shown in Inactive state.
		_dimOverlay = new ColorRect();
		_dimOverlay.Color = OverlayDim;
		_dimOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_dimOverlay.MouseFilter = MouseFilterEnum.Ignore;
		_inner.AddChild(_dimOverlay);

		// Keybind label ("R")
		var label = new Label();
		label.Text = GetKeybindLabel("ultimate");
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", new Color(1.00f, 1.00f, 0.85f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.00f, 0.00f, 0.00f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		label.GrowHorizontal = GrowDirection.Begin;
		label.GrowVertical = GrowDirection.Begin;
		_inner.AddChild(label);

		// Subscribe to the cooldown signal so the overlay animates correctly.
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CooldownStarted),
			Callable.From((SpellResource spell, float duration) =>
			{
				if (ReferenceEquals(spell, _ultimate))
					_cooldown?.Start(duration);
			}));

		// Tooltip
		var tooltipText = GameTooltip.FormatSpellTooltip(ultimate);
		_panel.MouseEntered += () => GameTooltip.Show(tooltipText.title, tooltipText.desc);
		_panel.MouseExited += () => GameTooltip.Hide();
		_panel.MouseFilter = MouseFilterEnum.Stop;

		_state = SlotState.Inactive;
		QueueRedraw();
	}

	// ── per-frame update ──────────────────────────────────────────────────────

	public override void _Process(double delta)
	{
		_cooldown?.Tick((float)delta);

		if (_ultimate == null || _player == null) return;

		_pulseTimer += (float)delta;

		// Determine state.
		var newState = DetermineState();
		if (newState != _state)
		{
			_state = newState;
			ApplyVisualState();
		}

		// In Active state, pulse the border brightness.
		if (_state == SlotState.Active)
		{
			var pulse = 0.75f + 0.25f * Mathf.Sin(_pulseTimer * 4f);
			_border.BorderColor = BorderActive * pulse;
			// QueueRedraw handled below for the arc.
		}

		QueueRedraw(); // redraws the progress arc each frame
	}

	SlotState DetermineState()
	{
		if (_ultimate == null) return SlotState.Empty;

		// "Active" = the ultimate's buff is currently on the caster.
		if (!string.IsNullOrEmpty(_ultimate.ActiveEffectId)
		    && _player?.GetEffectById(_ultimate.ActiveEffectId) != null)
			return SlotState.Active;

		if (_ultimate.IsRequirementMet && !(_player?.IsOnCooldown(_ultimate) ?? false))
			return SlotState.Ready;

		return SlotState.Inactive;
	}

	void ApplyVisualState()
	{
		switch (_state)
		{
			case SlotState.Inactive:
				_border.BorderColor = BorderInactive;
				if (_dimOverlay != null) _dimOverlay.Visible = true;
				break;

			case SlotState.Ready:
				_border.BorderColor = BorderReady;
				if (_dimOverlay != null) _dimOverlay.Visible = false;
				break;

			case SlotState.Active:
				_border.BorderColor = BorderActive;
				if (_dimOverlay != null) _dimOverlay.Visible = false;
				break;

			case SlotState.Empty:
				_border.BorderColor = BorderInactive;
				if (_dimOverlay != null) _dimOverlay.Visible = false;
				break;
		}
	}

	// ── progress arc drawing ──────────────────────────────────────────────────

	public override void _Draw()
	{
		if (_ultimate == null || _state == SlotState.Active || _state == SlotState.Empty) return;

		var progress = _ultimate.Progress;
		var requirement = _ultimate.Requirement;
		if (requirement <= 0f) return;

		var fraction = Mathf.Clamp(progress / requirement, 0f, 1f);
		if (fraction <= 0f) return;

		// Draw a thin arc clockwise from 12 o'clock, filling as progress increases.
		// The arc sits just inside the slot border.
		var size = _panel.Size;
		var center = size / 2f;
		var radius = Mathf.Min(size.X, size.Y) / 2f - 4f;

		var sweepAngle = fraction * Mathf.Tau;
		var startAngle = -Mathf.Pi / 2f; // 12 o'clock

		var points = new List<Vector2> { center };

		var segments = Mathf.Max(3, Mathf.CeilToInt(ArcSegments * fraction));

		for (var i = 0; i <= segments; i++)
		{
			var angle = startAngle + (float)i / segments * sweepAngle;
			points.Add(center + new Vector2(
				Mathf.Cos(angle),
				Mathf.Sin(angle)
			) * radius);
		}

// Remove duplicate end-point for full circle
		if (fraction >= 1f)
			points.RemoveAt(points.Count - 1);

		DrawColoredPolygon(points.ToArray(), ArcColor);
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	static string GetKeybindLabel(string actionName)
	{
		var events = InputMap.ActionGetEvents(actionName);
		if (events.Count > 0 && events[0] is InputEventKey key)
			return OS.GetKeycodeString(key.PhysicalKeycode);
		return "R"; // sensible fallback
	}
}