using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.LastLight;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// One of the tendrils that rise when The Abyss Opens (at half health). While any are
/// alive the Abyss itself can't be harmed; each one lashes a random party member until
/// it is cut down. Stays in the boss group so the party focuses it automatically.
/// </summary>
public partial class AbyssalTendril : EnemyCharacter
{
	static readonly Dictionary<string, CharacterVisual.Clip> Clips = new()
	{
		["idle"] = new(1.2f, Loop: true),
		["attack"] = new(0.6f)
	};

	[Export] public float LashInterval = 3f;

	readonly BossAbyssTendrilLashSpell _lash = new();
	CharacterVisual _visual = null!;
	Character _pendingTarget;
	float _lashTimer;

	public AbyssalTendril(string instanceName, float maxHealth, float lashDamage)
	{
		CharacterName = instanceName;
		MaxHealth = maxHealth;
		_lash.DamageAmount = lashDamage;
	}

	public override void _Ready()
	{
		base._Ready();
		_visual = CharacterVisual.Attach(this, Clips);
		_visual.AnimationFinished += OnAnimationFinished;
		// Staggered, so three tendrils don't lash in unison.
		_lashTimer = LashInterval * (0.4f + 0.6f * GD.Randf());
		ApplyRuneModifiers();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive || _pendingTarget != null) return;

		_lashTimer -= (float)delta;
		if (_lashTimer > 0f) return;
		_lashTimer = LashInterval;
		_pendingTarget = PickRandomPartyMember();
		if (_pendingTarget != null) _visual.Play("attack");
	}

	void OnAnimationFinished()
	{
		if (_pendingTarget is { IsAlive: true } && IsAlive)
			SpellPipeline.Cast(_lash, this, _pendingTarget);
		_pendingTarget = null;
		if (IsAlive) _visual.Play("idle");
	}

	protected override void OnApplyHasteRune()
	{
		LashInterval /= GameConstants.RuneMidnightHasteMultiplier;
	}
}
