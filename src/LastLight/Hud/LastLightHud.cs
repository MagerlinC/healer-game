using Godot;
using healerfantasy;

namespace healerfantasy.LastLight.Hud;

/// <summary>
/// Shared palette and small builders for the Last Light HUD.
/// Soot-black panels, thin iron borders, sharp corners, Cinzel for names,
/// and warm light reserved for things that matter (health, mana, the active spell).
/// </summary>
public static class LastLightHud
{
	// ── palette ──────────────────────────────────────────────────────────────
	public static readonly Color Soot = new(0.045f, 0.038f, 0.034f, 0.94f);
	public static readonly Color SootSolid = new(0.045f, 0.038f, 0.034f, 1f);
	public static readonly Color Pit = new(0.022f, 0.018f, 0.016f, 1f);
	public static readonly Color Iron = new(0.23f, 0.2f, 0.17f, 1f);
	public static readonly Color IronDim = new(0.15f, 0.13f, 0.11f, 1f);
	public static readonly Color IronLit = new(0.42f, 0.36f, 0.28f, 1f);
	public static readonly Color Light = new(1f, 0.85f, 0.6f, 1f);
	public static readonly Color LightPale = new(0.95f, 0.9f, 0.8f, 1f);
	public static readonly Color Ember = new(0.88f, 0.39f, 0.16f, 1f);
	public static readonly Color Blood = new(0.5f, 0.1f, 0.07f, 1f);
	public static readonly Color BloodBright = new(0.72f, 0.16f, 0.1f, 1f);
	public static readonly Color Text = new(0.85f, 0.81f, 0.74f, 1f);
	public static readonly Color TextDim = new(0.55f, 0.51f, 0.45f, 1f);
	public static readonly Color ShieldGlass = new(0.82f, 0.88f, 0.95f, 0.38f);
	public static readonly Color VoidAbsorb = new(0.2f, 0.06f, 0.28f, 0.7f);

	static Font _serif;

	/// <summary>Cinzel, slightly heavier than its default weight.</summary>
	public static Font Serif
	{
		get
		{
			if (_serif != null) return _serif;
			var file = GD.Load<FontFile>(AssetConstants.CinzelFontPath);
			var variation = new FontVariation { BaseFont = file };
			variation.VariationOpentype = new Godot.Collections.Dictionary { { "wght", 650 } };
			_serif = variation;
			return _serif;
		}
	}

	static ShaderMaterial _iconMaterial;

	/// <summary>Shared material that tones spell icons down to candlelight.</summary>
	public static ShaderMaterial IconMaterial => _iconMaterial ??= new ShaderMaterial
	{
		Shader = GD.Load<Shader>("res://src/LastLight/Hud/hud_icon.gdshader")
	};

	/// <summary>A sharp-cornered soot panel with a thin iron border.</summary>
	public static StyleBoxFlat Panel(Color? bg = null, Color? border = null, int borderWidth = 1)
	{
		var s = new StyleBoxFlat
		{
			BgColor = bg ?? Soot,
			BorderColor = border ?? Iron,
			ShadowColor = new Color(0, 0, 0, 0.45f),
			ShadowSize = 4
		};
		s.SetBorderWidthAll(borderWidth);
		s.SetCornerRadiusAll(0);
		return s;
	}

	/// <summary>Applies the serif font, colour and a soft black shadow to a label.</summary>
	public static void Style(Label label, int size, Color? color = null)
	{
		label.AddThemeFontOverride("font", Serif);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color ?? Text);
		label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
		label.AddThemeConstantOverride("outline_size", 3);
	}
}

/// <summary>
/// Four L-shaped corner marks, used to show the locked default heal target.
/// </summary>
public partial class CornerBrackets : Control
{
	public Color BracketColor = LastLightHud.Light;
	public float Length = 12f;
	public float Width = 2f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Resized += QueueRedraw;
	}

	public override void _Draw()
	{
		var s = Size;
		var l = Mathf.Min(Length, Mathf.Min(s.X, s.Y) * 0.4f);
		var w = Width;
		var c = BracketColor;
		// top-left
		DrawRect(new Rect2(0, 0, l, w), c);
		DrawRect(new Rect2(0, 0, w, l), c);
		// top-right
		DrawRect(new Rect2(s.X - l, 0, l, w), c);
		DrawRect(new Rect2(s.X - w, 0, w, l), c);
		// bottom-left
		DrawRect(new Rect2(0, s.Y - w, l, w), c);
		DrawRect(new Rect2(0, s.Y - l, w, l), c);
		// bottom-right
		DrawRect(new Rect2(s.X - l, s.Y - w, l, w), c);
		DrawRect(new Rect2(s.X - w, s.Y - l, w, l), c);
	}
}
