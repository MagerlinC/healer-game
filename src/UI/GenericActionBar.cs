using System.Collections.Generic;
using Godot;
using healerfantasy;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// A fixed 2-slot action bar for the player's always-available generic spells
/// (Dispel and Deflect). Unlike the regular action bar these slots are never
/// editable — the spells are always populated and cannot be swapped out.
///
/// Bound to input actions <c>generic_1</c> and <c>generic_2</c>; keybind
/// labels update automatically when the player rebinds those actions.
///
/// Wiring: add as a sibling of <see cref="ActionBar"/> inside GameUI.
/// </summary>
public partial class GenericActionBar : HBoxContainer
{
	record SlotInfo(SpellResource Spell, StyleBoxFlat BorderStyle, CooldownOverlay? Overlay);

	readonly List<SlotInfo> _slots = new();

	// Slightly purple-tinged border to visually distinguish from the regular bar.
	static readonly Color BorderDefault = new(0.32f, 0.24f, 0.36f);

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		AddThemeConstantOverride("separation", 6);
	}

	/// <summary>
	/// Populate the two fixed slots from the player's <see cref="Player.GenericSpells"/>.
	/// Call this from GameUI after the Player node is resolved.
	/// </summary>
	public void Build(Player player)
	{
		foreach (var child in GetChildren())
			child.QueueFree();
		_slots.Clear();

		var actions = new[] { "generic_1", "generic_2" };
		for (var i = 0; i < player.GenericSpells.Length; i++)
		{
			var spell = player.GenericSpells[i];
			var (panel, border, overlay) = BuildSlot(spell, actions[i]);
			_slots.Add(new SlotInfo(spell, border, overlay));
			AddChild(panel);
		}

		// Listen for cooldown events so the overlays animate correctly.
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CooldownStarted),
			Callable.From((SpellResource spell, float duration) => OnCooldownStarted(spell, duration))
		);
	}

	public override void _Process(double delta)
	{
		foreach (var slot in _slots)
			slot.Overlay?.Tick((float)delta);
	}

	// ── private helpers ──────────────────────────────────────────────────────

	void OnCooldownStarted(SpellResource spell, float duration)
	{
		foreach (var slot in _slots)
		{
			if (!ReferenceEquals(slot.Spell, spell)) continue;
			slot.Overlay?.Start(duration);
			break;
		}
	}

	static (PanelContainer panel, StyleBoxFlat border, CooldownOverlay? overlay) BuildSlot(
		SpellResource spell, string actionName)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(52, 52);

		var border = new StyleBoxFlat();
		border.BgColor      = new Color(0.10f, 0.08f, 0.12f, 0.95f);
		border.SetCornerRadiusAll(4);
		border.SetBorderWidthAll(2);
		border.BorderColor  = BorderDefault;
		border.ContentMarginLeft   = 3f;
		border.ContentMarginRight  = 3f;
		border.ContentMarginTop    = 3f;
		border.ContentMarginBottom = 3f;
		panel.AddThemeStyleboxOverride("panel", border);

		var inner = new Control();
		inner.MouseFilter          = MouseFilterEnum.Ignore;
		inner.SizeFlagsHorizontal  = SizeFlags.ExpandFill;
		inner.SizeFlagsVertical    = SizeFlags.ExpandFill;
		inner.ClipContents         = true;
		panel.AddChild(inner);

		// Spell icon (optional — null is fine if no icon asset is assigned yet).
		if (spell.Icon != null)
		{
			var iconRect            = new TextureRect();
			iconRect.Texture        = spell.Icon;
			iconRect.ExpandMode     = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode    = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			inner.AddChild(iconRect);
		}

		// Cooldown overlay.
		var overlay          = new CooldownOverlay();
		overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		overlay.MouseFilter  = MouseFilterEnum.Ignore;
		inner.AddChild(overlay);

		// Keybind label — reads live from InputMap so rebinds are reflected instantly.
		var label = new Label();
		label.Text = GetKeybindLabel(actionName);
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color",        new Color(1.00f, 1.00f, 0.85f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.00f, 0.00f, 0.00f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		label.GrowHorizontal = GrowDirection.Begin;
		label.GrowVertical   = GrowDirection.Begin;
		inner.AddChild(label);

		// Tooltip.
		var tooltipText = GameTooltip.FormatSpellTooltip(spell);
		panel.MouseEntered += () => GameTooltip.Show(tooltipText);
		panel.MouseExited  += () => GameTooltip.Hide();

		return (panel, border, overlay);
	}

	static string GetKeybindLabel(string actionName)
	{
		var events = InputMap.ActionGetEvents(actionName);
		if (events.Count > 0 && events[0] is InputEventKey key)
			return OS.GetKeycodeString(key.PhysicalKeycode);

		// Fallback: generic_1 → "G1", generic_2 → "G2".
		return actionName.StartsWith("generic_")
			? "G" + actionName["generic_".Length..]
			: actionName;
	}
}
