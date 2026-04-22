using Godot;

namespace healerfantasy;

/// <summary>
/// Minimal player character used only in the Overworld scene.
/// Handles WASD / arrow-key movement and carries a Camera2D so the
/// library background scrolls as the player explores.
///
/// No spell-casting logic — the Player.cs NullReferenceException that
/// occurs when no boss group exists is avoided by using this lighter class.
/// </summary>
public partial class OverworldPlayer : CharacterBody2D
{
	[Export] public float Speed = 80f;

	Sprite2D _sprite = null!;

	public override void _Ready()
	{
		// ── Sprite ────────────────────────────────────────────────────────────
		_sprite = new Sprite2D();
		_sprite.Texture = GD.Load<Texture2D>("res://assets/32rogues/rogues.png");
		_sprite.RegionEnabled = true;
		_sprite.RegionRect = new Rect2(32, 64, 32, 32);
		AddChild(_sprite);

		// ── Collision ─────────────────────────────────────────────────────────
		var collision = new CollisionShape2D();
		collision.Position = new Vector2(0f, 4f);
		collision.Shape = new CapsuleShape2D { Radius = 8f, Height = 12f };
		AddChild(collision);

	}

	public override void _PhysicsProcess(double delta)
	{
		var dir = Vector2.Zero;

		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1f;
		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) dir.X -= 1f;
		// if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))  dir.Y += 1f;
		// if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))    dir.Y -= 1f;

		if (dir != Vector2.Zero)
		{
			// Flip sprite to face horizontal movement direction
			if (dir.X != 0f) _sprite.FlipH = -1 * dir.X < 0f;
			dir = dir.Normalized();
		}

		Velocity = dir * Speed;
		MoveAndSlide();
	}
}