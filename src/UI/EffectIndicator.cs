using System.Text.RegularExpressions;
using Godot;
using healerfantasy;

/// <summary>
/// A small icon badge displayed on a party frame while an effect is active.
///
/// Layout (all inside the PanelContainer):
///        2            ← stack count, centered, breaks out above the top edge
///   ┌──────────┐
///   │  icon    │
///   │      10s │  ← duration, bottom-right inside the badge
///   └──────────┘
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
	Label _durationLabel;
	Label _stackLabel;
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
		MouseFilter = MouseFilterEnum.Stop;

		// ── badge style ──────────────────────────────────────────────────────
		_style = new StyleBoxFlat();
		_style.BgColor = new Color(0.10f, 0.10f, 0.10f, 0.85f);
		_style.SetCornerRadiusAll(3);
		_style.SetBorderWidthAll(2);

		var borderColor = effect.IsHarmful
			? effect.IsDispellable ? HarmfulDispellableBadgeBorder : HarmfulBadgeBorder
			: HelpfulBadgeBorder;

		_style.BorderColor = borderColor;
		_style.ContentMarginLeft = 1f;
		_style.ContentMarginRight = 1f;
		_style.ContentMarginTop = 1f;
		_style.ContentMarginBottom = 1f;

		AddThemeStyleboxOverride("panel", _style);
		SetupGlow(borderColor, effect.IsHarmful && effect.IsDispellable);

		// Stacking layer for icon + labels
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

		// Stack count — centered on the top edge, breaking out above the badge.
		_stackLabel = new Label();
		_stackLabel.MouseFilter = MouseFilterEnum.Ignore;
		_stackLabel.AddThemeFontSizeOverride("font_size", 10);
		_stackLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 1f));
		_stackLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 1f));
		_stackLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_stackLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		_stackLabel.AnchorLeft   = 0.5f;
		_stackLabel.AnchorRight  = 0.5f;
		_stackLabel.AnchorTop    = 0f;
		_stackLabel.AnchorBottom = 0f;
		_stackLabel.OffsetTop    = -9f;  // negative: break above the top border
		_stackLabel.OffsetBottom =  9f;  // 18 px tall hit area for the text
		_stackLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_stackLabel.GrowHorizontal = GrowDirection.Both;
		_stackLabel.GrowVertical   = GrowDirection.End;
		inner.AddChild(_stackLabel);

		// Duration — bottom-right inside the badge.
		// Full-width bottom strip (OffsetTop = -13 pulls the top edge up from the
		// bottom anchor), text right-aligned within it.  Explicit height avoids
		// the unreliable expansion that BottomRight preset + GrowDirection produces.
		_durationLabel = new Label();
		_durationLabel.MouseFilter = MouseFilterEnum.Ignore;
		_durationLabel.AddThemeFontSizeOverride("font_size", 9);
		_durationLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 1f));
		_durationLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 1f));
		_durationLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_durationLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		_durationLabel.AnchorLeft   = 0f;
		_durationLabel.AnchorRight  = 1f;
		_durationLabel.AnchorTop    = 1f;
		_durationLabel.AnchorBottom = 1f;
		_durationLabel.OffsetTop    = -13f; // 13 px strip at the very bottom of the icon
		_durationLabel.OffsetBottom = -1f;  // 1 px breathing room from the border
		_durationLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_durationLabel.GrowHorizontal = GrowDirection.Both;
		_durationLabel.GrowVertical   = GrowDirection.Begin;
		inner.AddChild(_durationLabel);

		UpdateLabels();

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
		UpdateLabels();

		if (_hovered)
		{
			var tooltip = TooltipText();
			GameTooltip.Show(tooltip.title, tooltip.desc);
		}
	}

	// ── private helpers ──────────────────────────────────────────────────────
	void UpdateLabels()
	{
		_stackLabel.Text = CharacterEffect.CurrentStacks > 1
			? CharacterEffect.CurrentStacks.ToString()
			: "";

		_durationLabel.Text = CharacterEffect.Remaining == GameConstants.InfiniteDuration
			? ""
			: Mathf.CeilToInt(CharacterEffect.Remaining) + "s";
	}

	(string title, string desc) TooltipText()
	{
		var description = CharacterEffect.Description;
		var durationText = CharacterEffect.Remaining == GameConstants.InfiniteDuration
			? ""
			: $"{Mathf.CeilToInt(CharacterEffect.Remaining)}s remaining";
		var body = string.IsNullOrEmpty(description)
			? ""
			: $"\n{description}";
		return (_displayName, $"{durationText}{body}");
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
		if (_glowRect == null)
		{
			_glowRect = new ColorRect();
			AddChild(_glowRect);
			MoveChild(_glowRect, 0); // behind everything

			_glowRect.MouseFilter = MouseFilterEnum.Ignore;
		}

		_glowRect.AnchorLeft   = 0;
		_glowRect.AnchorTop    = 0;
		_glowRect.AnchorRight  = 1;
		_glowRect.AnchorBottom = 1;

		_glowRect.OffsetLeft   = -3;
		_glowRect.OffsetTop    = -3;
		_glowRect.OffsetRight  =  3;
		_glowRect.OffsetBottom =  3;

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

		_pulseTween.TweenMethod(
				Callable.From<float>(t => { _style.BorderColor = baseColor.Lerp(bright, t); }),
				0f, 1f, 0.6f
			).SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);

		_pulseTween.TweenMethod(
			Callable.From<float>(t => { _style.BorderColor = baseColor.Lerp(bright, 1f - t); }),
			0f, 1f, 0.6f
		);

		_pulseTween.Parallel().TweenProperty(_glowRect, "modulate:a", 0.5f, 0.6f);
		_pulseTween.Parallel().TweenProperty(_glowRect, "modulate:a", 0.2f, 0.6f);
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
