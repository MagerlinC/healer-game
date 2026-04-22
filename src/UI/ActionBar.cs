using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// WoW-style action bar: a row of spell slots, each showing the spell icon
/// and its keybind. The active slot (currently being cast) is highlighted
/// with a gold border for the duration of the cast.
/// Empty slots (null spells) are rendered as dim placeholders so the bar
/// always shows all <see cref="Player.MaxSpellSlots"/> positions.
/// </summary>
public partial class ActionBar : HBoxContainer
{
	// Keeps everything needed to update a single slot at runtime.
	record SlotInfo(SpellResource? Spell, StyleBoxFlat BorderStyle, TextureRect? Icon, CooldownOverlay? Overlay);

	readonly List<SlotInfo> _slots = new();

	int _activeIndex = -1;
	float _castTimer = 0f;
	bool _isPlayerDead = false;

	static readonly Color BorderDefault = new(0.25f, 0.22f, 0.20f);
	static readonly Color BorderActive = new(0.95f, 0.80f, 0.10f); // gold
	static readonly Color BorderEmpty = new(0.18f, 0.16f, 0.14f); // near-black for empty slots

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
			Callable.From((SpellResource spell, float adjustedCastTime) => OnCastStarted(spell, adjustedCastTime))
		);
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CastCancelled),
			Callable.From(ClearActiveSlot)
		);
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CooldownStarted),
			Callable.From((SpellResource spell, float duration) => OnCooldownStarted(spell, duration))
		);
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.GlobalCooldownStarted),
			Callable.From((float duration) => OnGlobalCooldownStarted(duration))
		);
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.Died),
			Callable.From((Character character) => SetIconShadingBasedOnCharacterDeath(character))
		);
	}

	public override void _Process(double delta)
	{
		if (_activeIndex >= 0)
		{
			_castTimer -= (float)delta;
			if (_castTimer <= 0f)
				ClearActiveSlot();
		}

		// Tick cooldown overlays every frame regardless of cast state.
		foreach (var slot in _slots)
			slot.Overlay?.Tick((float)delta);
	}

	// ── public API ───────────────────────────────────────────────────────────

	/// <summary>
	/// Rebuild the bar from <paramref name="equipped"/>, one slot per entry.
	/// Null entries render as empty placeholder slots so the bar always has
	/// <see cref="Player.MaxSpellSlots"/> visible positions.
	/// Safe to call at any time; clears and repopulates all children.
	/// </summary>
	public void Rebuild(SpellResource?[] equipped)
	{
		// Remove all current slot nodes
		foreach (var child in GetChildren())
			child.QueueFree();
		_slots.Clear();
		_activeIndex = -1;
		_castTimer = 0f;

		for (var i = 0; i < equipped.Length; i++)
		{
			var spell = equipped[i];
			var actionName = $"spell_{i + 1}";
			var (panel, borderStyle, icon, overlay) = BuildSlot(spell, actionName);
			_slots.Add(new SlotInfo(spell, borderStyle, icon, overlay));
			AddChild(panel);
		}
	}

	/// <summary>
	/// Called whenever the player's mana changes.
	/// Greys out any slot whose spell costs more than <paramref name="current"/> mana.
	/// </summary>
	public void SetIconShadingBasedOnPlayerMana(float current, float max)
	{
		foreach (var slot in _slots.Where(s => s.Icon != null))
		{
			slot.Icon!.Material = _isPlayerDead || slot.Spell != null && current < slot.Spell.ManaCost
				? GreyMaterial
				: null;
		}
	}

	void SetIconShadingBasedOnCharacterDeath(Character character)
	{
		if (character.Name != GameConstants.PlayerName) return;
		_isPlayerDead = true;
	}

	// ── private helpers ──────────────────────────────────────────────────────
	void ClearActiveSlot()
	{
		if (_activeIndex < 0) return;
		_slots[_activeIndex].BorderStyle.BorderColor = _slots[_activeIndex].Spell != null
			? BorderDefault
			: BorderEmpty;
		_activeIndex = -1;
		_castTimer = 0f;
	}

	void OnCastStarted(SpellResource spell, float adjustedCastTime)
	{
		ClearActiveSlot();

		for (var i = 0; i < _slots.Count; i++)
		{
			if (!ReferenceEquals(_slots[i].Spell, spell)) continue;

			_slots[i].BorderStyle.BorderColor = BorderActive;
			_activeIndex = i;
			_castTimer = adjustedCastTime;
			break;
		}
	}

	void OnCooldownStarted(SpellResource spell, float duration)
	{
		foreach (var slot in _slots)
		{
			if (!ReferenceEquals(slot.Spell, spell)) continue;
			slot.Overlay?.Start(duration);
			break;
		}
	}

	void OnGlobalCooldownStarted(float duration)
	{
		foreach (var slot in _slots)
		{
			if (slot.Overlay == null) continue;
			// Don't override a spell that's already on a longer individual cooldown —
			// its sweep is already running and should keep counting down undisturbed.
			if (slot.Overlay.IsActive && slot.Overlay.Remaining >= duration) continue;
			slot.Overlay.Start(duration);
		}
	}

	/// <summary>
	/// Derive a short display string from the action's first bound key.
	/// Falls back to stripping the "spell_" prefix if the InputMap has no events.
	/// Reading directly from InputMap means player-rebound keys are reflected
	/// automatically without any code changes.
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
	static (PanelContainer panel, StyleBoxFlat borderStyle, TextureRect? icon, CooldownOverlay? overlay) BuildSlot(
		SpellResource? spell, string actionName)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(52, 52);

		var borderStyle = new StyleBoxFlat();
		borderStyle.BgColor = new Color(0.12f, 0.10f, 0.10f, 0.95f);
		borderStyle.SetCornerRadiusAll(4);
		borderStyle.SetBorderWidthAll(2);
		borderStyle.BorderColor = spell != null ? BorderDefault : BorderEmpty;
		borderStyle.ContentMarginLeft = 3f;
		borderStyle.ContentMarginRight = 3f;
		borderStyle.ContentMarginTop = 3f;
		borderStyle.ContentMarginBottom = 3f;
		panel.AddThemeStyleboxOverride("panel", borderStyle);

		var inner = new Control();
		inner.MouseFilter = MouseFilterEnum.Ignore;
		inner.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		inner.SizeFlagsVertical = SizeFlags.ExpandFill;
		inner.ClipContents = true; // prevents the cooldown overlay polygon from drawing outside the slot
		panel.AddChild(inner);

		// Spell icon — only added when the slot is filled
		TextureRect? iconRect = null;
		if (spell?.Icon != null)
		{
			iconRect = new TextureRect();
			iconRect.Texture = spell.Icon;
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			inner.AddChild(iconRect);
		}

		// Cooldown overlay — sits above the icon, below the keybind label.
		// Only created for filled slots; empty slots can never have cooldowns.
		CooldownOverlay? overlay = null;
		if (spell != null)
		{
			overlay = new CooldownOverlay();
			overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			overlay.MouseFilter = MouseFilterEnum.Ignore;
			inner.AddChild(overlay);
		}

		// Keybind label — always shown so empty slots are clearly numbered
		var label = new Label();
		label.Text = GetKeybindLabel(actionName);
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", new Color(1.00f, 1.00f, 0.85f, spell != null ? 1.0f : 0.35f));
		label.AddThemeColorOverride("font_shadow_color", new Color(0.00f, 0.00f, 0.00f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		label.GrowHorizontal = GrowDirection.Begin;
		label.GrowVertical = GrowDirection.Begin;
		inner.AddChild(label);

		// Tooltip — uses the shared GameTooltip singleton so it works inside CanvasLayer.
		if (spell != null)
		{
			var tooltipText = GameTooltip.FormatSpellTooltip(spell);
			panel.MouseEntered += () => GameTooltip.Show(tooltipText);
			panel.MouseExited += () => GameTooltip.Hide();
		}

		return (panel, borderStyle, iconRect, overlay);
	}
}