using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

/// <summary>
/// One of The Infinite Abyss's Falling Tendrils. A dark circle grows on the floor
/// under its mark; when it fills, a tendril spears down from the top of the screen
/// and hits every party member standing inside.
///
/// A followed mark (an ally) is tracked until shortly before impact, so the circle
/// stays on them; an unfollowed mark (the Lightkeeper's spot) stays put, so she can
/// step out of it. Spawned by <see cref="TheInfiniteAbyss"/> as a sibling of the boss.
/// </summary>
public partial class FallingTendrilStrike : Node2D
{
	/// <summary>Who the strike is aimed at; followed while <see cref="FollowTarget"/> is set.</summary>
	public Character Target { get; set; }
	public bool FollowTarget { get; set; }
	public float Delay { get; set; } = 2.3f;
	public float Damage { get; set; } = 45f;
	public string BossName { get; set; } = GameConstants.AbyssBossName;

	/// <summary>Half-width of the struck area in world units (the floor ellipse is flatter).</summary>
	const float Radius = 26f;
	const float Flatten = 0.5f;
	const float LockBefore = 0.6f; // stop following this long before impact
	const float SpearTime = 0.55f; // descend, hold, retract
	static readonly Vector2 FeetOffset = new(0f, 20f);

	float _t, _strikeT;
	bool _struck;

	/// <summary>Where a character's feet are, in world space.</summary>
	public static Vector2 FeetOf(Character c) => c.GlobalPosition + FeetOffset;

	public override void _Ready()
	{
		// On the floor: above the boss's pool, under the figures. The spear is drawn by a child on top.
		ZIndex = -1;
		AddChild(new Spear { Strike = this, ZIndex = 3, ZAsRelative = false });
	}

	public override void _Process(double delta)
	{
		var dt = (float)delta;
		if (!_struck)
		{
			_t += dt;
			if (FollowTarget && _t < Delay - LockBefore && IsInstanceValid(Target) && Target.IsAlive)
				GlobalPosition = FeetOf(Target);
			if (_t >= Delay) Strike();
		}
		else
		{
			_strikeT += dt;
			if (_strikeT >= SpearTime) QueueFree();
		}
		QueueRedraw();
	}

	void Strike()
	{
		_struck = true;
		foreach (var node in GetTree().GetNodesInGroup(GameConstants.PartyGroupName))
		{
			if (node is not Character c || !c.IsAlive) continue;
			var d = FeetOf(c) - GlobalPosition;
			var inside = d.X * d.X / (Radius * Radius) + d.Y * d.Y / (Radius * Flatten * Radius * Flatten) <= 1f;
			if (!inside) continue;

			c.TakeDamage(Damage);
			c.RaiseFloatingCombatText(Damage, false, (int)SpellSchool.Umbra, false);
			CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = BossName,
				TargetName = c.CharacterName,
				AbilityName = "Falling Tendril",
				Amount = Damage,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description = "Speared by a tendril falling from the dark."
			});
		}
	}

	public override void _Draw()
	{
		if (_struck) return;
		var p = Mathf.Clamp(_t / Delay, 0f, 1f);
		var pts = Ellipse(Radius);
		DrawColoredPolygon(pts, new Color(0.03f, 0.015f, 0.05f, 0.35f));
		// The fill grows toward the rim as the tendril falls.
		DrawColoredPolygon(Ellipse(Radius * p), new Color(0.05f, 0.02f, 0.08f, 0.55f + 0.3f * p));
		var rim = new Color(0.78f, 0.7f, 0.95f, 0.45f + 0.45f * p);
		DrawPolyline([..pts, pts[0]], rim, 1.5f, true);
	}

	static Vector2[] Ellipse(float r)
	{
		var pts = new Vector2[28];
		for (var i = 0; i < pts.Length; i++)
		{
			var a = Mathf.Tau * i / pts.Length;
			pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * Flatten);
		}
		return pts;
	}

	/// <summary>The tendril itself: drops from above the screen to the mark, then pulls back up.</summary>
	partial class Spear : Node2D
	{
		public FallingTendrilStrike Strike = null!;

		public override void _Process(double delta) => QueueRedraw();

		public override void _Draw()
		{
			if (!Strike._struck) return;
			var t = Strike._strikeT / SpearTime;
			// 0–0.2 drop, 0.2–0.6 hold, 0.6–1 retract
			var reach = t < 0.2f ? t / 0.2f : t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
			const float top = -320f;
			var tip = new Vector2(0f, Mathf.Lerp(top, 0f, reach));
			if (tip.Y <= top + 1f) return;

			// A tapering, slightly bent black tendril with a pale edge.
			var len = tip.Y - top;
			var left = new Vector2[8];
			var right = new Vector2[8];
			for (var i = 0; i < 8; i++)
			{
				var f = i / 7f;
				var y = top + len * f;
				var bend = Mathf.Sin(f * Mathf.Pi) * 10f;
				var w = Mathf.Lerp(9f, 1.5f, f);
				left[i] = new Vector2(bend - w, y);
				right[7 - i] = new Vector2(bend + w, y);
			}
			var poly = new Vector2[16];
			left.CopyTo(poly, 0);
			right.CopyTo(poly, 8);
			DrawColoredPolygon(poly, new Color(0.01f, 0.005f, 0.015f, 1f));
			DrawPolyline(left, new Color(0.7f, 0.62f, 0.88f, 0.55f), 1.2f, true);
			if (t >= 0.2f && t < 0.6f)
				DrawCircle(tip, 7f, new Color(0.75f, 0.65f, 0.95f, 0.35f * (1f - (t - 0.2f) / 0.4f)));
		}
	}
}
