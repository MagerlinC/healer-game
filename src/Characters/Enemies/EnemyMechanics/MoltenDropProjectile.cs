using Godot;
using healerfantasy;

/// <summary>
/// A bead of molten tallow let fall by the Tallow Queen.
///
/// The projectile floats slowly toward the healer (the player character).
/// Every frame it checks whether it is within <see cref="CollisionRadius"/>
/// pixels of the healer — if so it explodes. NPC party members do not
/// trigger the drop, so only the player can detonate it by running into it
/// (or avoiding it to steer the blast to the arena edge).
///
///   1. It removes itself from the scene.
///   2. It spawns an <see cref="MoltenWaxPool"/> at its current position,
///      creating a permanent pool of hot wax on the ground.
///
/// The mechanic rewards running the drop to the edge of the arena so the
/// resulting zone doesn't block critical space.
/// </summary>
public partial class MoltenDropProjectile : Node2D
{
	// ── tunables ──────────────────────────────────────────────────────────────
	const float CollisionRadius = 28f;

	const string MoltenDropTexturePath =
		"res://assets/enemies/the-tallow-queen/molten-drop.png";

	/// <summary>Tumble speed in radians per second. ~0.8 rad/s ≈ one full spin every 8 s.</summary>
	const float TumbleSpeed = 0.8f;

	// ── config ────────────────────────────────────────────────────────────────
	readonly float _speed;
	readonly float _zoneDamagePerTick;

	bool _exploded;

	// ── ctor ──────────────────────────────────────────────────────────────────
	public MoltenDropProjectile(float speed, float zoneDamagePerTick)
	{
		_speed = speed;
		_zoneDamagePerTick = zoneDamagePerTick;
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		ZIndex = 5;

		// Sprite — the molten-drop.png asset from the queen's folder.
		var sprite = new Sprite2D();
		var texture = GD.Load<Texture2D>(MoltenDropTexturePath);
		if (texture != null)
		{
			sprite.Texture = texture;
			sprite.Scale = new Vector2(0.4f, 0.4f);
		}
		else
		{
			// Fallback: small amber circle drawn via a ColorRect substitute.
			GD.PrintErr("[MoltenDrop] Could not load molten drop texture — projectile will be invisible.");
		}

		AddChild(sprite);
	}

	public override void _Process(double delta)
	{
		if (_exploded) return;

		// ── tumble ───────────────────────────────────────────────────────────
		Rotation += TumbleSpeed * (float)delta;

		// ── move toward the healer ────────────────────────────────────────────
		var healer = FindHealer();
		if (healer != null && healer.IsAlive)
		{
			var direction = (healer.GlobalPosition - GlobalPosition).Normalized();
			GlobalPosition += direction * _speed * (float)delta;
		}

		// ── collision check — only the healer (player) triggers the drop ─────
		// NPC party members pass through it; the player must actively kite it.
		if (healer != null && healer.IsAlive &&
		    GlobalPosition.DistanceTo(healer.GlobalPosition) <= CollisionRadius)
		{
			Explode();
		}
	}

	// ── private ───────────────────────────────────────────────────────────────

	Character FindHealer()
	{
		foreach (var node in GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive && c.CharacterName == GameConstants.LightkeeperName)
				return c;
		return null;
	}

	void Explode()
	{
		if (_exploded) return;
		_exploded = true;

		// Spawn the persistent wax pool at this position.
		var zone = new MoltenWaxPool(_zoneDamagePerTick);
		zone.GlobalPosition = GlobalPosition;

		var parent = GetParent();
		parent?.AddChild(zone);

		GD.Print($"[MoltenDrop] Exploded at {GlobalPosition} — zone spawned.");
		QueueFree();
	}
}