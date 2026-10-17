using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Godot;
using healerfantasy.SpellResources;

namespace healerfantasy.LastLight.Hud;

/// <summary>
/// Spell and talent icons for the Last Light style, drawn as small SVGs and
/// rasterised once at startup.
///
/// Every icon shares one frame — a soot square with a soft glow in its school's
/// colour — and a single bold glyph, so icons are told apart by silhouette first
/// and colour second. Talents add a thin inset border to read differently from spells.
///
/// Glyphs use a 64×64 canvas and these colour tokens:
///   {C} school colour   {H} hot highlight   {K} soot   {U} umbral black
///
/// Spells are keyed by class name (stable across renames); talents by name.
/// </summary>
public static class LastLightIcons
{
	const float RasterScale = 2f; // 64 → 128 px

	static readonly Dictionary<string, Texture2D> Cache = new();
	static Dictionary<string, string> SpellGlyphs, TalentGlyphs;
	static Dictionary<string, (SpellSchool School, string Glyph)> ItemGlyphs;
	static string GloomGlyph;

	/// <summary>Builds the glyph strings once, with numbers formatted culture-independently.</summary>
	static void EnsureGlyphs()
	{
		if (SpellGlyphs != null) return;
		var previous = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		try
		{
			SpellGlyphs = BuildSpellGlyphs();
			TalentGlyphs = BuildTalentGlyphs();
			ItemGlyphs = BuildItemGlyphs();
			GloomGlyph = BuildGloomGlyph();
		}
		finally
		{
			CultureInfo.CurrentCulture = previous;
		}
	}
	static readonly HashSet<Texture2D> Ours = new();

	/// <summary>The icon for a spell, or null if it has none (falls back to the original art).</summary>
	public static Texture2D ForSpell(SpellResource spell)
	{
		if (spell == null) return null;
		EnsureGlyphs();
		var key = spell.GetType().Name;
		return SpellGlyphs.TryGetValue(key, out var glyph) ? Render("spell:" + key, spell.School, glyph, false) : null;
	}

	/// <summary>The icon for a talent, or null if it has none.</summary>
	public static Texture2D ForTalent(string name, SpellSchool school)
	{
		if (name == null) return null;
		EnsureGlyphs();
		return TalentGlyphs.TryGetValue(name, out var glyph) ? Render("talent:" + name, school, glyph, true) : null;
	}

	/// <summary>The icon for an equippable item, or null if it has none.</summary>
	public static Texture2D ForItem(Items.EquippableItem item)
	{
		return item == null ? null : ForItem(item.GetType().Name);
	}

	/// <summary>The icon for an item by class name — for effects that borrow their item's icon.</summary>
	public static Texture2D ForItem(string className)
	{
		EnsureGlyphs();
		return ItemGlyphs.TryGetValue(className, out var item)
			? Render("item:" + className, item.School, ItemFrame + item.Glyph, false)
			: null;
	}

	/// <summary>The Gloom debuff badge.</summary>
	public static Texture2D Gloom()
	{
		EnsureGlyphs();
		return Render("effect:gloom", SpellSchool.Umbra, GloomGlyph, false);
	}

	const string RuneStonePath = "res://assets/interactibles/rune.png";

	/// <summary>
	/// A rune: the blank rune stone (<see cref="RuneStonePath"/>) with the sky at that hour
	/// of the night carved into it. The runes go darker as they go: a sinking sun, a crescent,
	/// a sliver, no light at all, and finally the Abyss looking back.
	/// </summary>
	public static Texture2D ForRune(Runes.RuneIndex rune)
	{
		var key = "rune:" + rune;
		if (Cache.TryGetValue(key, out var cached)) return cached;

		var previous = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		try
		{
			// Marks are drawn on the usual 64×64 grid; only light and dark shapes, no
			// background-coloured covers, since the stone shows through around them.
			var mark = rune switch
			{
				// Twilight: the sun half-sunk below a horizon line
				Runes.RuneIndex.Twilight => Path("M19,37 A13,13 0 0 1 45,37 Z", "{H}", "{C}", 1.5f) + Line(13, 37, 51, 37, 3),
				// Nightfall: a crescent moon
				Runes.RuneIndex.Nightfall => Crescent(32, 32, 14, 38, 28, 12),
				// Midnight: only a thin sliver of moon left
				Runes.RuneIndex.Midnight => Crescent(32, 32, 14, 35.5f, 30.5f, 13.5f),
				// Abyss: blackness with two pale eyes open in it
				Runes.RuneIndex.Abyss => Circle(32, 32, 16, "#050307") + Circle(25.5f, 31, 3.6f, "{H}") +
				                         Circle(38.5f, 31, 3.6f, "{H}"),
				// Starless Night: an empty, lightless disc
				_ => Circle(32, 32, 14, "#050307") + Ring(32, 32, 14, 2f, "{C}", 0.8f)
			};
			// A dark, slightly offset copy underneath reads as the cut into the stone.
			var carve = Regex.Replace(mark, "(fill|stroke)='(?!none)[^']*'", "$1='#1f1a17'");

			var c = Runes.RuneInfo.Color(rune);
			var svg = ("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 91 120' width='91' height='120'>" +
			           "<g transform='translate(7.1,18.6) scale(1.2)'>" +
			           "<g transform='translate(1,1.4)' opacity='0.8'>" + carve + "</g>" + mark + "</g></svg>")
				.Replace("{C}", "#" + c.ToHtml(false))
				.Replace("{H}", "#" + c.Lerp(Colors.White, 0.55f).ToHtml(false));

			var glyph = new Image();
			var stone = GD.Load<Texture2D>(RuneStonePath)?.GetImage();
			if (glyph.LoadSvgFromString(svg, RasterScale) != Error.Ok || stone == null)
			{
				GD.PushWarning($"[LastLightIcons] could not render {key}");
				return Cache[key] = null;
			}

			stone = (Image)stone.Duplicate();
			if (stone.IsCompressed()) stone.Decompress();
			stone.Convert(Image.Format.Rgba8);
			stone.Resize(glyph.GetWidth(), glyph.GetHeight(), Image.Interpolation.Cubic);
			stone.BlendRect(glyph, new Rect2I(0, 0, glyph.GetWidth(), glyph.GetHeight()), Vector2I.Zero);

			var tex = ImageTexture.CreateFromImage(stone);
			Ours.Add(tex);
			return Cache[key] = tex;
		}
		finally
		{
			CultureInfo.CurrentCulture = previous;
		}
	}

	/// <summary>
	/// A crescent: the disc at (cx, cy, r) with the disc at (ix, iy, ir) taken out of it,
	/// as one polygon (sampled, so no covering shape is needed).
	/// </summary>
	static string Crescent(float cx, float cy, float r, float ix, float iy, float ir, string fill = "{H}")
	{
		const int samples = 120;
		var outer = new Vector2(cx, cy);
		var inner = new Vector2(ix, iy);
		var away = Mathf.Atan2(cy - iy, cx - ix); // the lit side faces away from the shadow disc
		var pts = new List<Vector2>();
		for (var i = 0; i <= samples; i++)
		{
			var a = away - Mathf.Pi + Mathf.Tau * i / samples;
			var p = outer + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
			if (p.DistanceTo(inner) >= ir) pts.Add(p);
		}

		var back = new List<Vector2>();
		for (var i = 0; i <= samples; i++)
		{
			var a = away - Mathf.Pi + Mathf.Tau * i / samples;
			var p = inner + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ir;
			if (p.DistanceTo(outer) <= r) back.Add(p);
		}

		back.Reverse();
		pts.AddRange(back);
		return Poly(string.Join(" ", pts.Select(p => $"{p.X:0.##},{p.Y:0.##}")), fill);
	}

	/// <summary>The Infinite Abyss's ability icons (cast bars, debuffs, the combat log).</summary>
	public static Texture2D ForAbyss(string ability)
	{
		var previous = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		try
		{
			var glyph = ability switch
			{
				// a black weight pressing down on the ground
				"crushing-dark" => Poly("14,8 50,8 41,34 32,54 23,34", "{U}", "{C}") + Line(12, 58, 52, 58, 3, "{H}"),
				// a tendril spearing down from above into a marked circle
				"falling-tendril" => Ring(32, 52, 9, 2.5f, "{H}") + Path("M38,2 C26,16 42,28 32,48", "none", "{C}", 6)
				                                                  + Path("M38,2 C26,16 42,28 32,48", "none", "{U}", 2.5f),
				// two huge eyes opening in the black
				"stare" => Circle(32, 32, 26, "#000000") + Poly("8,32 20,23 30,31 20,38", "{H}") +
				           Poly("56,32 44,23 34,31 44,38", "{H}")
				           + Line(20, 25, 20, 37, 2.5f, "#000000") + Line(44, 25, 44, 37, 2.5f, "#000000"),
				// the light swallowed: a thin ring of it left around a black disc
				"eclipse" => Circle(32, 32, 17, "{H}") + Circle(32, 32, 14.5f, "#000000"),
				// a single tendril lashing sideways
				_ => Path("M14,56 C18,36 44,40 42,22 C41,14 48,10 54,10", "none", "{C}", 5) + Circle(54, 10, 3, "{H}")
			};
			return Render("abyss:" + ability, new Color(0.55f, 0.36f, 0.62f), glyph, false);
		}
		finally
		{
			CultureInfo.CurrentCulture = previous;
		}
	}

	/// <summary>True for icons made here — they are already in palette and must not be toned down.</summary>
	public static bool IsOurs(Texture2D texture)
	{
		return texture != null && Ours.Contains(texture);
	}

	// ── rendering ────────────────────────────────────────────────────────────

	static Texture2D Render(string key, SpellSchool school, string glyph, bool talent)
	{
		return Render(key, school == SpellSchool.Generic ? new Color(0.86f, 0.8f, 0.66f) : SchoolInfo.Color(school), glyph, talent);
	}

	static Texture2D Render(string key, Color c, string glyph, bool talent)
	{
		if (Cache.TryGetValue(key, out var cached)) return cached;

		var hot = c.Lerp(Colors.White, 0.65f);

		var svg = new StringBuilder()
			.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64' width='64' height='64'>")
			.Append("<defs><radialGradient id='g' cx='50%' cy='55%' r='62%'>")
			.Append("<stop offset='0' stop-color='{C}' stop-opacity='0.38'/>")
			.Append("<stop offset='1' stop-color='{K}' stop-opacity='0'/></radialGradient></defs>")
			.Append("<rect width='64' height='64' fill='{K}'/>")
			.Append("<rect width='64' height='64' fill='url(#g)'/>")
			.Append(talent
				? "<rect x='3.5' y='3.5' width='57' height='57' fill='none' stroke='{C}' stroke-opacity='0.55' stroke-width='1.5'/>"
				: "")
			.Append(glyph)
			.Append("</svg>")
			.ToString()
			.Replace("{C}", "#" + c.ToHtml(false))
			.Replace("{H}", "#" + hot.ToHtml(false))
			.Replace("{D}", "#" + c.Darkened(0.25f).ToHtml(false))
			.Replace("{K}", "#0e0c0b")
			.Replace("{U}", "#120c1a");

		var image = new Image();
		var err = image.LoadSvgFromString(svg, RasterScale);
		if (err != Error.Ok)
		{
			GD.PushWarning($"[LastLightIcons] could not render {key}: {err}");
			return Cache[key] = null;
		}

		var tex = ImageTexture.CreateFromImage(image);
		Ours.Add(tex);
		return Cache[key] = tex;
	}

	// ── shape helpers (invariant culture: these become SVG numbers) ──────────

	// Glyphs are built inside an invariant-culture scope (see EnsureGlyphs), so plain
	// interpolation already writes "1.5", never "1,5".
	static string F(string s)
	{
		return s;
	}

	/// <summary>A flame centred at (x, y); about 40 units tall at scale 1.</summary>
	static string Flame(float x, float y, float s, string fill = "{C}", string core = "{H}", string stroke = null)
	{
		return F(
			$"<g transform='translate({x} {y}) scale({s})'>" +
			$"<path d='M0,-22 C9,-12 14,-3 12,6 C10,15 5,19 0,19 C-5,19 -10,15 -12,6 C-14,-3 -9,-12 0,-22 Z' fill='{fill}'" +
			(stroke != null ? $" stroke='{stroke}' stroke-width='2.5'" : "") + "/>" +
			$"<path d='M0,-5 C4,1 6,5 5,10 C4,14 2,15 0,15 C-2,15 -4,14 -5,10 C-6,5 -4,1 0,-5 Z' fill='{core}'/></g>");
	}

	static string Drop(float x, float y, float s, string fill = "{C}")
	{
		return F(
			$"<path transform='translate({x} {y}) scale({s})' d='M0,-14 C5,-6 10,0 10,6 A10,10 0 0 1 -10,6 C-10,0 -5,-6 0,-14 Z' fill='{fill}'/>");
	}

	static string Heart(float x, float y, float s, string fill = "{C}")
	{
		return F(
			$"<path transform='translate({x} {y}) scale({s})' d='M0,10 C-15,-1 -13,-14 -5,-14 C-2,-14 0,-11 0,-9 C0,-11 2,-14 5,-14 C13,-14 15,-1 0,10 Z' fill='{fill}'/>");
	}

	static string Coal(float x, float y, float s, string fill = "{C}")
	{
		return F(
			$"<polygon transform='translate({x} {y}) scale({s})' points='-11,4 -8,-6 0,-10 9,-7 12,3 5,9 -6,9' fill='{fill}'/>");
	}

	static string Shield(float x, float y, float s, string fill, string stroke = null)
	{
		return F(
			$"<path transform='translate({x} {y}) scale({s})' d='M0,-16 L13,-10 L11,6 L0,16 L-11,6 L-13,-10 Z' fill='{fill}'" +
			(stroke != null ? $" stroke='{stroke}' stroke-width='2.5' stroke-linejoin='round'" : "") + "/>");
	}

	static string Circle(float x, float y, float r, string fill = "{C}")
	{
		return F($"<circle cx='{x}' cy='{y}' r='{r}' fill='{fill}'/>");
	}

	static string Ring(float x, float y, float r, float w, string stroke = "{C}", float opacity = 1f)
	{
		return F(
			$"<circle cx='{x}' cy='{y}' r='{r}' fill='none' stroke='{stroke}' stroke-width='{w}' stroke-opacity='{opacity}'/>");
	}

	static string Line(float x1, float y1, float x2, float y2, float w, string stroke = "{C}")
	{
		return F(
			$"<line x1='{x1}' y1='{y1}' x2='{x2}' y2='{y2}' stroke='{stroke}' stroke-width='{w}' stroke-linecap='round'/>");
	}

	static string Poly(string points, string fill = "{C}", string stroke = null)
	{
		return $"<polygon points='{points}' fill='{fill}'" +
		       (stroke != null ? $" stroke='{stroke}' stroke-width='2.5' stroke-linejoin='round'" : "") + "/>";
	}

	static string Path(string d, string fill = "none", string stroke = "{C}", float w = 3f)
	{
		return F(
			$"<path d='{d}' fill='{fill}' stroke='{stroke}' stroke-width='{w}' stroke-linecap='round' stroke-linejoin='round'/>");
	}

	/// <summary>An n-pointed star.</summary>
	static string Star(float x, float y, int n, float r1, float r2, string fill = "{C}", float rotDeg = -90f)
	{
		var sb = new StringBuilder();
		for (var i = 0; i < n * 2; i++)
		{
			var a = Mathf.DegToRad(rotDeg) + Mathf.Pi * i / n;
			var r = i % 2 == 0 ? r1 : r2;
			sb.Append(F($"{x + Mathf.Cos(a) * r:0.##},{y + Mathf.Sin(a) * r:0.##} "));
		}

		return Poly(sb.ToString().Trim(), fill);
	}

	/// <summary>n rays between radius r1 and r2.</summary>
	static string Rays(float x, float y, float r1, float r2, int n, float w, string stroke = "{C}", float rotDeg = -90f)
	{
		var sb = new StringBuilder();
		for (var i = 0; i < n; i++)
		{
			var a = Mathf.DegToRad(rotDeg) + Mathf.Tau * i / n;
			sb.Append(Line(x + Mathf.Cos(a) * r1, y + Mathf.Sin(a) * r1, x + Mathf.Cos(a) * r2, y + Mathf.Sin(a) * r2, w, stroke));
		}

		return sb.ToString();
	}

	/// <summary>A triangular arrowhead at (x, y) pointing along <paramref name="angleDeg"/>.</summary>
	static string Head(float x, float y, float angleDeg, float size, string fill = "{C}")
	{
		var a = Mathf.DegToRad(angleDeg);
		var tip = new Vector2(x, y) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size;
		var l = new Vector2(x, y) + new Vector2(Mathf.Cos(a + 2.3f), Mathf.Sin(a + 2.3f)) * size * 0.8f;
		var r = new Vector2(x, y) + new Vector2(Mathf.Cos(a - 2.3f), Mathf.Sin(a - 2.3f)) * size * 0.8f;
		return Poly(F($"{tip.X:0.##},{tip.Y:0.##} {l.X:0.##},{l.Y:0.##} {r.X:0.##},{r.Y:0.##}"), fill);
	}

	/// <summary>A straight arrow from (x1, y1) to (x2, y2).</summary>
	static string Arrow(float x1, float y1, float x2, float y2, float w, string color = "{C}")
	{
		var angle = Mathf.RadToDeg(Mathf.Atan2(y2 - y1, x2 - x1));
		return Line(x1, y1, x2, y2, w, color) + Head(x2, y2, angle, w * 2.2f, color);
	}

	/// <summary>A circular arrow (degrees, clockwise), ending in a head.</summary>
	static string ArcArrow(float x, float y, float r, float fromDeg, float toDeg, float w, string color = "{C}")
	{
		var sb = new StringBuilder();
		const int steps = 28;
		for (var i = 0; i <= steps; i++)
		{
			var a = Mathf.DegToRad(Mathf.Lerp(fromDeg, toDeg, i / (float)steps));
			sb.Append(F($"{x + Mathf.Cos(a) * r:0.##},{y + Mathf.Sin(a) * r:0.##} "));
		}

		var end = Mathf.DegToRad(toDeg);
		var tangent = toDeg > fromDeg ? toDeg + 90f : toDeg - 90f;
		return
			F($"<polyline points='{sb.ToString().Trim()}' fill='none' stroke='{color}' stroke-width='{w}' stroke-linecap='round'/>") +
			Head(x + Mathf.Cos(end) * r, y + Mathf.Sin(end) * r, tangent, w * 2f, color);
	}

	static string Spiral(float x, float y, float turns, float rMax, float w, string stroke = "{C}")
	{
		var sb = new StringBuilder();
		const int steps = 60;
		for (var i = 0; i <= steps; i++)
		{
			var t = i / (float)steps;
			var a = t * turns * Mathf.Tau;
			var r = 2f + t * rMax;
			sb.Append(F($"{x + Mathf.Cos(a) * r:0.##},{y + Mathf.Sin(a) * r:0.##} "));
		}

		return F(
			$"<polyline points='{sb.ToString().Trim()}' fill='none' stroke='{stroke}' stroke-width='{w}' stroke-linecap='round'/>");
	}

	static string Candle(float x, float y, float s)
	{
		return F(
			       $"<g transform='translate({x} {y}) scale({s})'>" +
			       $"<rect x='-6' y='-4' width='12' height='22' rx='1.5' fill='{{H}}'/>" +
			       $"<path d='M-6,-4 C-6,-1 -3,0 -3,3 C-3,5 -1,5 -1,3 L-1,-4 Z' fill='{{C}}'/>" +
			       $"<line x1='0' y1='-4' x2='0' y2='-8' stroke='{{K}}' stroke-width='1.5'/></g>") +
		       Flame(x, y - 15f * s, 0.42f * s);
	}

	static string Moth(float x, float y, float s, string fill = "{C}")
	{
		return F(
			$"<g transform='translate({x} {y}) scale({s})' fill='{fill}'>" +
			$"<path d='M0,-6 C-6,-14 -18,-14 -16,-4 C-15,2 -6,2 0,0 Z'/>" +
			$"<path d='M0,-6 C6,-14 18,-14 16,-4 C15,2 6,2 0,0 Z'/>" +
			$"<path d='M0,0 C-5,3 -12,9 -9,13 C-6,15 -2,8 0,4 Z'/>" +
			$"<path d='M0,0 C5,3 12,9 9,13 C6,15 2,8 0,4 Z'/>" +
			$"<ellipse cx='0' cy='0' rx='2' ry='9'/></g>");
	}

	static string Hourglass(float x, float y, float s)
	{
		return F(
			$"<g transform='translate({x} {y}) scale({s})'>" +
			$"<path d='M-11,-16 L11,-16 L11,-13 C11,-5 3,-2 3,0 C3,2 11,5 11,13 L11,16 L-11,16 L-11,13 C-11,5 -3,2 -3,0 C-3,-2 -11,-5 -11,-13 Z' fill='none' stroke='{{C}}' stroke-width='2.5' stroke-linejoin='round'/>" +
			$"<path d='M-7,12 C-4,7 4,7 7,12 L7,13 L-7,13 Z' fill='{{H}}'/></g>");
	}

	static string Eye(float x, float y, float s)
	{
		return F(
			       $"<path transform='translate({x} {y}) scale({s})' d='M-20,0 C-12,-11 12,-11 20,0 C12,11 -12,11 -20,0 Z' fill='none' stroke='{{C}}' stroke-width='3'/>") +
		       Flame(x, y + 1f * s, 0.32f * s);
	}

	static string Blade(float x, float y, float s)
	{
		return F(
			$"<g transform='translate({x} {y}) scale({s})'>" +
			$"<polygon points='0,-22 4,-16 3,8 -3,8 -4,-16' fill='{{H}}'/>" +
			$"<rect x='-10' y='8' width='20' height='3.5' fill='{{C}}'/>" +
			$"<rect x='-2' y='11' width='4' height='9' fill='{{C}}'/></g>");
	}

	// ── the glyphs ───────────────────────────────────────────────────────────

	static Dictionary<string, string> BuildSpellGlyphs()
	{
		return new Dictionary<string, string>
		{
			// FLAME — the light itself
			["FlareSpell"] = Star(32, 32, 8, 25, 9) + Circle(32, 32, 6, "{H}"), // Flare
			["CandleTouchSpell"] = Candle(32, 40, 1.15f), // Candle Touch
			["LanternWardSpell"] = Ring(32, 13, 4, 2.5f) + Poly("22,18 42,18 38,22 26,22") + // Lantern Ward
			                       Path("M24,22 L40,22 L42,50 L22,50 Z", "none", "{C}", 3) + Line(20, 51, 44, 51, 3) +
			                       Flame(32, 37, 0.5f),
			["RekindleSpell"] = Flame(32, 34, 0.85f) + ArcArrow(32, 32, 24, 150, 400, 3), // Rekindle
			["FlashfireSpell"] = Ring(32, 32, 9, 4, "{H}") + Rays(32, 32, 15, 27, 12, 3), // Flashfire
			["WildfireSpell"] = Flame(17, 41, 0.6f) + Flame(47, 41, 0.6f) + Flame(32, 33, 0.85f), // Wildfire
			["BecomeTheSunSpell"] = Rays(32, 32, 15, 29, 8, 3.5f) + Rays(32, 32, 15, 23, 8, 2.5f, "{C}", -67.5f) + // Become the Sun
			                        Circle(32, 32, 11, "{H}") + Ring(32, 32, 11, 2),

			// EMBER — light that lingers
			["SmoulderSpell"] = Coal(32, 44, 1.5f) + Path("M27,46 L32,41 L37,46", "none", "{H}", 2) + // Smoulder
			                    Path("M26,30 C22,24 30,22 26,14", "none", "#8a7c72", 2.5f) +
			                    Path("M37,30 C41,24 33,21 38,12", "none", "#8a7c72", 2.5f),
			["SpreadingEmbersSpell"] = Circle(32, 40, 6, "{H}") + Circle(17, 30, 4) + Circle(47, 28, 4.5f) + // Spreading Embers
			                           Circle(22, 49, 3.5f) + Circle(44, 48, 3.5f) + Circle(32, 19, 3.5f) + Circle(12, 44, 2.5f) +
			                           Circle(53, 41, 2.5f),
			["CinderSpell"] = Poly("36,18 10,47 15,52 44,29") + Circle(41, 23, 9, "{H}") + Circle(41, 23, 4.5f), // Cinder
			["AshVeilSpell"] = Shield(32, 32, 1.45f, "none", "{C}") + Circle(24, 26, 2, "#9a8e84") + // Ash Veil
			                   Circle(36, 22, 1.6f, "#9a8e84") + Circle(30, 36, 2.2f, "#9a8e84") + Circle(40, 33, 1.6f, "#9a8e84") +
			                   Circle(34, 44, 1.8f, "#9a8e84") + Circle(25, 40, 1.4f, "#9a8e84"),
			["MothswarmSpell"] = Moth(30, 32, 1.25f) + Moth(50, 16, 0.45f, "{H}") + Moth(14, 50, 0.4f, "{H}"), // Mothswarm
			["StokeSpell"] = Coal(32, 48, 1.25f) + Path("M21,30 L32,20 L43,30", "none", "{H}", 3.5f) + // Stoke
			                 Path("M21,40 L32,30 L43,40", "none", "{C}", 3.5f),
			["OneWithTheAshSpell"] = Poly("32,12 46,48 18,48") + Circle(32, 26, 5, "{K}") +
			                         Circle(32, 26, 3, "{H}") + // One With the Ash
			                         Circle(14, 30, 2.5f, "{H}") + Circle(50, 24, 2.5f, "{H}") + Circle(12, 16, 1.8f) +
			                         Circle(52, 40, 1.8f),

			// UMBRA — borrowing the dark
			["UmbralBoltSpell"] = Poly("44,8 20,34 31,34 19,56 46,26 35,26", "{U}", "{C}"), // Umbral Bolt
			["SiphonTheDarkSpell"] = Spiral(32, 32, 2.6f, 22, 3.5f) + Circle(32, 32, 3.5f, "{U}"), // Siphon the Dark
			["TouchOfAffliction"] = Path("M20,8 C16,20 28,26 22,40", "none", "{C}", 6) +
			                        Path("M20,8 C16,20 28,26 22,40", "none", "{U}", 3) + // Touch of Affliction
			                        Path("M32,6 C30,22 38,28 32,46", "none", "{C}", 6) +
			                        Path("M32,6 C30,22 38,28 32,46", "none", "{U}", 3) +
			                        Path("M44,8 C48,20 36,26 42,40", "none", "{C}", 6) +
			                        Path("M44,8 C48,20 36,26 42,40", "none", "{U}", 3) +
			                        Circle(32, 54, 2.5f) + Circle(22, 48, 2) + Circle(42, 48, 2),
			["RotSpell"] =
				Path(
					"M10,14 L54,14 L54,22 C50,22 49,30 46,40 C43,30 42,24 38,24 C35,24 34,36 31,50 C28,36 27,24 24,24 C21,24 20,30 18,36 C16,28 15,22 10,22 Z",
					"{U}", "{C}", 2.5f), // Rot
			["SoulShatterSpell"] = Poly("32,8 44,30 34,32", "{U}", "{C}") + Poly("44,34 32,56 30,38", "{U}", "{C}") + // Soul Shatter
			                       Poly("20,30 28,30 28,46", "{U}", "{C}") + Line(32, 33, 36, 35, 2, "{H}"),
			["ConsumingDarkSpell"] = Circle(32, 32, 15, "#000000") + Ring(32, 32, 15, 3) + // Consuming Dark
			                         Head(32, 9, 90, 6) + Head(32, 55, -90, 6) + Head(9, 32, 0, 6) + Head(55, 32, 180, 6),
			["EmbraceTheDarkSpell"] = Flame(32, 35, 1.15f, "#000000", "{C}", "{C}"), // Embrace the Dark

			// WICK — time by the candle
			["QuickenTheWickSpell"] =
				Candle(38, 40, 1.05f) + Line(8, 26, 22, 26, 3) + Line(5, 36, 22, 36, 3) + Line(10, 46, 22, 46, 3), // Quicken the Wick
			["WaxSealSpell"] =
				Star(32, 32, 14, 23, 20) + Ring(32, 32, 14, 2, "{K}", 0.6f) + Flame(32, 33, 0.42f, "{K}", "{C}"), // Wax Seal
			["DripSpell"] = Drop(32, 38, 1.55f) + Drop(32, 12, 0.45f, "{H}"), // Drip
			["RelightSpell"] = ArcArrow(32, 32, 20, -60, 250, 3.5f) + Star(32, 32, 4, 11, 3.5f, "{H}"), // Relight
			["UnburnSpell"] = Candle(32, 40, 0.95f) + ArcArrow(32, 32, 24, 30, -230, 3), // Unburn
			["BurnBrightSpell"] =
				Flame(16, 40, 0.5f) + Flame(32, 30, 0.75f) + Flame(48, 40, 0.5f) + Head(32, 6, -90, 5, "{H}"), // Burn Bright
			["TwinFlameSpell"] = Flame(22, 34, 0.75f) + Flame(42, 34, 0.75f, "none", "{C}", "{C}"), // Twin Flame

			// KINDLING — burning your own life
			["BurningPactSpell"] = Heart(32, 38, 1.5f) + Flame(32, 16, 0.38f), // Burning Pact
			["LifeflameSpell"] = Flame(32, 34, 1.15f, "{C}", "{H}") + Heart(32, 40, 0.55f, "{K}"), // Lifeflame
			["DrawTheHeatSpell"] = Flame(32, 34, 0.6f) + Arrow(6, 8, 18, 20, 3) + Arrow(58, 8, 46, 20, 3) + // Draw the Heat
			                       Arrow(6, 58, 18, 46, 3) + Arrow(58, 58, 46, 46, 3),
			["FeedThePyreSpell"] = Line(14, 54, 50, 40, 6) + Line(14, 40, 50, 54, 6) + Flame(32, 26, 0.75f), // Feed the Pyre
			["NovaOfSacrifice"] = Heart(32, 34, 1f, "{H}") + Ring(32, 32, 19, 3) + Rays(32, 32, 22, 29, 8, 2.5f), // Nova of Sacrifice
			["SharedFlameSpell"] = Flame(32, 24, 0.6f) + Flame(15, 45, 0.45f) + Flame(49, 45, 0.45f) + // Shared Flame
			                       Line(30, 34, 18, 40, 2.5f, "{H}") + Line(34, 34, 46, 40, 2.5f, "{H}"),

			// GENERAL
			["DispelSpell"] = Ring(32, 32, 19, 3.5f) + Line(18, 46, 46, 18, 4) + Star(44, 20, 4, 7, 2, "{H}"),
			["DeflectSpell"] = Shield(26, 34, 1.15f, "none", "{C}") + Arrow(58, 8, 36, 28, 3) + Arrow(36, 28, 58, 50, 3, "{H}")
		};
	}

	static Dictionary<string, string> BuildTalentGlyphs()
	{
		return new Dictionary<string, string>
		{
			// Umbra
			["Umbral Specialist"] = Flame(28, 36, 0.95f, "#000000", "{C}", "{C}") + Line(48, 12, 48, 24, 3) + Line(42, 18, 54, 18, 3),
			["Entropic Surge"] = Poly("30,10 14,32 22,32 14,52 34,26 26,26", "{U}", "{C}") +
			                     Poly("48,10 32,32 40,32 32,52 52,26 44,26", "{U}", "{C}"),
			["Siphoning Dark"] = Spiral(30, 34, 2.2f, 17, 3) + Drop(48, 16, 0.6f, "{H}"),
			["Necrotic Touch"] = Path("M32,8 C28,22 38,28 32,40", "none", "{C}", 6) +
			                     Path("M32,8 C28,22 38,28 32,40", "none", "{U}", 3) +
			                     Circle(22, 50, 3.5f) + Circle(32, 52, 3.5f) + Circle(42, 50, 3.5f),
			["Umbral Resonance"] = Ring(32, 32, 22, 2, "{C}", 0.35f) + Ring(32, 32, 15, 2.5f, "{C}", 0.65f) + Ring(32, 32, 8, 3) +
			                       Circle(32, 32, 5, "{U}"),

			// Flame
			["Shielding Rekindle"] = Shield(32, 32, 1.5f, "none", "{C}") + Flame(32, 34, 0.55f),
			["Sacred Ground"] =
				Line(8, 50, 56, 50, 3) + Flame(32, 32, 0.8f) + Drop(13, 38, 0.45f, "{H}") + Drop(51, 38, 0.45f, "{H}"),
			["Sacred Momentum"] = Flame(26, 32, 0.85f) + Path("M44,14 L52,14 L52,52 L44,52 Z", "none", "{C}", 2.5f) +
			                      Poly("45,34 51,34 51,51 45,51", "{H}"),
			["Radiant Infusion"] = Circle(32, 30, 16, "{C}") + Blade(32, 34, 1f),
			["Illumination"] = Star(30, 32, 4, 22, 5, "{H}") + Star(30, 32, 4, 12, 3) + Drop(50, 46, 0.5f),

			// Ember
			["Banked Embers"] = Coal(22, 46, 1.1f) + Coal(42, 46, 1.1f, "{H}") + Coal(32, 30, 1.1f),
			["Searing Cinders"] = Poly("34,16 6,40 10,46 42,26") + Poly("42,26 16,54 22,58 50,34", "{C}") + Circle(44, 24, 8, "{H}"),
			["Spreading Warmth"] = Coal(32, 40, 1.15f) + Path("M14,28 C18,22 14,16 18,10", "none", "{C}", 2.5f) +
			                       Path("M32,26 C36,20 28,14 32,6", "none", "{H}", 2.5f) +
			                       Path("M50,28 C46,22 50,16 46,10", "none", "{C}", 2.5f),
			["Deep Coals"] = Path("M10,30 C12,54 52,54 54,30", "none", "{C}", 3) + Coal(32, 40, 0.9f, "{H}") + Drop(32, 16, 0.55f),
			["Overburn"] = Coal(32, 46, 1.15f) + Path("M14,12 L50,12 L50,20 L14,20 Z", "none", "{C}", 2) +
			               Poly("15,13 41,13 41,19 15,19", "{H}") + Arrow(32, 36, 32, 26, 3),

			// Wick
			["Acceleration"] = Path("M12,18 L26,32 L12,46", "none", "{C}", 4) + Path("M26,18 L40,32 L26,46", "none", "{H}", 4) +
			                   Flame(50, 33, 0.38f),
			["Temporal Flow"] = Hourglass(32, 32, 1.35f),
			["Kindling Rift"] = Drop(32, 34, 1.6f) + Path("M30,14 L35,26 L28,34 L34,46", "none", "{K}", 3),
			["Future Sight"] = Eye(32, 32, 1.2f),
			["Temporal Momentum"] = Flame(24, 34, 0.75f) + Arrow(38, 32, 58, 32, 3.5f),

			// General
			["Critical Amplifier"] = Star(28, 34, 4, 20, 5) + Line(50, 10, 50, 22, 3, "{H}") + Line(44, 16, 56, 16, 3, "{H}"),
			["Critical Recharge"] = ArcArrow(32, 32, 22, -40, 260, 3) + Star(32, 32, 4, 13, 3.5f, "{H}"),
			["Gathered Kindling"] = Drop(32, 36, 1.2f) + ArcArrow(32, 32, 24, 200, 470, 3, "{H}"),
			["Arcane Hunger"] = Drop(30, 36, 1.6f) + Circle(46, 26, 9, "{K}") + Poly("38,22 42,30 44,22", "{H}") +
			                    Poly("44,30 48,36 50,28", "{H}"),
			["Critical Infusion"] = Star(24, 34, 4, 18, 4.5f) + Flame(46, 34, 0.55f),

			// Pyre
			["Crimson Rebound"] = Heart(30, 36, 1.2f) + ArcArrow(32, 32, 24, 220, 470, 3, "{H}"),
			["Bloodthirst"] = Drop(32, 38, 1.45f) + Poly("22,10 28,10 25,22", "{H}") + Poly("36,10 42,10 39,22", "{H}"),
			["Pyre Ward"] = Shield(32, 32, 1.5f, "none", "{C}") + Heart(32, 34, 0.75f),
			["Desperate Blaze"] = Flame(32, 27, 0.95f) + Path("M14,50 L50,50 L50,58 L14,58 Z", "none", "{C}", 2) +
			                      Poly("15,51 21,51 21,57 15,57", "{H}"),
			["Phoenix Heart"] = Heart(32, 42, 1.25f) + Flame(32, 20, 0.55f, "{H}", "{C}")
		};
	}

	// ── items ────────────────────────────────────────────────────────────────

	/// <summary>Corner brackets: marks an icon as an item rather than a spell or talent.</summary>
	const string ItemFrame =
		"<path d='M4,13 L4,4 L13,4 M51,4 L60,4 L60,13 M60,51 L60,60 L51,60 M13,60 L4,60 L4,51' " +
		"fill='none' stroke='{C}' stroke-opacity='0.6' stroke-width='2'/>";

	/// <summary>Any 64×64 glyph, scaled by <paramref name="s"/> and centred on (x, y).</summary>
	static string Place(float x, float y, float s, string glyph)
	{
		return F($"<g transform='translate({x} {y}) scale({s}) translate(-32 -32)'>") + glyph + "</g>";
	}

	/// <summary>A staff rising from the bottom left; its head goes around (43, 20).</summary>
	static string Staff(string head)
	{
		// the shaft, with three bands of wrapping across it
		var sb = new StringBuilder(Line(12, 59, 36, 30, 6, "{D}"));
		foreach (var t in new[] { 0.3f, 0.45f, 0.6f })
		{
			var p = new Vector2(Mathf.Lerp(12, 36, t), Mathf.Lerp(59, 30, t));
			sb.Append(Line(p.X - 4f, p.Y - 3.3f, p.X + 4f, p.Y + 3.3f, 2.5f));
		}

		return sb + head;
	}

	/// <summary>A ring band with its stone (or signet face) around (32, 22).</summary>
	static string Band(string stone)
	{
		return Ring(32, 43, 14, 6, "{D}") + Path("M21,51 C26,56 38,56 43,51", "none", "{C}", 2) + stone;
	}

	/// <summary>A flat signet face at (32, 23) with a mark carved into it.</summary>
	static string Signet(string mark)
	{
		return Band(F("<ellipse cx='32' cy='23' rx='16' ry='12' fill='{K}' stroke='{C}' stroke-width='3'/>") + mark);
	}

	/// <summary>A pendant hanging from its chain; the pendant goes around (32, 44).</summary>
	static string Pendant(string pendant, bool heavyChain = false)
	{
		var chain = heavyChain
			? "stroke-width='4.5' stroke-dasharray='6 3' stroke-linecap='butt'"
			: "stroke-width='4' stroke-dasharray='0.1 5.5' stroke-linecap='round'";
		return $"<path d='M9,2 C11,16 22,26 32,28 C42,26 53,16 55,2' fill='none' stroke='{{D}}' {chain}/>" +
		       Ring(32, 29, 3, 2.5f, "{C}") + pendant;
	}

	/// <summary>A cut gem, as a kite with its facets.</summary>
	static string Crystal(float x, float y, float s)
	{
		return Place(x, y, s,
			Poly("32,8 46,26 32,56 18,26", "{H}", "{C}") + Poly("18,26 32,30 46,26 32,8", "{C}") +
			Line(32, 30, 32, 56, 1.5f, "{C}"));
	}

	static string Crown(float x, float y, float s, string fill = "{H}")
	{
		return Place(x, y, s, Poly("14,44 12,20 23,32 32,16 41,32 52,20 50,44", fill) + Line(14, 50, 50, 50, 5, fill));
	}

	static string Leaf(float x, float y, float s, string fill = "{H}")
	{
		return Place(x, y, s,
			Path("M32,6 C52,18 54,40 32,56 C10,40 12,18 32,6 Z", fill, fill, 1) + Line(32, 16, 32, 58, 3.5f, "{K}") +
			Line(32, 30, 42, 22, 3, "{K}") + Line(32, 42, 22, 34, 3, "{K}"));
	}

	static Dictionary<string, (SpellSchool, string)> BuildItemGlyphs()
	{
		var umbralBolt = Poly("44,8 20,34 31,34 19,56 46,26 35,26", "{U}", "{C}");

		return new Dictionary<string, (SpellSchool, string)>
		{
			// STAVES
			["CrystalStaff"] = (SpellSchool.Wick, Staff(Crystal(43, 21, 0.66f))),
			["StaffOfRestoration"] = (SpellSchool.Flame, Staff(Ring(43, 20, 13, 3.5f) + Flame(43, 21, 0.48f, "{C}", "{H}"))),
			["SlimewardenSceptre"] = (SpellSchool.Generic,
				Staff(Ring(43, 20, 13, 3.5f) + Star(43, 20, 4, 11, 3, "{H}"))),
			["ArcaneAccelerator"] = (SpellSchool.Wick, Staff(Hourglass(43, 19, 0.8f))),
			["StaffOfEternalFlame"] = (SpellSchool.Flame,
				Staff(Rays(43, 20, 15, 21, 8, 3) + Flame(43, 21, 0.68f, "{C}", "{H}"))),
			["Shadeweaver"] = (SpellSchool.Umbra, Staff(Circle(43, 20, 14, "{U}") + Spiral(43, 20, 2.2f, 12, 3))),
			["StaffOfKindling"] = (SpellSchool.Ember, Staff(Drop(43, 22, 1.1f) + Flame(43, 28, 0.3f, "{H}", "{C}"))),
			["TheBloodCane"] = (SpellSchool.Pyre,
				Line(12, 59, 32, 32, 6, "{D}") + Path("M32,32 C38,20 42,8 51,8 C58,8 61,15 56,21", "none", "{D}", 6) +
				Line(14.8f, 51.6f, 22.2f, 57.2f, 2.5f) + Line(20.2f, 44.5f, 27.6f, 50.1f, 2.5f) + Heart(44, 34, 0.85f) +
				Drop(56, 30, 0.35f)),
			["WrathOfEmbers"] = (SpellSchool.Ember,
				Staff(Coal(43, 23, 1.15f) + Coal(43, 23, 0.5f, "{H}") + Flame(35, 9, 0.3f) + Flame(51, 7, 0.36f))),

			// RINGS
			["CrystalRing"] = (SpellSchool.Wick, Band(Crystal(32, 21, 0.56f))),
			["RingOfMending"] = (SpellSchool.Flame, Band(Flame(32, 20, 0.65f, "{C}", "{H}"))),
			["PreciseRing"] = (SpellSchool.Generic, Band(Star(32, 21, 4, 16, 4, "{H}"))),
			["RulersSignet"] = (SpellSchool.Wick, Signet(Crown(32, 23, 0.42f))),
			["SignetOfTheOak"] = (SpellSchool.Generic, Signet(Leaf(32, 23, 0.38f))),
			["SeersBand"] = (SpellSchool.Generic, Band(Circle(32, 21, 12, "{K}") + Eye(32, 21, 0.7f))),
			["RingOfTriage"] = (SpellSchool.Flame,
				Band(Heart(32, 24, 1f) + Line(32, 15, 32, 26, 3, "{K}") + Line(26.5f, 20.5f, 37.5f, 20.5f, 3, "{K}"))),
			["BandOfRejuvenation"] = (SpellSchool.Ember,
				Band(Coal(32, 24, 0.95f) + Coal(32, 24, 0.45f, "{H}") + Circle(19, 10, 2.5f) + Circle(44, 8, 3, "{H}") +
				     Circle(31, 4, 2))),
			["BandOfAffliction"] = (SpellSchool.Umbra,
				Band(Circle(32, 22, 12, "{U}") + Ring(32, 22, 12, 3) + Path("M26,17 C35,13 40,24 32,28", "none", "{H}", 2.5f) +
				     Drop(14, 10, 0.4f) + Drop(50, 10, 0.4f))),
			["BandOfArcaneRecovery"] = (SpellSchool.Wick,
				Band(Drop(32, 24, 0.85f, "{H}") + ArcArrow(32, 22, 16, 190, 350, 3))),

			// AMULETS
			["EmberkeepersClasp"] = (SpellSchool.Ember,
				Pendant(Circle(32, 45, 13, "{K}") + Ring(32, 45, 13, 3.5f) + Coal(32, 45, 0.8f) + Coal(32, 45, 0.38f, "{H}"))),
			["BulwarkOfCinders"] = (SpellSchool.Ember,
				Pendant(Shield(32, 46, 1.15f, "{K}", "{C}") + Coal(32, 46, 0.7f) + Coal(32, 46, 0.32f, "{H}"))),
			["CrimsonAmulet"] = (SpellSchool.Pyre, Pendant(Drop(32, 46, 1.3f) + Circle(27, 51, 2.8f, "{H}"))),
			["ChainsOfCommand"] = (SpellSchool.Generic,
				Pendant(Flame(32, 44, 0.45f, "{C}", "{H}") + Flame(20, 53, 0.36f, "{C}", "{H}") + Flame(44, 53, 0.36f, "{C}", "{H}") +
				        Line(26, 47, 22, 49, 2.5f, "{D}") + Line(38, 47, 42, 49, 2.5f, "{D}"), true)),
			["ShadowboundChain"] = (SpellSchool.Umbra, Pendant(Place(32, 45, 0.68f, umbralBolt), true)),
			["ChainOfReflection"] = (SpellSchool.Umbra,
				Pendant(Circle(32, 46, 13, "{U}") + Ring(32, 46, 13, 3.5f) + Line(25, 51, 37, 39, 2.5f, "{H}") +
				        Line(31, 55, 39, 47, 2, "{H}"), true)),
			["PendantOfTheUmbralEmbrace"] = (SpellSchool.Umbra,
				Pendant(Flame(32, 46, 0.78f, "#000000", "{C}", "{C}") + Circle(32, 51, 2.2f, "{H}"))),
			["TheHeartOfLight"] = (SpellSchool.Flame,
				Pendant(Rays(32, 46, 16, 22, 8, 2.5f) + Heart(32, 49, 1.15f) + Flame(32, 46, 0.3f, "{H}", "#ffffff"))),
			["TheLifebindersCovenant"] = (SpellSchool.Flame,
				Pendant(Ring(32, 45, 13, 3) + Drop(32, 47, 0.55f, "{H}") + Circle(19, 45, 4, "{H}") +
				        Circle(45, 45, 4, "{H}") + Circle(32, 58, 4, "{H}")))
		};
	}

	/// <summary>Gloom: a flame being eclipsed by the dark.</summary>
	static string BuildGloomGlyph()
	{
		return Flame(26, 36, 0.85f, "{H}", "#ffffff") + Circle(38, 32, 15, "#000000") + Ring(38, 32, 15, 2.5f);
	}
}