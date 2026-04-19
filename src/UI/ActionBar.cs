using System.Collections.Generic;
using Godot;
using healerfantasy;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// WoW-style action bar: a row of spell slots, each showing the spell icon
/// and its keybind. The active slot (currently being cast) is highlighted
/// with a gold border for the duration of the cast.
/// </summary>
public partial class ActionBar : HBoxContainer
{
	// Keeps everything needed to update a single slot at runtime.
	record SlotInfo(SpellResource Spell, StyleBoxFlat BorderStyle);

	readonly List<SlotInfo> _slots = new();

	int   _activeIndex = -1;
	float _castTimer   = 0f;

	static readonly Color BorderDefault = new Color(0.25f, 0.22f, 0.20f);
	static readonly Color BorderActive  = new Color(0.95f, 0.80f, 0.10f); // gold

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		AddThemeConstantOverride("separation", 6);

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CastStarted),
			Callable.From((SpellResource spell) => OnCastStarted(spell))
		);
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CastCancelled),
			Callable.From(ClearActiveSlot)
		);
	}

	public override void _Process(double delta)
	{
		if (_activeIndex < 0) return;

		_castTimer -= (float)delta;
		if (_castTimer <= 0f)
			ClearActiveSlot();
	}

	// ── public API ───────────────────────────────────────────────────────────
	/// <summary>
	/// Build and append a slot for the given spell, labelled with the key
	/// bound to <paramref name="actionName"/> in the InputMap.
	/// </summary>
	public void AddSlot(SpellResource spell, string actionName)
	{
		var (panel, borderStyle) = BuildSlot(spell, actionName);
		_slots.Add(new SlotInfo(spell, borderStyle));
		AddChild(panel);
	}

	// ── private helpers ──────────────────────────────────────────────────────
	void ClearActiveSlot()
	{
		if (_activeIndex < 0) return;
		_slots[_activeIndex].BorderStyle.BorderColor = BorderDefault;
		_activeIndex = -1;
		_castTimer   = 0f;
	}

	void OnCastStarted(SpellResource spell)
	{
		ClearActiveSlot();

		for (var i = 0; i < _slots.Count; i++)
		{
			if (!ReferenceEquals(_slots[i].Spell, spell)) continue;

			_slots[i].BorderStyle.BorderColor = BorderActive;
			_activeIndex = i;
			_castTimer   = spell.CastTime;
			break;
		}
	}

	/// <summary>
	/// Derive a short display string from the action's first bound key.
	/// Falls back to stripping the "spell_" prefix if the InputMap has no events.
	/// </summary>
	static string GetKeybindLabel(string actionName)
	{
		var events = InputMap.ActionGetEvents(actionName);
		if (events.Count > 0 && events[0] is InputEventKey key)
			return OS.GetKeycodeString(key.PhysicalKeycode);

		return actionName.StartsWith("spell_")
			? actionName["spell_".Length..]
			: actionName;
	}

	// ── slot builder ─────────────────────────────────────────────────────────
	static (PanelContainer panel, StyleBoxFlat borderStyle) BuildSlot(
		SpellResource spell, string actionName)
	{
		// Outer frame
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(52, 52);

		var borderStyle = new StyleBoxFlat();
		borderStyle.BgColor = new Color(0.12f, 0.10f, 0.10f, 0.95f);
		borderStyle.SetCornerRadiusAll(4);
		borderStyle.SetBorderWidthAll(2);
		borderStyle.BorderColor         = BorderDefault;
		borderStyle.ContentMarginLeft   = 3f;
		borderStyle.ContentMarginRight  = 3f;
		borderStyle.ContentMarginTop    = 3f;
		borderStyle.ContentMarginBottom = 3f;
		panel.AddThemeStyleboxOverride("panel", borderStyle);

		// Inner control — acts as the stacking layer for icon + label
		var inner = new Control();
		inner.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		inner.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
		panel.AddChild(inner);

		// Spell icon — stretches to fill the slot
		if (spell?.Icon != null)
		{
			var icon = new TextureRect();
			icon.Texture     = spell.Icon;
			icon.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			inner.AddChild(icon);
		}

		// Keybind label — bottom-right corner, grows toward top-left
		var label = new Label();
		label.Text = GetKeybindLabel(actionName);
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color",        new Color(1.00f, 1.00f, 0.85f, 1.0f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.00f, 0.00f, 0.00f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
		label.GrowHorizontal = Control.GrowDirection.Begin;
		label.GrowVertical   = Control.GrowDirection.Begin;
		inner.AddChild(label);

		return (panel, borderStyle);
	}
}
