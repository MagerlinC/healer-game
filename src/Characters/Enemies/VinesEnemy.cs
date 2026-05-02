using Godot;
using healerfantasy;
using healerfantasy.SpellResources;

/// <summary>
/// Rune of Nature — a vine creature that spawns every
/// <see cref="GameConstants.RuneNatureVinesInterval"/> seconds during boss fights.
///
/// On spawn the vines latch onto a random alive party member and deal
/// <see cref="GameConstants.RuneNatureVinesDamagePerSecond"/> damage per second
/// to that target until the vines are killed.
///
/// The vines have their own health bar displayed below the boss bar (managed by
/// <see cref="healerfantasy.UI.GameUI"/>).  Killing them removes them from the
/// scene and clears the health bar.
/// </summary>
public partial class VinesEnemy : Character
{
	// ── public state ──────────────────────────────────────────────────────────

	/// <summary>The party-member character this vine is currently latched onto.</summary>
	public Character? AttachedTarget { get; private set; }

	/// <summary>Display name shown on the vines health bar.</summary>
	public string DisplayName { get; private set; } = "Growing Vines";

	// ── internal ──────────────────────────────────────────────────────────────

	float _damageTimer;
	const float DamageInterval = 1.0f;

	AnimatedSprite2D _sprite = null!;

	// ── ctor ──────────────────────────────────────────────────────────────────

	public VinesEnemy(Character target, string instanceName)
	{
		MaxHealth = GameConstants.RuneNatureVinesMaxHealth;
		IsFriendly = false;
		CharacterName = instanceName;
		DisplayName = "Growing Vines";
		AttachedTarget = target;
	}

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		RemoveFromGroup("party");
		AddToGroup(GameConstants.BossGroupName);
		AddToGroup(GameConstants.VinesGroupName);

		// ── sprite (3-frame grow animation) ──────────────────────────────────
		_sprite = new AnimatedSprite2D();
		_sprite.Scale = new Vector2(0.25f, 0.25f);
		var frames = new SpriteFrames();

		frames.AddAnimation("idle");
		frames.SetAnimationLoop("idle", true);
		frames.SetAnimationSpeed("idle", 3f);

		for (var i = 1; i <= 3; i++)
		{
			var path = i switch
			{
				1 => AssetConstants.VinesFrame1Path,
				2 => AssetConstants.VinesFrame2Path,
				_ => AssetConstants.VinesFrame3Path
			};
			var tex = GD.Load<Texture2D>(path);
			frames.AddFrame("idle", tex);
		}

		_sprite.SpriteFrames = frames;
		_sprite.Scale = new Vector2(1.2f, 1.2f);
		AddChild(_sprite);
		_sprite.Play("idle");

		// Position near the attached target if possible, else use zero.
		if (AttachedTarget != null)
			GlobalPosition = AttachedTarget.GlobalPosition + new Vector2(0f, -30f);
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive || AttachedTarget == null) return;

		// Follow target loosely.
		GlobalPosition = AttachedTarget.GlobalPosition + new Vector2(0f, -30f);

		// Periodic damage tick.
		_damageTimer -= (float)delta;
		if (_damageTimer <= 0f)
		{
			_damageTimer = DamageInterval;
			if (AttachedTarget.IsAlive)
				AttachedTarget.TakeDamage(GameConstants.RuneNatureVinesDamagePerSecond);
		}
	}

	// ── death ─────────────────────────────────────────────────────────────────

	protected override void ApplyDeathVisuals()
	{
		if (_sprite != null) _sprite.Modulate = new Color(0.5f, 0.5f, 0.5f);
		// Remove from scene shortly after death so the health bar can clean up.
		var timer = new Timer { WaitTime = 0.8f, OneShot = true, Autostart = true };
		AddChild(timer);
		timer.Timeout += () => QueueFree();
	}
}