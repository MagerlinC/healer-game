using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Assassin — a fast melee damage dealer.
/// On combat start, dashes forward to melee range before attacking.
/// Attacks every <see cref="AttackInterval"/> seconds with Sinister Strike.
/// Lower damage per hit than the Templar, but attacks nearly twice as often.
/// </summary>
public partial class Assassin : PartyMember
{
	/// <summary>Seconds between each Sinister Strike.</summary>
	[Export] public float AttackInterval = 1.5f;

	/// <summary>Movement speed in pixels per second while closing to melee range.</summary>
	[Export] public float MoveSpeed = 100f;

	/// <summary>
	/// Distance from the boss centre at which the Assassin stops and begins
	/// attacking. Should account for the boss sprite radius so the Assassin
	/// stops just outside the boss rather than overlapping it.
	/// </summary>
	[Export] public float MeleeRange = 50f;

	float _attackTimer;
	AssassinSinisterStrikeSpell _sinisterStrike;
	AnimatedSprite2D _sprite = null!;

	public override void _Ready()
	{
		base._Ready();
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.Play("idle");
		_sprite.AnimationFinished += OnAnimationFinished;
		// Stagger first attack so it doesn't sync with the Templar.
		_attackTimer = AttackInterval * 0.7f;
		_sinisterStrike = new AssassinSinisterStrikeSpell();
	}

	void OnAnimationFinished()
	{
		if (_sprite.Animation == "attack")
			_sprite.Play("idle");
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;

		var boss = FindPreferredBoss();

		// ── Movement phase ───────────────────────────────────────────────────
		// Keep advancing toward the boss until we're within melee range.
		// No attacks are made while out of range.
		if (boss != null)
		{
			var dist = GlobalPosition.DistanceTo(boss.GlobalPosition);
			if (dist > MeleeRange)
			{
				var direction = (boss.GlobalPosition - GlobalPosition).Normalized();
				Velocity = direction * MoveSpeed;
				MoveAndSlide();
				return; // Not in range yet — skip attack logic
			}
			else
			{
				// In range — make sure we've fully stopped.
				Velocity = Vector2.Zero;
				MoveAndSlide();
			}
		}

		// ── Attack phase ─────────────────────────────────────────────────────
		_attackTimer -= (float)delta;
		if (_attackTimer <= 0f)
		{
			_attackTimer = GetHasteAdjustedAttackInterval(AttackInterval);
			PerformSinisterStrike();
		}
	}

	void PerformSinisterStrike()
	{
		var boss = FindPreferredBoss();
		if (boss == null) return;
		_sprite.Play("attack");
		SpellPipeline.Cast(_sinisterStrike, this, boss);
	}
}