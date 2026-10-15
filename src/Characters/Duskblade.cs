using Godot;
using healerfantasy;
using healerfantasy.LastLight;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Duskblade — a fast melee damage dealer.
/// On combat start, dashes forward to melee range before attacking.
/// Attacks every <see cref="AttackInterval"/> seconds with Sinister Strike.
/// Lower damage per hit than the Dawnwarden, but attacks nearly twice as often.
/// </summary>
public partial class Duskblade : PartyMember
{
	public Duskblade()
	{
		MaxHealth = 100f;
	}

	/// <summary>Seconds between each Sinister Strike.</summary>
	[Export] public float AttackInterval = 1.2f;

	/// <summary>Movement speed in pixels per second while closing to melee range.</summary>
	[Export] public float MoveSpeed = 100f;

	/// <summary>
	/// Closest distance from the boss centre at which to stop and attack; wide bosses
	/// push it out to just beyond their body (see <see cref="ReachFor"/>).
	/// </summary>
	[Export] public float MeleeRange = 50f;

	float _attackTimer;
	DuskbladeSinisterStrikeSpell _sinisterStrike;
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
		// Stagger first attack so it doesn't sync with the Dawnwarden.
		_attackTimer = AttackInterval * 0.7f;
		_sinisterStrike = new DuskbladeSinisterStrikeSpell();
	}

	void OnAnimationFinished()
	{
		if (_visual.Animation == "attack")
			_visual.Play("idle");
	}

	/// <summary>How far inside <see cref="ReachFor"/> we tolerate before stepping back.</summary>
	const float BackOffSlack = 20f;

	/// <summary>
	/// How close to the boss's centre to stand: <see cref="MeleeRange"/>, or just
	/// outside a wide boss's body so the two don't overlap.
	/// </summary>
	float ReachFor(Character boss) =>
		Mathf.Max(MeleeRange, healerfantasy.LastLight.SilhouetteRig.BodyRadius(boss) + 25f);

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
			var reach = ReachFor(boss);
			if (dist > reach)
			{
				var direction = (boss.GlobalPosition - GlobalPosition).Normalized();
				Velocity = direction * MoveSpeed;
				MoveAndSlide();
				ClampToArenaBoundary();
				return; // Not in range yet — skip attack logic
			}
			if (StepAsideFromAllies(boss, MoveSpeed * 0.6f))
			{
				// Crowding another ally: StepAsideFromAllies has moved us sideways.
			}
			else if (dist < reach - BackOffSlack)
			{
				// Standing inside a big boss (e.g. it moved onto us): step back out,
				// still attacking. The slack keeps this from jittering at the edge.
				Velocity = (GlobalPosition - boss.GlobalPosition).Normalized() * MoveSpeed * 0.6f;
				MoveAndSlide();
				ClampToArenaBoundary();
			}
			else
			{
				// In range with room to stand — make sure we've fully stopped.
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