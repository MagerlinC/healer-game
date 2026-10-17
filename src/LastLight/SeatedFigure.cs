using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// A rig sitting down: the figure's polygons are cut off at the hips and it is set
/// down so its hips rest on a seat (a log) drawn in front, which hides where the legs
/// would be. Place it at the point on top of the seat where the hips go.
/// </summary>
public partial class SeatedFigure : Node2D
{
	readonly RigDefinition _def;
	readonly float _hipY;
	readonly float _facing;

	/// <summary>The light the figure is lit by, and that light's point in its local space.</summary>
	public Node2D LitBy;
	public Vector2 LitByOffset;

	/// <param name="def">The rig to seat.</param>
	/// <param name="hipY">Design-space height of the hips (negative = up): everything below is hidden.</param>
	/// <param name="facing">Sign of the direction to face (rigs face right).</param>
	public SeatedFigure(RigDefinition def, float hipY, float facing)
	{
		_def = CutAtHips(def, hipY);
		_hipY = hipY;
		_facing = facing;
	}

	/// <summary>
	/// Drops the parts below the hips and flattens the rest at the hip line. (Clipping
	/// the drawn figure doesn't work: rigs are canvas groups with a screen-reading shader.)
	/// </summary>
	static RigDefinition CutAtHips(RigDefinition def, float hipY)
	{
		var kept = new System.Collections.Generic.List<RigPart>();
		foreach (var part in def.Parts)
		{
			var above = false;
			var points = new Vector2[part.Points.Length];
			for (var i = 0; i < points.Length; i++)
			{
				var p = part.Points[i];
				above |= p.Y < hipY;
				points[i] = new Vector2(p.X, Mathf.Min(p.Y, hipY));
			}
			if (above) kept.Add(part with { Points = points });
		}
		def.Parts = kept;
		// Feet on this node's origin (not mid-body), so the hips are a known height above it.
		def.FeetOffset = 0f;
		// Sitting still: just breathing, no shifting about.
		def.Breath *= 0.7f;
		return def;
	}

	public SeatedFigure() : this(new RigDefinition(), 0f, 1f)
	{
	}

	public override void _Ready()
	{
		// The origin sits on the seat; lift the figure so its hips are there.
		var figure = new Node2D { Position = new Vector2(0f, -_hipY * _def.Scale) };
		AddChild(figure);
		var rig = SilhouetteRig.Attach(figure, _def);
		rig.Face(_facing);
		rig.LitBy = LitBy;
		rig.LitByOffset = LitByOffset;
	}
}
