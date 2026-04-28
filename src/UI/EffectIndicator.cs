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
	Label _countLabel;
	bool _hovered;

	StyleBoxFlat _style;
	ColorRect _glowRect;
	Tween _pulseTween;

	// Helpful buff color
	static readonly Color HelpfulBadgeBorder = new(0.25f, 0.70f, 0.35f, 0.90f);

	// Harmful buff color
	static readonly Color HarmfulBadgeBorder = new(0.70f, 0.25f, 0.35f, 0.90f);

	// Bright orange color for dispellable harmful effects 
	static readonly Color HarmfulDispellableBadgeBorder = new(0.90f, 0.40f, 0.10f, 0.95f);

	// ── constructor ──────────────────────────────────────────────────────────
	public EffectIndicator(CharacterEffect effect, int indicatorSize = 34)
	{
		CharacterEffect = effect;
		_displayName = FormatDisplayName(effect.EffectId);

		CustomMinimumSize = new Vector2(indicatorSize, indicatorSize);
		MouseFilter = MouseFilterEnum.Stop; // must be non-Ignore to receive mouse events

		// ── badge style ──────────────────────────────────────────────────────
		_style = new StyleBoxFlat();
		_style.BgColor = new Color(0.10f, 0.10f, 0.10f, 0.85f);
		_style.SetCornerRadiusAll(3);
		_style.SetBorderWidthAll(2);

		var borderColor = effect.IsHarmful
			? effect.IsDispellable ? HarmfulDispellableBadgeBorder : HarmfulBadgeBorder
			: HelpfulBadgeBorder;

		_style.BorderColor = borderColor;
		// Glow dispellable effects
		SetupGlow(borderColor, effect.IsHarmful && effect.IsDispellable);

		_style.ContentMarginLeft = 1f;
		_style.ContentMarginRight = 1f;
		_style.ContentMarginTop = 1f;
		_style.ContentMarginBottom = 1f;


		AddThemeStyleboxOverride("panel", _style);

		// Stacking layer for icon + countdown label
		var inner = new Control();
		inner.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(inner);

		// Effect icon fills the badge
		if (effect.Icon != null)
		{
			var iconRect = new TextureRect();
			iconRect.Texture = effect.Icon;
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.MouseFilter = MouseFilterEnum.Ignore;
			iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			inner.AddChild(iconRect);
		}

		// Countdown label — bottom-right corner, small + shadowed for legibility
		_countLabel = new Label();
		_countLabel.MouseFilter = MouseFilterEnum.Ignore;
		_countLabel.AddThemeFontSizeOverride("font_size", 9);
		_countLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 1f));
		_countLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 1f));
		_countLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_countLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		_countLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		_countLabel.GrowHorizontal = GrowDirection.Begin;
		_countLabel.GrowVertical = GrowDirection.Begin;
		inner.AddChild(_countLabel);

		UpdateCountLabel();

		// ── tooltip wiring ───────────────────────────────────────────────────
		MouseEntered += () =>
		{
			_hovered = true;
			var tooltip = TooltipText();
			GameTooltip.Show(tooltip.title, tooltip.desc);
		};
		MouseExited += () =>
		{
			_hovered = false;
			GameTooltip.Hide();
		};
	}

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Process(double delta)
	{
		UpdateCountLabel();

		// Refresh the live countdown in the tooltip while the mouse is over the badge.
		if (_hovered)
		{
			var tooltip = TooltipText();
			GameTooltip.Show(tooltip.title, tooltip.desc);
		}
	}

	// ── private helpers ──────────────────────────────────────────────────────
	void UpdateCountLabel()
	{
		_countLabel.Text = Mathf.CeilToInt(CharacterEffect.Remaining).ToString();
	}

	(string title, string desc) TooltipText()
	{
		var description = CharacterEffect.Description;
		var body = string.IsNullOrEmpty(description)
			? ""
			: $"{description}\n";
		return (_displayName, $"{Mathf.CeilToInt(CharacterEffect.Remaining)}s remaining\n{body}");
	}

	/// <summary>
	/// Converts a PascalCase effect ID into a space-separated display name.
	/// "ShieldingReinvigoration" → "Shielding Reinvigoration"
	/// </summary>
	static string FormatDisplayName(string id)
	{
		return Regex.Replace(id, @"(?<=[a-z])(?=[A-Z])", " ");
	}
	void SetupGlow(Color color, bool shouldPulse)
	{
		// Create once
		if (_glowRect == null)
		{
			_glowRect = new ColorRect();
			AddChild(_glowRect);
			MoveChild(_glowRect, 0); // ensure it's behind

			_glowRect.MouseFilter = MouseFilterEnum.Ignore;
		}

		// Slightly bigger than the panel
		_glowRect.AnchorLeft = 0;
		_glowRect.AnchorTop = 0;
		_glowRect.AnchorRight = 1;
		_glowRect.AnchorBottom = 1;

		_glowRect.OffsetLeft = -3;
		_glowRect.OffsetTop = -3;
		_glowRect.OffsetRight = 3;
		_glowRect.OffsetBottom = 3;

		// Soft glow color
		_glowRect.Color = new Color(color.R, color.G, color.B, 0.25f);

		if (shouldPulse)
			StartPulse(color);
		else
			StopPulse();
	}
	void StartPulse(Color baseColor)
	{
		StopPulse();

		var bright = baseColor.Lightened(0.5f);

		_pulseTween = CreateTween().SetLoops();

		// Border pulse
		_pulseTween.TweenMethod(
				Callable.From<float>(t => { _style.BorderColor = baseColor.Lerp(bright, t); }),
				0f, 1f, 0.6f
			).SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);

		_pulseTween.TweenMethod(
			Callable.From<float>(t => { _style.BorderColor = baseColor.Lerp(bright, 1f - t); }),
			0f, 1f, 0.6f
		);

		// Glow alpha pulse (subtle)
		_pulseTween.Parallel().TweenProperty(
			_glowRect, "modulate:a", 0.5f, 0.6f);

		_pulseTween.Parallel().TweenProperty(
			_glowRect, "modulate:a", 0.2f, 0.6f);
	}
	void StopPulse()
	{
		if (_pulseTween != null)
		{
			_pulseTween.Kill();
			_pulseTween = null;
		}
	}
}