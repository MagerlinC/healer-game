using Godot;
using healerfantasy;
using healerfantasy.LastLight;
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
	public Assassin()
	{
		MaxHealth = 100f;
	}

	/// <summary>Seconds between each Sinister Strike.</summary>
	[Export] public float AttackInterval = 1.2f;

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
	CharacterVisual _visual = null!;

	public override void _Ready()
	{
		base._Ready();
		// Clip lengths from the original sprite animations (frames / fps).
		_visual = CharacterVisual.Attach(this, new()
		{
			["idle"] = new(2 / 4f, Loop: true),
			["attack"] = new(2 / 6f)
		});
		_visual.AnimationFinished += OnAnimationFinished;
		// Stagger first attack so it doesn't sync with the Templar.
		_attackTimer = AttackInterval * 0.7f;
		_sinisterStrike = new AssassinSinisterStrikeSpell();
	}

	void OnAnimationFinished()
	{
		if (_visual.Animation == "attack")
			_visual.Play("idle");
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;

		// Pause all movement while a knockback stun is active.
		if (IsKnockedBack) return;

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
			PerformSinisterStrike();
		}
	}

	void PerformSinisterStrike()
	{
		var boss = FindPreferredBoss();
		if (boss == null) return;
		_visual.Play("attack");
		SpellPipeline.Cast(_sinisterStrike, this, boss);
	}

	protected override void ApplyDeathVisuals()
	{
		// The rig topples the figure and lets the darkness take it.
		_visual.Stop();
	}
}