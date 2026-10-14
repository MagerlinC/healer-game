using Godot;
using healerfantasy;
using healerfantasy.LastLight;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Dawnwarden — a defensive frontliner who keeps aggro on the boss.
/// On combat start, marches forward to melee range before attacking.
/// Attacks every <see cref="AttackInterval"/> seconds with Shield Bash,
/// dealing a moderate melee hit. Hits less frequently than the Duskblade
/// but with higher damage per swing.
/// </summary>
public partial class Dawnwarden : PartyMember
{
	public Dawnwarden()
	{
		MaxHealth = 180f;
	}

	/// <summary>Seconds between each Shield Bash.</summary>
	[Export] public float AttackInterval = 2.5f;

	/// <summary>Movement speed in pixels per second while closing to melee range.</summary>
	[Export] public float MoveSpeed = 80f;

	/// <summary>
	/// Distance from the boss centre at which the Dawnwarden stops and begins
	/// attacking. Should account for the boss sprite radius so the Dawnwarden
	/// stops just outside the boss rather than overlapping it.
	/// </summary>
	[Export] public float MeleeRange = 50f;

	float _attackTimer;
	DawnwardenShieldBashSpell _shieldBash;
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
		// Stagger the first attack slightly so all three members don't hit at t=0.
		_attackTimer = AttackInterval * 0.4f;
		_shieldBash = new DawnwardenShieldBashSpell();
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
			PerformShieldBash();
		}
	}

	void PerformShieldBash()
	{
		var boss = FindPreferredBoss();
		if (boss == null) return;
		_visual.Play("attack");
		SpellPipeline.Cast(_shieldBash, this, boss);
	}

	protected override void ApplyDeathVisuals()
	{
		// The rig topples the figure and lets the darkness take it.
		_visual.Stop();
	}
}