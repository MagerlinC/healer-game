using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace healerfantasy.LastLight;

/// <summary>How the figure shader treats a rig.</summary>
public enum RigMode
{
	/// <summary>Lit by the healer; missing health is darkness rising from the feet.</summary>
	Party = 0,

	/// <summary>Pure darkness. Bright colours (eyes, glints) stay emissive.</summary>
	Darkness = 1,

	/// <summary>The healer — the light itself. Drawn unshaded.</summary>
	Light = 2
}

/// <summary>A pivot that polygons hang from and that animations rotate/scale.</summary>
public sealed record RigJoint(string Name, string Parent, Vector2 Pivot);

/// <summary>
/// One flat polygon. <see cref="Points"/> are in the rig's design space
/// (feet at 0,0, up is negative Y, facing right) — the same space for every
/// part, so shapes can be authored without thinking about joints.
/// </summary>
public sealed record RigPart(string Joint, Color Color, Vector2[] Points, int Z = 0);

/// <summary>
/// A code-defined character: joints + polygons + a few animation knobs.
/// Party albedo is greyscale (white = fully lit); darkness rigs use near-black
/// with bright colours for anything that should glow.
/// </summary>
public sealed class RigDefinition
{
	public string Id = "";
	public RigMode Mode = RigMode.Party;

	/// <summary>Design-space height (feet to top), used for lighting and the darkness line.</summary>
	public float Height = 180f;

	/// <summary>Design-space width of the main body, used for the light gradient.</summary>
	public float Width = 70f;

	/// <summary>Design units → world units.</summary>
	public float Scale = 0.3f;

	public List<RigJoint> Joints = new();
	public List<RigPart> Parts = new();

	// ── animation knobs ──────────────────────────────────────────────────────
	public float SwingAngle = 1.6f;   // attack swing of the "arm" joint, radians
	public float Lunge = 8f;          // forward step on the strike, design units
	public float Breath = 1f;         // idle breathing strength
	public float CastRaise = 1.2f;    // how far "arm" lifts while casting

	/// <summary>Darkness rigs: size of the ragged pool under the feet (design units). Zero = none.</summary>
	public Vector2 Pool = Vector2.Zero;

	/// <summary>Colour of the pool; dungeons tint their darkness (blood, rust, frost).</summary>
	public Color PoolColor = new(0.012f, 0.01f, 0.01f, 0.94f);

	/// <summary>Flying: the figure floats above its pool, which becomes a faint shadow.</summary>
	public bool Hover;
	public float HoverHeight = 40f;

	/// <summary>Draw in front of the party instead of behind (e.g. vines wrapping an ally).</summary>
	public bool InFront;

	/// <summary>
	/// Per-rig overrides for how sprite animation names map to poses
	/// ("attack", "cast", "move", "idle"). Unlisted names use the shared defaults.
	/// </summary>
	public Dictionary<string, string> AnimAliases = new();

	/// <summary>Joint the healer's light is emitted from.</summary>
	public string LightJoint = "";

	/// <summary>Offset from <see cref="LightJoint"/>'s pivot to the light's centre, design units.</summary>
	public Vector2 LightOffset = Vector2.Zero;

	/// <summary>
	/// A non-healer figure that carries its own small light (e.g. a lantern) at
	/// <see cref="LightJoint"/>: it is lit from there instead of by the healer, and the
	/// light holds back the dark around it. Screen-height fraction; 0 = no own light.
	/// </summary>
	public float OwnLightRadius;

	// ── authoring helpers ────────────────────────────────────────────────────

	public RigDefinition Joint(string name, string parent, float x, float y)
	{
		Joints.Add(new RigJoint(name, parent, new Vector2(x, y)));
		return this;
	}

	public RigDefinition Part(string joint, Color color, string points, int z = 0)
	{
		Parts.Add(new RigPart(joint, color, P(points), z));
		return this;
	}

	public RigDefinition Part(string joint, Color color, Vector2[] points, int z = 0)
	{
		Parts.Add(new RigPart(joint, color, points, z));
		return this;
	}

	/// <summary>Parses "x,y x,y x,y" into points.</summary>
	public static Vector2[] P(string s)
	{
		var tokens = s.Split(new[] { ' ', '\n', '\t', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
		var pts = new Vector2[tokens.Length];
		for (var i = 0; i < tokens.Length; i++)
		{
			var xy = tokens[i].Split(',');
			pts[i] = new Vector2(
				float.Parse(xy[0], CultureInfo.InvariantCulture),
				float.Parse(xy[1], CultureInfo.InvariantCulture));
		}
		return pts;
	}

	/// <summary>A regular polygon approximating a circle.</summary>
	public static Vector2[] Circle(float cx, float cy, float r, int segments = 14)
	{
		var pts = new Vector2[segments];
		for (var i = 0; i < segments; i++)
		{
			var a = Mathf.Tau * i / segments;
			pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
		}
		return pts;
	}

	/// <summary>Turns an open polyline into a filled strip of the given width.</summary>
	public static Vector2[] Thick(float width, string points) => Thick(width, P(points));

	public static Vector2[] Thick(float width, Vector2[] line)
	{
		var n = line.Length;
		var left = new Vector2[n];
		var right = new Vector2[n];
		for (var i = 0; i < n; i++)
		{
			var prev = line[Mathf.Max(i - 1, 0)];
			var next = line[Mathf.Min(i + 1, n - 1)];
			var dir = (next - prev).Normalized();
			var normal = new Vector2(-dir.Y, dir.X) * (width * 0.5f);
			left[i] = line[i] + normal;
			right[i] = line[i] - normal;
		}
		var pts = new Vector2[n * 2];
		for (var i = 0; i < n; i++)
		{
			pts[i] = left[i];
			pts[n * 2 - 1 - i] = right[i];
		}
		return pts;
	}

	/// <summary>
	/// Scatters small glowing dots (stars, embers, frost) inside a rectangle.
	/// Deterministic per seed.
	/// </summary>
	public RigDefinition Specks(string joint, Color color, float x0, float y0, float x1, float y1,
		int count, int seed, float size = 1.6f, int z = 0)
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)seed };
		for (var i = 0; i < count; i++)
		{
			var r = size * rng.RandfRange(0.6f, 1.4f);
			Parts.Add(new RigPart(joint, color,
				Circle(rng.RandfRange(x0, x1), rng.RandfRange(y0, y1), r, 6), z));
		}
		return this;
	}

	/// <summary>Greyscale albedo for party rigs.</summary>
	public static Color G(float v) => new(v, v, v);
}
