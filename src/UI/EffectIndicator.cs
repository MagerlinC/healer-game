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
///
/// Dispellable harmful effects get an enhanced treatment: a larger soft glow
/// halo (via a canvas-item shader), a faster border pulse, and a repeating
/// scale-bounce animation so they stand out clearly from regular buffs.
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
	Tween _bounceTween;

	// Helpful buff color
	static readonly Color HelpfulBadgeBorder = new(0.25f, 0.70f, 0.35f, 0.90f);

	// Harmful buff color
	static readonly Color HarmfulBadgeBorder = new(0.70f, 0.25f, 0.35f, 0.90f);

	// Bright orange color for dispellable harmful effects
	static readonly Color HarmfulDispellableBadgeBorder = new(0.90f, 0.40f, 0.10f, 0.95f);

	// Mirror Image clone effect — matches the ghostly blue-cyan tint on MirrorImageClone
	static readonly Color MirrorBadgeBorder = new(0.35f, 0.72f, 1.00f, 0.90f);
	static readonly Color MirrorBgColor     = new(0.07f, 0.11f, 0.20f, 0.90f);

	const string MirrorSuffix = "_Mirror";

	// ── constructor ──────────────────────────────────────────────────────────
	public EffectIndicator(CharacterEffect effect, int indicatorSize = 34)
	{
		CharacterEffect = effect;

		var isMirror = effect.EffectId.EndsWith(MirrorSuffix);
		var isDispellableHarmful = effect.IsHarmful && effect.IsDispellable;

		// Strip the internal "_Mirror" suffix so the tooltip reads cleanly,
		// then annotate with "(Mirror)" so the player knows it's the clone's copy.
		var baseId = isMirror ? effect.EffectId[..^MirrorSuffix.Length] : effect.EffectId;
		_displayName = isMirror
			? FormatDisplayName(baseId) + " (Mirror)"
			: FormatDisplayName(baseId);

		CustomMinimumSize = new Vector2(indicatorSize, indicatorSize);
		// Centre the pivot so the bounce scale animation grows from the middle of the badge.
		PivotOffset = new Vector2(indicatorSize / 2f, indicatorSize / 2f);
		MouseFilter = MouseFilterEnum.Stop;

		// ── badge style ──────────────────────────────────────────────────────
		_style = new StyleBoxFlat();
		_style.BgColor = isMirror
			? MirrorBgColor
			: new Color(0.10f, 0.10f, 0.10f, 0.85f);
		_style.SetCornerRadiusAll(3);
		_style.SetBorderWidthAll(2);

		var borderColor = isMirror
			? MirrorBadgeBorder
			: effect.IsHarmful
				? effect.IsDispellable ? HarmfulDispellableBadgeBorder : HarmfulBadgeBorder
				: HelpfulBadgeBorder;

		_style.BorderColor = borderColor;
		_style.ContentMarginLeft = 1f;
		_style.ContentMarginRight = 1f;
		_style.ContentMarginTop = 1f;
		_style.ContentMarginBottom = 1f;

		AddThemeStyleboxOverride("panel", _style);
		SetupGlow(borderColor, isMirror, isDispellableHarmful);

		// Dispellable harmful effects bounce to demand the player's attention.
		if (isDispellableHarmful)
			StartBounce();

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

			// Apply the same ghostly blue shader used on MirrorImageClone so the
			// icon is visually consistent with the clone standing beside the player.
			if (isMirror)
			{
				var shader = new Shader();
				shader.Code = """
				              shader_type canvas_item;
				              void fragment() {
				              	vec4 col = texture(TEXTURE, UV);
				              	col.rgb = mix(col.rgb, vec3(0.35, 0.72, 1.0), 0.45 * col.a);
				              	col.a *= 0.80;
				              	COLOR = col;
				              }
				              """;
				iconRect.Material = new ShaderMaterial { Shader = shader };
			}

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
	/// The <c>_Mirror</c> suffix is stripped before formatting; callers that
	/// want "(Mirror)" in the display string append it themselves.
	/// </summary>
	static string FormatDisplayName(string id)
	{
		if (id.EndsWith(MirrorSuffix))
			id = id[..^MirrorSuffix.Length];
		return Regex.Replace(id, @"(?<=[a-z])(?=[A-Z])", " ");
	}

	/// <summary>
	/// Creates the glow halo behind the badge.
	///
	/// Dispellable harmful effects use a larger halo (±8 px instead of ±3 px)
	/// with a soft-edge shader so the colour radiates outward rather than appearing
	/// as a hard-edged rectangle.  Mirror effects keep the smaller, subtler halo.
	/// </summary>
	void SetupGlow(Color color, bool isMirror, bool isDispellableHarmful)
	{
		bool shouldPulse = isMirror || isDispellableHarmful;

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

		// Dispellable effects get a bigger halo so it's clearly visible.
		float expand = isDispellableHarmful ? 8f : 3f;
		_glowRect.OffsetLeft   = -expand;
		_glowRect.OffsetTop    = -expand;
		_glowRect.OffsetRight  =  expand;
		_glowRect.OffsetBottom =  expand;

		// Dispellable effects start at a higher base opacity.
		float baseAlpha = isDispellableHarmful ? 0.70f : 0.25f;
		_glowRect.Color = new Color(color.R, color.G, color.B, baseAlpha);

		// Soft-edge shader: alpha is highest at the outermost rim of the glow
		// rect (the halo region beyond the badge border) and fades to zero as it
		// approaches the badge itself — creating a bloom-style outward glow.
		if (isDispellableHarmful && _glowRect.Material == null)
		{
			var glowShader = new Shader();
			glowShader.Code = """
				shader_type canvas_item;
				void fragment() {
					float dx = min(UV.x, 1.0 - UV.x);
					float dy = min(UV.y, 1.0 - UV.y);
					float d = min(dx, dy);
					// Full alpha at the outer edge (d = 0), fades to transparent
					// by the time it reaches the badge border (~22 % in from edge).
					float glow = 1.0 - smoothstep(0.0, 0.22, d);
					COLOR.a *= glow;
				}
				""";
			_glowRect.Material = new ShaderMaterial { Shader = glowShader };
		}

		if (shouldPulse)
			StartPulse(color, isDispellableHarmful);
		else
			StopPulse();
	}

	/// <summary>
	/// Looping tween that pulses the border colour between the base colour and a
	/// lightened variant, with the glow alpha breathing in sync.
	///
	/// Dispellable effects use a faster cycle and a wider alpha swing so they
	/// feel more urgent than the gentle mirror-image pulse.
	/// </summary>
	void StartPulse(Color baseColor, bool isDispellable)
	{
		StopPulse();

		var bright   = baseColor.Lightened(isDispellable ? 0.65f : 0.50f);
		float speed  = isDispellable ? 0.35f : 0.60f;
		float glowHi = isDispellable ? 0.90f : 0.50f;
		float glowLo = isDispellable ? 0.25f : 0.20f;

		_pulseTween = CreateTween().SetLoops();

		// Phase A — brighten: border lightens, glow intensifies.
		_pulseTween.TweenMethod(
				Callable.From<float>(t => { _style.BorderColor = baseColor.Lerp(bright, t); }),
				0f, 1f, speed
			).SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		_pulseTween.Parallel()
			.TweenProperty(_glowRect, "modulate:a", glowHi, speed)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);

		// Phase B — dim: border returns to base, glow fades.
		_pulseTween.TweenMethod(
				Callable.From<float>(t => { _style.BorderColor = baseColor.Lerp(bright, 1f - t); }),
				0f, 1f, speed
			).SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		_pulseTween.Parallel()
			.TweenProperty(_glowRect, "modulate:a", glowLo, speed)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
	}

	void StopPulse()
	{
		if (_pulseTween != null)
		{
			_pulseTween.Kill();
			_pulseTween = null;
		}
	}

	/// <summary>
	/// Looping scale-bounce animation for dispellable harmful effects.
	/// The badge pops up to 110 % of its size and springs back, repeating
	/// every ~1.1 s to catch the player's eye without being distracting.
	/// Scale is centred on <see cref="Control.PivotOffset"/> which is set
	/// to the badge's midpoint in the constructor.
	/// </summary>
	void StartBounce()
	{
		_bounceTween?.Kill();
		_bounceTween = CreateTween().SetLoops();

		// Pop outward quickly.
		_bounceTween
			.TweenProperty(this, "scale", new Vector2(1.10f, 1.10f), 0.28f)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.In);

		// Spring back with a slight overshoot for a bouncy feel.
		_bounceTween
			.TweenProperty(this, "scale", Vector2.One, 0.45f)
			.SetTrans(Tween.TransitionType.Bounce)
			.SetEase(Tween.EaseType.Out);

		// Brief rest at normal size before the next pop.
		_bounceTween.TweenInterval(0.35f);
	}

	void StopBounce()
	{
		if (_bounceTween != null)
		{
			_bounceTween.Kill();
			_bounceTween = null;
			Scale = Vector2.One;
		}
	}
}
