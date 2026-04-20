using System.Text.RegularExpressions;
using Godot;
using healerfantasy;

/// <summary>
/// A small icon badge displayed on a party frame while an effect is active.
/// Shows the effect's icon with a ceiling-integer countdown overlaid at the bottom.
/// Self-destructs when its timer reaches zero.
///
/// Hovering the badge shows the shared <see cref="GameTooltip"/> with the
/// effect's display name and a live remaining-duration countdown.
/// </summary>
public partial class EffectIndicator : PanelContainer
{
	// ── public ───────────────────────────────────────────────────────────────
	public CharacterEffect CharacterEffect { get; private set; }

	// ── private ───────────────────────────────────────────────────────────────
	string _displayName;
	Label  _countLabel;
	bool   _hovered;

	static readonly Color BadgeBorder = new(0.25f, 0.70f, 0.35f, 0.90f);

	// ── constructor ──────────────────────────────────────────────────────────
	public EffectIndicator(CharacterEffect effect)
	{
		CharacterEffect = effect;
		_displayName    = FormatDisplayName(effect.EffectId);

		CustomMinimumSize = new Vector2(24, 24);
		MouseFilter       = MouseFilterEnum.Stop; // must be non-Ignore to receive mouse events

		// ── badge style ──────────────────────────────────────────────────────
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.10f, 0.10f, 0.10f, 0.85f);
		style.SetCornerRadiusAll(3);
		style.SetBorderWidthAll(1);
		style.BorderColor        = BadgeBorder;
		style.ContentMarginLeft  = 1f;
		style.ContentMarginRight = 1f;
		style.ContentMarginTop   = 1f;
		style.ContentMarginBottom = 1f;
		AddThemeStyleboxOverride("panel", style);

		// Stacking layer for icon + countdown label
		var inner = new Control();
		inner.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(inner);

		// Effect icon fills the badge
		if (effect.Icon != null)
		{
			var iconRect = new TextureRect();
			iconRect.Texture     = effect.Icon;
			iconRect.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.MouseFilter = MouseFilterEnum.Ignore;
			iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			inner.AddChild(iconRect);
		}

		// Countdown label — bottom-right corner, small + shadowed for legibility
		_countLabel = new Label();
		_countLabel.MouseFilter = MouseFilterEnum.Ignore;
		_countLabel.AddThemeFontSizeOverride("font_size", 9);
		_countLabel.AddThemeColorOverride("font_color",        new Color(1f, 1f, 1f, 1f));
		_countLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 1f));
		_countLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_countLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		_countLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		_countLabel.GrowHorizontal = GrowDirection.Begin;
		_countLabel.GrowVertical   = GrowDirection.Begin;
		inner.AddChild(_countLabel);

		UpdateCountLabel();

		// ── tooltip wiring ───────────────────────────────────────────────────
		MouseEntered += () => { _hovered = true;  GameTooltip.Show(TooltipText()); };
		MouseExited  += () => { _hovered = false; GameTooltip.Hide(); };
	}

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Process(double delta)
	{
		UpdateCountLabel();

		// Refresh the live countdown in the tooltip while the mouse is over the badge.
		if (_hovered)
			GameTooltip.Show(TooltipText());
	}

	// ── private helpers ──────────────────────────────────────────────────────
	void UpdateCountLabel()
	{
		_countLabel.Text = Mathf.CeilToInt(CharacterEffect.Remaining).ToString();
	}

	string TooltipText() =>
		$"{_displayName}\n{Mathf.CeilToInt(CharacterEffect.Remaining)}s remaining";

	/// <summary>
	/// Converts a PascalCase effect ID into a space-separated display name.
	/// "ShieldingReinvigoration" → "Shielding Reinvigoration"
	/// </summary>
	static string FormatDisplayName(string id) =>
		Regex.Replace(id, @"(?<=[a-z])(?=[A-Z])", " ");
}
