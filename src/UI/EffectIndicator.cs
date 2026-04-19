using Godot;

/// <summary>
/// A small icon badge displayed on a party frame while an effect is active.
/// Shows the effect's icon with a ceiling-integer countdown overlaid at the bottom.
/// Self-destructs when its timer reaches zero.
/// </summary>
public partial class EffectIndicator : PanelContainer
{
	public string EffectId { get; }

	float _remaining;
	Label _label;

	static readonly Color BorderColor = new Color(0.25f, 0.70f, 0.35f, 0.90f); // soft green

	public EffectIndicator(string effectId, Texture2D icon, float duration)
	{
		EffectId   = effectId;
		_remaining = duration;

		CustomMinimumSize = new Vector2(24, 24);
		MouseFilter       = MouseFilterEnum.Ignore;

		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.10f, 0.10f, 0.10f, 0.85f);
		style.SetCornerRadiusAll(3);
		style.SetBorderWidthAll(1);
		style.BorderColor         = BorderColor;
		style.ContentMarginLeft   = 1f;
		style.ContentMarginRight  = 1f;
		style.ContentMarginTop    = 1f;
		style.ContentMarginBottom = 1f;
		AddThemeStyleboxOverride("panel", style);

		// Stacking layer for icon + label
		var inner = new Control();
		inner.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(inner);

		// Spell icon fills the badge
		if (icon != null)
		{
			var iconRect = new TextureRect();
			iconRect.Texture     = icon;
			iconRect.ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.MouseFilter = MouseFilterEnum.Ignore;
			iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			inner.AddChild(iconRect);
		}

		// Countdown label — bottom-right, small with shadow for legibility over the icon
		_label = new Label();
		_label.MouseFilter = MouseFilterEnum.Ignore;
		_label.AddThemeFontSizeOverride("font_size", 9);
		_label.AddThemeColorOverride("font_color",        new Color(1.0f, 1.0f, 1.0f, 1.0f));
		_label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 1.0f));
		_label.AddThemeConstantOverride("shadow_offset_x", 1);
		_label.AddThemeConstantOverride("shadow_offset_y", 1);
		_label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		_label.GrowHorizontal = GrowDirection.Begin;
		_label.GrowVertical   = GrowDirection.Begin;
		inner.AddChild(_label);

		UpdateLabel();
	}

	public override void _Process(double delta)
	{
		_remaining -= (float)delta;
		if (_remaining <= 0f)
		{
			QueueFree();
			return;
		}
		UpdateLabel();
	}

	void UpdateLabel() =>
		_label.Text = Mathf.CeilToInt(_remaining).ToString();
}
