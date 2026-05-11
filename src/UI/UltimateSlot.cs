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
///             Dim square border track, slightly dimmed icon, border fills
///             clockwise from 12 o'clock as progress accumulates.
///   Ready    — requirement met, spell not on cooldown.
///             Full bright gold square border, full-brightness icon.
///   Active   — ultimate's buff is currently running on the caster.
///             Full pulsing arcane-purple square border, bright icon.
///
/// The border is drawn by a <see cref="BorderOverlay"/> child node added on top
/// of the icon panel so it is always visible over the icon texture.
/// </summary>
public partial class UltimateSlot : Control
{
	// ── colours ──────────────────────────────────────────────────────────────
	static readonly Color BgColor = new(0.10f, 0.07f, 0.14f, 0.95f);
	static readonly Color OverlayDim = new(0f, 0f, 0f, 0.42f);

	// ── child refs ───────────────────────────────────────────────────────────
	PanelContainer _panel = null!;
	StyleBoxFlat _border = null!;
	TextureRect? _icon = null;
	Control _inner = null!;
	ColorRect? _dimOverlay = null;
	CooldownOverlay? _cooldown = null;
	BorderOverlay _borderOverlay = null!;

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

	// ── border overlay ────────────────────────────────────────────────────────
	/// <summary>
	/// Draws a rectangular progress border on top of the slot icon.
	/// Added as a sibling of <c>_panel</c> so it renders above all icon content.
	/// </summary>
	sealed partial class BorderOverlay : Control
	{
		public UltimateSpellResource? Ultimate;
		public SlotState State = SlotState.Empty;
		public float PulseTimer = 0f;

		static readonly Color TrackColor = new(0.20f, 0.14f, 0.28f, 0.5f); // dim track
		static readonly Color FillColor = new(0.65f, 0.22f, 0.90f, 0.85f); // progress fill (purple)
		static readonly Color ReadyColor = new(0.95f, 0.80f, 0.10f, 1.0f); // gold
		static readonly Color ActiveColor = new(0.65f, 0.22f, 0.90f, 1.0f); // arcane purple

		const float BW = 3.5f; // border stroke width

		public override void _Draw()
		{
			if (Ultimate == null || State == SlotState.Empty) return;

			float w = Size.X, h = Size.Y, half = BW / 2f;

			// Perimeter waypoints going clockwise from top-centre back to top-centre.
			// Segments: [0→1] right along top, [1→2] down right side,
			//           [2→3] left along bottom, [3→4] up left side, [4→5] right to close.
			var pts = new Vector2[]
			{
				new(w / 2f, half), // 0  top-centre (start)
				new(w - half, half), // 1  top-right corner
				new(w - half, h - half), // 2  bottom-right corner
				new(half, h - half), // 3  bottom-left corner
				new(half, half), // 4  top-left corner
				new(w / 2f, half) // 5  top-centre (close)
			};

			switch (State)
			{
				case SlotState.Inactive:
					// Full dim track so the "empty" border is always visible.
					DrawBorder(pts, TrackColor, 1f);
					// Bright fill sweeps clockwise as progress grows.
					if (Ultimate.Requirement > 0f)
					{
						var fraction = Mathf.Clamp(Ultimate.Progress / Ultimate.Requirement, 0f, 1f);
						if (fraction > 0f)
							DrawBorder(pts, FillColor, fraction);
					}

					break;

				case SlotState.Ready:
					DrawBorder(pts, ReadyColor, 1f);
					break;

				case SlotState.Active:
					var pulse = 0.75f + 0.25f * Mathf.Sin(PulseTimer * 4f);
					DrawBorder(pts, ActiveColor * pulse, 1f);
					break;
			}
		}

		/// <summary>
		/// Traces the perimeter waypoints with a thick line up to <paramref name="fraction"/>
		/// of the total length. Corner joins are filled with a small circle so there are
		/// no gaps where two perpendicular segments meet.
		/// </summary>
		void DrawBorder(Vector2[] pts, Color color, float fraction)
		{
			// Pre-compute total perimeter length.
			var total = 0f;
			for (var i = 0; i < pts.Length - 1; i++)
				total += pts[i].DistanceTo(pts[i + 1]);

			var target = fraction * total;
			var traveled = 0f;

			for (var i = 0; i < pts.Length - 1; i++)
			{
				if (traveled >= target) break;

				var segLen = pts[i].DistanceTo(pts[i + 1]);
				var remaining = target - traveled;
				var segEnd = remaining >= segLen
					? pts[i + 1]
					: pts[i].Lerp(pts[i + 1], remaining / segLen);

				DrawLine(pts[i], segEnd, color, BW, true);

				// Fill the inner corner gap when we complete a full segment (except the
				// closing segment that returns to the start point, which is mid-edge).
				var completedSegment = remaining >= segLen;
				var isCorner = completedSegment && i + 1 < pts.Length - 1;
				if (isCorner)
					DrawCircle(pts[i + 1], BW / 2f, color);

				traveled += segLen;
			}
		}
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(58, 58);
		MouseFilter = MouseFilterEnum.Ignore;

		_panel = new PanelContainer();
		_panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_border = new StyleBoxFlat();
		_border.BgColor = BgColor;
		_border.SetCornerRadiusAll(5);
		_border.SetBorderWidthAll(0); // all border drawing is done by BorderOverlay
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

		// BorderOverlay is added as a sibling of _panel AFTER it in UltimateSlot,
		// so Godot renders it on top of the icon and all other panel content.
		_borderOverlay = new BorderOverlay();
		_borderOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_borderOverlay.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_borderOverlay);
	}

	/// <summary>
	/// Bind the slot to a player and their equipped ultimate.
	/// Call from GameUI after the Player node is resolved.
	/// </summary>
	public void Bind(Player player, UltimateSpellResource? ultimate)
	{
		_player = player;
		_ultimate = ultimate;

		foreach (var child in _inner.GetChildren())
			child.QueueFree();
		_icon = null;
		_dimOverlay = null;
		_cooldown = null;

		_borderOverlay.Ultimate = ultimate;

		if (ultimate == null)
		{
			_state = SlotState.Empty;
			_borderOverlay.State = SlotState.Empty;
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
		_borderOverlay.State = SlotState.Inactive;
		_borderOverlay.QueueRedraw();
	}

	// ── per-frame update ──────────────────────────────────────────────────────

	public override void _Process(double delta)
	{
		_cooldown?.Tick((float)delta);

		if (_ultimate == null || _player == null) return;

		_pulseTimer += (float)delta;

		var newState = DetermineState();
		if (newState != _state)
		{
			_state = newState;
			ApplyVisualState();
		}

		// Push latest data to the overlay and request a redraw every frame so the
		// progress fill and Active pulse stay smooth.
		_borderOverlay.PulseTimer = _pulseTimer;
		_borderOverlay.QueueRedraw();
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
		// Dim overlay is only shown in Inactive state; border colour is handled by BorderOverlay.
		if (_dimOverlay != null)
			_dimOverlay.Visible = _state == SlotState.Inactive;

		_borderOverlay.State = _state;
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