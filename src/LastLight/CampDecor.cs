using Godot;

namespace healerfantasy.LastLight;

/// <summary>
/// The lived-in bits of the camp around the bonfire: the Lightkeeper's companions
/// sitting on logs, facing the fire, with a tent and a woodpile behind them. Flat
/// silhouettes, rimmed with firelight on the side that faces the fire.
/// Placed with its origin at the base of the bonfire.
/// </summary>
public partial class CampDecor : Node2D
{
	/// <summary>The fire's light, in this node's space (the flame of the bonfire).</summary>
	static readonly Vector2 FireLight = new(0f, -28f);

	const float FigureScale = 1.35f;

	// (centre, angle, length, radius) of each log, left and right of the fire
	static readonly (Vector2 Centre, float Angle, float Length, float Radius)[] Logs =
	{
		(new Vector2(-104f, 44f), 0.08f, 64f, 7f),
		(new Vector2(116f, 46f), -0.06f, 116f, 7.5f)
	};

	public override void _Ready()
	{
		// The party sits round the fire: the Duskblade on the left log, the Dawnwarden
		// and the Starcaller sharing the right one. Hip heights are per rig (design y).
		Seat(RigLibrary.DuskbladeResting(), -40f, 0, -6f, 1f);
		Seat(RigLibrary.Dawnwarden(), -58f, 1, -32f, -1f);
		Seat(RigLibrary.Starcaller(), -55f, 1, 34f, -1f);

		// The logs are drawn over the seated figures, hiding where their legs would be.
		var logs = new Node2D { ZIndex = 1 };
		logs.Draw += () =>
		{
			foreach (var log in Logs) Log(logs, log.Centre, log.Angle, log.Length, log.Radius);
		};
		AddChild(logs);
	}

	/// <summary>Sits a rig on a log, <paramref name="along"/> units from its centre.</summary>
	void Seat(RigDefinition def, float hipY, int log, float along, float facing)
	{
		var (centre, angle, _, radius) = Logs[log];
		var top = centre + Vector2.Right.Rotated(angle) * along + Vector2.Up.Rotated(angle) * radius;
		var figure = new SeatedFigure(def, hipY, facing)
		{
			// Sunk into the log a little, so the cut at the hips is hidden behind it.
			Position = top + new Vector2(0f, 3f),
			Scale = Vector2.One * FigureScale,
			LitBy = this,
			LitByOffset = FireLight
		};
		AddChild(figure);
	}

	static readonly Color Bark = new(0.06f, 0.048f, 0.04f);
	static readonly Color Canvas = new(0.17f, 0.14f, 0.11f);
	static readonly Color CanvasShade = new(0.085f, 0.072f, 0.06f);
	static readonly Color BarkShade = new(0.035f, 0.028f, 0.024f);
	static readonly Color Cut = new(0.2f, 0.14f, 0.09f);
	static readonly Color Rim = new(0.75f, 0.4f, 0.16f, 0.55f);

	public override void _Draw()
	{
		Tent(new Vector2(208f, -48f));
		Woodpile(new Vector2(-178f, 72f));
	}

	static void Log(CanvasItem canvas, Vector2 centre, float angle, float length, float radius)
	{
		var along = Vector2.Right.Rotated(angle) * length * 0.5f;
		var up = Vector2.Up.Rotated(angle) * radius;
		// a rounded trunk, darker on its underside
		canvas.DrawColoredPolygon(new[] { centre - along - up, centre + along - up, centre + along + up, centre - along + up }, Bark);
		canvas.DrawCircle(centre - along, radius, Bark, true, -1f, true);
		canvas.DrawCircle(centre + along, radius, Bark, true, -1f, true);
		canvas.DrawLine(centre - along - up * 0.6f, centre + along - up * 0.6f, BarkShade, radius * 0.7f, true);
		// firelight along the top
		canvas.DrawLine(centre - along + up, centre + along + up, Rim, 1.5f, true);
	}

	void Tent(Vector2 b)
	{
		var peak = b + new Vector2(0f, -62f);
		var back = peak + new Vector2(28f, 6f);
		// the side running away from the fire, in shade
		DrawColoredPolygon(new[] { peak, back, b + new Vector2(66f, 2f), b + new Vector2(40f, 0f) }, CanvasShade);
		// the front, facing the fire
		DrawColoredPolygon(new[] { b + new Vector2(-40f, 0f), peak, b + new Vector2(40f, 0f) }, Canvas);
		// the door flap, open
		DrawColoredPolygon(new[] { b + new Vector2(-13f, 0f), b + new Vector2(-2f, -32f), b + new Vector2(9f, 0f) },
			new Color(0.01f, 0.01f, 0.01f));
		// crossed poles poking out of the top
		DrawLine(peak + new Vector2(-5f, -7f), peak + new Vector2(4f, 6f), Bark, 2f, true);
		DrawLine(peak + new Vector2(5f, -7f), peak + new Vector2(-4f, 6f), Bark, 2f, true);
		DrawLine(b + new Vector2(-40f, 0f), peak, Rim, 1.5f, true);
	}

	void Woodpile(Vector2 b)
	{
		// split logs stacked end-on: bark rings round the sawn faces
		const float r = 5.5f;
		Vector2[] ends = { new(-11f, 0f), new(0f, 0f), new(11f, 0f), new(-5.5f, -9.5f), new(5.5f, -9.5f), new(0f, -19f) };
		foreach (var e in ends) DrawEllipse(b + e, r + 1.4f, r + 1.4f, Bark);
		foreach (var e in ends) DrawEllipse(b + e, r * 0.9f, r * 0.9f, Cut);
		// firelight on the side that faces the fire
		DrawArc(b + new Vector2(5.5f, -9.5f), r + 1.4f, -1.4f, 0.4f, 8, Rim, 1.5f, true);
		DrawArc(b + new Vector2(11f, 0f), r + 1.4f, -1.4f, 0.4f, 8, Rim, 1.5f, true);
		DrawArc(b + new Vector2(0f, -19f), r + 1.4f, -1.4f, 0.4f, 8, Rim, 1.5f, true);
	}

	void DrawEllipse(Vector2 c, float rx, float ry, Color color)
	{
		var pts = new Vector2[16];
		for (var i = 0; i < pts.Length; i++)
		{
			var a = Mathf.Tau * i / pts.Length;
			pts[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
		}
		DrawColoredPolygon(pts, color);
	}
}
