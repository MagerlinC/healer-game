using System.Collections.Generic;
using Godot;

namespace healerfantasy.LastLight.Hud;

/// <summary>
/// Grades every UI control into the Last Light look as it is created, so panels,
/// screens, tooltips and badges match the HUD without restyling each by hand.
///
/// What it does, per control (once, just before the frame it first appears in):
///   • StyleBoxFlat overrides: backgrounds → soot, borders → iron/bronze, sharp corners.
///   • Colour overrides (text, ColorRect): colours keep their hue — green still means
///     helpful, red harmful — but cool and loud hues are muted toward candlelight.
///   • Labels/buttons at title size (≥ 18 px) without a font override get the serif.
///   • Square icon textures (spell/talent/item icons) get the candlelight icon material.
///
/// The grading only caps saturation and brightness, so colours already in the Last
/// Light palette (the hand-styled HUD) pass through unchanged. Anything drawn in
/// _Draw or by shaders is untouched.
/// </summary>
public static class LastLightSkin
{
	static readonly string[] StyleBoxNames =
	{
		"panel", "normal", "hover", "pressed", "focus", "disabled", "hover_pressed",
		"background", "fill", "separator", "grabber_area", "grabber_area_highlight", "slider",
		"tab_selected", "tab_unselected", "tab_hovered", "read_only", "panel_disabled"
	};

	static readonly string[] ColorNames =
	{
		"font_color", "font_hover_color", "font_pressed_color", "font_focus_color",
		"font_hover_pressed_color", "font_disabled_color", "font_selected_color",
		"default_color", "font_uneditable_color"
	};

	static readonly string[] FontSizeNames = { "font_size", "normal_font_size" };

	static readonly List<Control> Pending = new();
	static bool _installed;

	/// <summary>Starts grading every control added to <paramref name="tree"/> from now on.</summary>
	public static void Install(SceneTree tree)
	{
		if (_installed) return;
		_installed = true;
		tree.NodeAdded += node =>
		{
			if (node is Control c) Pending.Add(c);
		};
		// Process just before drawing, so controls built in _Ready are graded
		// before they are ever seen.
		RenderingServer.FramePreDraw += Flush;
	}

	static void Flush()
	{
		if (Pending.Count == 0) return;
		var batch = Pending.ToArray();
		Pending.Clear();
		foreach (var c in batch)
			if (GodotObject.IsInstanceValid(c) && !c.HasMeta("ll_skinned"))
				Grade(c);
	}

	static void Grade(Control c)
	{
		c.SetMeta("ll_skinned", true);

		foreach (var name in StyleBoxNames)
			if (c.HasThemeStyleboxOverride(name) && c.GetThemeStylebox(name) is StyleBoxFlat sb)
				GradeStyleBox(sb);

		foreach (var name in ColorNames)
			if (c.HasThemeColorOverride(name))
				c.AddThemeColorOverride(name, Accent(c.GetThemeColor(name), 0.6f, 0.38f));

		if (c is ColorRect rect && rect.Material == null)
			rect.Color = IsDark(rect.Color) ? Background(rect.Color) : Accent(rect.Color, 0.85f, 0.45f);

		if (c is Label or Button && !c.HasThemeFontOverride("font"))
			foreach (var name in FontSizeNames)
				if (c.HasThemeFontSizeOverride(name) && c.GetThemeFontSize(name) >= 18)
				{
					c.AddThemeFontOverride("font", LastLightHud.Serif);
					break;
				}

		if (c is TextureRect tr && tr.Material == null && tr.Texture is { } tex)
		{
			var w = tex.GetWidth();
			var h = tex.GetHeight();
			if (w <= 512 && h <= 512 && Mathf.Abs(w - h) <= w / 8)
				tr.Material = LastLightHud.IconMaterial;
		}
	}

	static void GradeStyleBox(StyleBoxFlat sb)
	{
		// Style boxes are often shared between controls; grade each only once.
		if (sb.HasMeta("ll_skinned")) return;
		sb.SetMeta("ll_skinned", true);

		sb.BgColor = IsDark(sb.BgColor) ? Background(sb.BgColor) : Accent(sb.BgColor, 0.85f, 0.45f);
		sb.BorderColor = Border(sb.BorderColor);
		sb.SetCornerRadiusAll(0);
	}

	// ── colour grading ───────────────────────────────────────────────────────

	static bool IsDark(Color c) => c.V < 0.3f;

	/// <summary>Dark fills become warm soot: low saturation, never brighter than 0.14.</summary>
	static Color Background(Color c)
	{
		var h = Mathf.Lerp(c.H, 0.07f, 0.6f);
		var s = Mathf.Min(c.S, 0.3f);
		var v = Mathf.Min(c.V, 0.14f);
		return Color.FromHsv(h, s, v, c.A);
	}

	/// <summary>Borders become iron/bronze; reds and ambers keep more of their heat.</summary>
	static Color Border(Color c)
	{
		var warm = IsWarm(c.H);
		var s = Mathf.Min(c.S, warm ? 0.85f : 0.4f);
		var v = Mathf.Min(c.V, warm && c.S > 0.6f ? 0.75f : 0.5f);
		return Color.FromHsv(c.H, s, v, c.A);
	}

	/// <summary>Accents keep hue and brightness; cool hues are muted harder than warm ones.</summary>
	static Color Accent(Color c, float warmCap, float coolCap)
	{
		var s = Mathf.Min(c.S, IsWarm(c.H) ? warmCap : coolCap);
		return Color.FromHsv(c.H, s, c.V, c.A);
	}

	/// <summary>Reds, oranges and golds (and red-magentas).</summary>
	static bool IsWarm(float hue) => hue < 0.16f || hue > 0.94f;
}
