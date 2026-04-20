using System.Collections.Generic;
using System.Linq;
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
	record SlotInfo(SpellResource Spell, StyleBoxFlat BorderStyle, TextureRect Icon);

	readonly List<SlotInfo> _slots = new();

	int _activeIndex = -1;
	float _castTimer = 0f;
	bool _isPlayerDead = false;

	static readonly Color BorderDefault = new(0.25f, 0.22f, 0.20f);
	static readonly Color BorderActive = new(0.95f, 0.80f, 0.10f); // gold

	// Shared greyscale shader — applied to the icon TextureRect when mana is too low.
	static ShaderMaterial _greyMaterial;
	static ShaderMaterial GreyMaterial => _greyMaterial ??= BuildGreyMaterial();

	static ShaderMaterial BuildGreyMaterial()
	{
		var shader = new Shader();
		shader.Code = """
		              shader_type canvas_item;
		              void fragment() {
		                  vec4 col = texture(TEXTURE, UV);
		                  float grey = dot(col.rgb, vec3(0.299, 0.587, 0.114));
		                  COLOR = vec4(vec3(grey * 0.55), col.a);
		              }
		              """;
		var mat = new ShaderMaterial();
		mat.Shader = shader;
		return mat;
	}

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

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.Died),
			Callable.From((string charName) => SetIconShadingBasedOnCharacterDeath(charName))
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
		var (panel, borderStyle, icon) = BuildSlot(spell, actionName);
		_slots.Add(new SlotInfo(spell, borderStyle, icon));
		AddChild(panel);
	}

	/// <summary>
	/// Called whenever the player's mana changes.
	/// Greys out any slot whose spell costs more than <paramref name="current"/> mana.
	/// </summary>
	public void SetIconShadingBasedOnPlayerMana(float current, float max)
	{
		foreach (var slot in _slots.Where(slot => slot.Icon != null))
		{
			slot.Icon.Material = _isPlayerDead || current < slot.Spell.ManaCost ? GreyMaterial : null;
		}
	}

	void SetIconShadingBasedOnCharacterDeath(string charName)
	{
		if (charName != GameConstants.PlayerName) return;
		_isPlayerDead = true;
	}

	// ── private helpers ──────────────────────────────────────────────────────
	void ClearActiveSlot()
	{
		if (_activeIndex < 0) return;
		_slots[_activeIndex].BorderStyle.BorderColor = BorderDefault;
		_activeIndex = -1;
		_castTimer = 0f;
	}

	void OnCastStarted(SpellResource spell)
	{
		ClearActiveSlot();

		for (var i = 0; i < _slots.Count; i++)
		{
			if (!ReferenceEquals(_slots[i].Spell, spell)) continue;

			_slots[i].BorderStyle.BorderColor = BorderActive;
			_activeIndex = i;
			_castTimer = spell.CastTime;
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
	static (PanelContainer panel, StyleBoxFlat borderStyle, TextureRect icon) BuildSlot(
		SpellResource spell, string actionName)
	{
		// Outer frame
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(52, 52);

		// TODO: This doesn't show?
		panel.TooltipText = spell.Name + "\n" + spell.Description;

		var borderStyle = new StyleBoxFlat();
		borderStyle.BgColor = new Color(0.12f, 0.10f, 0.10f, 0.95f);
		borderStyle.SetCornerRadiusAll(4);
		borderStyle.SetBorderWidthAll(2);
		borderStyle.BorderColor = BorderDefault;
		borderStyle.ContentMarginLeft = 3f;
		borderStyle.ContentMarginRight = 3f;
		borderStyle.ContentMarginTop = 3f;
		borderStyle.ContentMarginBottom = 3f;
		panel.AddThemeStyleboxOverride("panel", borderStyle);

		// Inner control — acts as the stacking layer for icon + label
		var inner = new Control();
		inner.MouseFilter = MouseFilterEnum.Ignore;
		inner.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		inner.SizeFlagsVertical = SizeFlags.ExpandFill;
		panel.AddChild(inner);

		// Spell icon — stretches to fill the slot
		TextureRect iconRect = null;
		if (spell?.Icon != null)
		{
			iconRect = new TextureRect();
			iconRect.Texture = spell.Icon;
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			inner.AddChild(iconRect);
		}

		// Keybind label — bottom-right corner, grows toward top-left
		var label = new Label();
		label.Text = GetKeybindLabel(actionName);
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", new Color(1.00f, 1.00f, 0.85f, 1.0f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.00f, 0.00f, 0.00f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		label.GrowHorizontal = GrowDirection.Begin;
		label.GrowVertical = GrowDirection.Begin;
		inner.AddChild(label);

		return (panel, borderStyle, iconRect);
	}
}