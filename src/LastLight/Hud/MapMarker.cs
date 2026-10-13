using Godot;

namespace healerfantasy.LastLight.Hud;

/// <summary>
/// World-map glyph: a keep silhouette for dungeons, a small flame for camps.
/// Lit markers flicker with a soft glow; dark ones are just an outline in the void.
/// </summary>
public partial class MapMarker : Control
{
	public bool IsDungeon;
	public bool Lit;
	public Color Tint = LastLightHud.Light;

	static readonly Vector2[] Keep = RigDefinition.P(
		"-13,9 -13,-4 -10,-9 -7,-4 -7,0 -3,0 -3,-11 0,-16 3,-11 3,0 7,0 7,-4 10,-9 13,-4 13,9");
	static readonly Vector2[] Gate = RigDefinition.P("-2.5,9 -2.5,3 0,1 2.5,3 2.5,9");
	static readonly Vector2[] Flame = RigDefinition.P("0,-12 4,-5 5,1 3,6 0,8 -3,6 -5,1 -4,-5");
	static readonly Vector2[] Core = RigDefinition.P("0,-4 2,1 0,6 -2,1");

	float _t;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(0f, 30f);
		_t = GD.Randf() * 10f;
	}

	public override void _Process(double delta)
	{
		if (!Lit) return;
		_t += (float)delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		var c = Size / 2f + new Vector2(0f, 1f);
		var flick = Lit ? 1f + 0.08f * Mathf.Sin(_t * 9f) + 0.05f * Mathf.Sin(_t * 23f) : 1f;

		if (Lit)
			for (var i = 0; i < 4; i++)
				DrawCircle(c, (16f - i * 3f) * flick, new Color(Tint, 0.06f + i * 0.03f));

		if (IsDungeon)
		{
			DrawColoredPolygon(Offset(Keep, c, 1f), Lit ? Tint : new Color(Tint, 0.5f));
			DrawColoredPolygon(Offset(Gate, c, 1f), LastLightHud.Pit);
		}
		else
		{
			DrawColoredPolygon(Offset(Flame, c, flick), Lit ? Tint : new Color(Tint, 0.5f));
			if (Lit) DrawColoredPolygon(Offset(Core, c, flick), new Color(1f, 0.95f, 0.85f));
			DrawLine(c + new Vector2(-7f, 9f), c + new Vector2(7f, 9f), new Color(Tint, 0.7f), 1.5f);
		}
	}

	static Vector2[] Offset(Vector2[] pts, Vector2 c, float scaleY)
	{
		var r = new Vector2[pts.Length];
		for (var i = 0; i < pts.Length; i++) r[i] = c + new Vector2(pts[i].X, pts[i].Y * scaleY);
		return r;
	}
}
