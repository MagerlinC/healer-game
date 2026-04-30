using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

/// <summary>
/// A permanent ground hazard spawned when a <see cref="VolatileIcicleProjectile"/>
/// explodes. Renders as a blue semi-transparent ellipse — wider than tall to
/// support the game's 2D perspective — and deals damage per second to any
/// living party member standing within the ellipse.
///
/// The zone persists until the scene is freed (end of the encounter). Multiple
/// zones accumulate over the fight — the intended pressure is that the arena
/// gradually fills with frost, punishing the party for failing to kite
/// icicles to the edges.
/// </summary>
public partial class IcicleExplosionZone : Node2D
{
	// ── tunables ──────────────────────────────────────────────────────────────
	/// <summary>Horizontal semi-axis (world pixels). Wider axis for the 2D perspective look.</summary>
	public const float RadiusX = 70f;

	/// <summary>Vertical semi-axis. Roughly half of RadiusX gives a convincing depth foreshortening.</summary>
	public const float RadiusY = 38f;

	/// <summary>Polygon segments used to approximate the ellipse outline.</summary>
	const int Segments = 48;

	// ── colours ───────────────────────────────────────────────────────────────
	static readonly Color FillColour = new(0.25f, 0.55f, 1.0f, 0.30f); // icy blue fill
	static readonly Color BorderColour = new(0.50f, 0.80f, 1.0f, 0.85f); // bright ice border
	const float BorderWidth = 2.0f;

	// Texture
	const string TexturePath = "res://assets/enemies/queen-of-the-frozen-wastes/volatile-icicle-zone.png";

	// ── config ────────────────────────────────────────────────────────────────
	readonly float _damagePerTick;

	// ── runtime ───────────────────────────────────────────────────────────────
	float _tickTimer = 1f;

	// ── ctor ──────────────────────────────────────────────────────────────────
	public IcicleExplosionZone(float damagePerTick)
	{
		_damagePerTick = damagePerTick;
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		ZIndex = 1; // render below characters but above the background
		// apply texture
		var sprite = new Sprite2D();
		var texture = GD.Load<Texture2D>(TexturePath);
		if (texture != null)
		{
			sprite.Texture = texture;
			sprite.Scale = new Vector2(RadiusX * 2 / texture.GetSize().X, RadiusY * 2 / texture.GetSize().Y);
			sprite.Centered = true;
			AddChild(sprite);
		}
		else
		{
			GD.PrintErr("[IcicleExplosionZone] Could not load texture — drawing fallback ellipse.");
		}

		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		_tickTimer -= (float)delta;
		if (_tickTimer <= 0f)
		{
			_tickTimer = 1f;
			DamageOccupants();
		}
	}

	public override void _Draw()
	{
		// Build an ellipse polygon and draw it as a filled + outlined shape.
		var points = new Vector2[Segments];
		for (var i = 0; i < Segments; i++)
		{
			var angle = Mathf.Tau * i / Segments;
			points[i] = new Vector2(Mathf.Cos(angle) * RadiusX, Mathf.Sin(angle) * RadiusY);
		}

		// Filled ellipse.
		DrawPolygon(points, new[] { FillColour });

		// Outlined border — close the loop by repeating the first point.
		var border = new Vector2[Segments + 1];
		points.CopyTo(border, 0);
		border[Segments] = border[0];
		DrawPolyline(border, BorderColour, BorderWidth, true);
	}

	// ── private ───────────────────────────────────────────────────────────────

	void DamageOccupants()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character target || !target.IsAlive) continue;
			// Ellipse containment: (dx/rx)² + (dy/ry)² <= 1
			var delta = target.GlobalPosition - GlobalPosition;
			var ex = delta.X / RadiusX;
			var ey = delta.Y / RadiusY;
			if (ex * ex + ey * ey > 1f) continue;

			target.TakeDamage(_damagePerTick);
			target.RaiseFloatingCombatText(_damagePerTick, false, (int)SpellSchool.Generic, false);

			CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = GameConstants.FrozenPeakBossName,
				TargetName = target.CharacterName,
				AbilityName = "Volatile Icicle",
				Amount = _damagePerTick,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description = "Standing in frozen ground left by the Queen's icicle."
			});
		}
	}
}