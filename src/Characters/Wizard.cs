using Godot;
using healerfantasy;
using healerfantasy.LastLight;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Wizard — a ranged spellcaster dealing damage from the back line.
/// Casts Arcane Blast every <see cref="CastInterval"/> seconds.
/// Slowest cadence of the three but highest damage per hit.
/// </summary>
public partial class Wizard : PartyMember
{

	/// <summary>Seconds between each Arcane Blast.</summary>
	[Export] public float CastInterval = 2.5f;

	float _castTimer;
	WizardArcaneBlastSpell _arcaneBlast;
	CharacterVisual _visual = null!;

	public Wizard()
	{
		MaxHealth = 100f;
	}

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
		// Stagger first cast so it doesn't overlap with the melee members.
		_castTimer = CastInterval * 0.6f;
		_arcaneBlast = new WizardArcaneBlastSpell();
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
		if (IsKnockedBack) return;

		_castTimer -= (float)delta;
		if (_castTimer <= 0f)
		{
			_castTimer = GetHasteAdjustedAttackInterval(CastInterval);
			CastArcaneBlast();
		}
	}

	void CastArcaneBlast()
	{
		var boss = FindPreferredBoss();
		if (boss == null) return;
		_visual.Play("attack");
		SpellPipeline.Cast(_arcaneBlast, this, boss);
	}

	protected override void ApplyDeathVisuals()
	{
		// The rig topples the figure and lets the darkness take it.
		_visual.Stop();
	}
}