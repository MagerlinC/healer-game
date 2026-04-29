using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Templar — a defensive frontliner who keeps aggro on the boss.
/// On combat start, marches forward to melee range before attacking.
/// Attacks every <see cref="AttackInterval"/> seconds with Shield Bash,
/// dealing a moderate melee hit. Hits less frequently than the Assassin
/// but with higher damage per swing.
/// </summary>
public partial class Templar : PartyMember
{
	public Templar()
	{
		MaxHealth = 200f;
	}

	/// <summary>Seconds between each Shield Bash.</summary>
	[Export] public float AttackInterval = 2.5f;

	/// <summary>Movement speed in pixels per second while closing to melee range.</summary>
	[Export] public float MoveSpeed = 80f;

	/// <summary>
	/// Distance from the boss centre at which the Templar stops and begins
	/// attacking. Should account for the boss sprite radius so the Templar
	/// stops just outside the boss rather than overlapping it.
	/// </summary>
	[Export] public float MeleeRange = 50f;

	float _attackTimer;
	TemplarShieldBashSpell _shieldBash;
	AnimatedSprite2D _sprite = null!;

	public override void _Ready()
	{
		base._Ready();
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.Play("idle");
		_sprite.AnimationFinished += OnAnimationFinished;
		// Stagger the first attack slightly so all three members don't hit at t=0.
		_attackTimer = AttackInterval * 0.4f;
		_shieldBash = new TemplarShieldBashSpell();
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
				ClampToArenaBoundary();
				return; // Not in range yet — skip attack logic
			}
			else
			{
				// In range — make sure we've fully stopped.
				Velocity = Vector2.Zero;
				MoveAndSlide();
				ClampToArenaBoundary();
			}
		}

		// ── Attack phase ─────────────────────────────────────────────────────
		_attackTimer -= (float)delta;
		if (_attackTimer <= 0f)
		{
			_attackTimer = GetHasteAdjustedAttackInterval(AttackInterval);
			PerformShieldBash();
		}
	}

	void PerformShieldBash()
	{
		var boss = FindPreferredBoss();
		if (boss == null) return;
		_sprite.Play("attack");
		SpellPipeline.Cast(_shieldBash, this, boss);
	}
}