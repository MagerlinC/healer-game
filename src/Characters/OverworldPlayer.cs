using System;
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

	/// <summary>World-space X bounds set by <see cref="OverworldController"/> to keep
	/// the player within the background image edges.</summary>
	public float XMin = float.NegativeInfinity;
	public float XMax = float.PositiveInfinity;

	AnimatedSprite2D _sprite = null!;

	public override void _Ready()
	{
		// ── Sprite ────────────────────────────────────────────────────────────
		var frames = new SpriteFrames();
		frames.AddAnimation("idle");
		frames.SetAnimationSpeed("idle", 4.0);
		frames.SetAnimationLoop("idle", true);
		foreach (var i in new[] { 1, 2, 3 })
			frames.AddFrame("idle", GD.Load<Texture2D>($"res://assets/characters/healer/idle{i}.png"));

		_sprite = new AnimatedSprite2D();
		// quarter size
		_sprite.Scale = new Vector2(0.15f, 0.15f);
		_sprite.SpriteFrames = frames;
		_sprite.Play("idle");
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
			if (dir.X != 0f) _sprite.FlipH = dir.X < 0f;
			dir = dir.Normalized();
		}

		Velocity = dir * Speed;
		MoveAndSlide();

		// Clamp to background edges (X only — vertical movement is disabled).
		Position = new Vector2(Mathf.Clamp(Position.X, XMin, XMax), Position.Y);
	}
}