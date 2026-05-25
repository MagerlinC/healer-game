using Godot;

namespace healerfantasy;

/// <summary>
/// Minimal player character used only in the Overworld and Camp scenes.
/// Handles WASD / arrow-key movement in 2D (left/right and up/down).
///
/// Animation and audio are managed entirely here so both
/// <see cref="OverworldController"/> and CampController get identical
/// behaviour from <see cref="LoadoutController.SetupPlayer"/>.
///
/// Vertical movement is restricted to the bottom half of the reference canvas
/// via <see cref="YMin"/> / <see cref="YMax"/> set by <see cref="LoadoutController"/>.
///
/// No spell-casting logic — the Player.cs NullReferenceException that
/// occurs when no boss group exists is avoided by using this lighter class.
/// </summary>
public partial class OverworldPlayer : CharacterBody2D
{
	[Export] public float Speed = 80f;

	/// <summary>World-space X bounds set by <see cref="LoadoutController"/> to keep
	/// the player within the background image edges.</summary>
	public float XMin = float.NegativeInfinity;

	public float XMax = float.PositiveInfinity;

	/// <summary>World-space Y bounds set by <see cref="LoadoutController"/>.
	/// Vertical movement is restricted to the bottom half of the reference canvas
	/// so the player stays within the visible play area.</summary>
	public float YMin = float.NegativeInfinity;

	public float YMax = float.PositiveInfinity;

	AnimatedSprite2D _sprite = null!;

	// ── Float lift parameters ─────────────────────────────────────────────────
	// The CharacterBody2D stays on the ground for correct collision; only the
	// sprite's local Y position is offset to create the soaring illusion.

	/// <summary>How many pixels above ground the sprite hovers while moving.</summary>
	const float FloatHeight = 6f;

	/// <summary>Additional ±pixels of slow sine-wave bob layered on top of the lift.</summary>
	const float BobAmplitude = 2f;

	/// <summary>Sine oscillations per second while floating.</summary>
	const float BobSpeed = 0.7f;

	/// <summary>Lerp factor controlling how quickly the sprite ascends / descends.</summary>
	const float LiftSpeed = 2.5f;

	/// <summary>Current lerped base lift offset (0 = ground, -FloatHeight = fully airborne).</summary>
	float _liftOffset = 0f;

	/// <summary>Phase accumulator for the sine bob (advances only while moving).</summary>
	float _bobPhase = 0f;

	public override void _Ready()
	{
		// ── Sprite frames ─────────────────────────────────────────────────────
		var frames = new SpriteFrames();

		frames.AddAnimation("idle");
		frames.SetAnimationSpeed("idle", 4.0);
		frames.SetAnimationLoop("idle", true);
		foreach (var i in new[] { 1, 2, 3 })
			frames.AddFrame("idle", GD.Load<Texture2D>($"res://assets/characters/healer/idle{i}.png"));

		frames.AddAnimation("walk");
		frames.SetAnimationSpeed("walk", 4.0); // slowed from 8 → 4 fps for a dreamier float
		frames.SetAnimationLoop("walk", true);
		foreach (var i in new[] { 1, 2, 3 })
			frames.AddFrame("walk", GD.Load<Texture2D>($"res://assets/characters/healer/float{i}.png"));

		_sprite = new AnimatedSprite2D();
		_sprite.Scale = new Vector2(GameConstants.HealerSpriteScale * 1.20f, GameConstants.HealerSpriteScale * 1.20f);
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
		var dt = (float)delta;
		var dir = Vector2.Zero;

		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1f;
		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) dir.X -= 1f;
		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) dir.Y += 1f;
		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) dir.Y -= 1f;

		if (dir != Vector2.Zero)
		{
			// Flip the sprite to face the direction of travel.
			// The healer sprite is drawn facing left, so FlipH=false → left,
			// FlipH=true → right.
			if (dir.X != 0f)
				_sprite.FlipH = dir.X > 0f;

			dir = dir.Normalized();

			if (_sprite.Animation != "walk")
				_sprite.Play("walk");

			// Slowly ascend toward FloatHeight.
			_liftOffset = Mathf.Lerp(_liftOffset, -FloatHeight, LiftSpeed * dt);

			// Advance the sine bob only while airborne.
			_bobPhase += BobSpeed * Mathf.Tau * dt;
			_sprite.Position = new Vector2(0f, _liftOffset + Mathf.Sin(_bobPhase) * BobAmplitude);
		}
		else
		{
			if (_sprite.Animation != "idle")
				_sprite.Play("idle");

			// Slowly descend back to the ground.
			_liftOffset = Mathf.Lerp(_liftOffset, 0f, LiftSpeed * dt);
			_sprite.Position = new Vector2(0f, _liftOffset);
		}

		Velocity = dir * Speed;
		MoveAndSlide();

		// Clamp to background edges and the allowed vertical range.
		Position = new Vector2(
			Mathf.Clamp(Position.X, XMin, XMax),
			Mathf.Clamp(Position.Y, YMin, YMax));
	}
}