using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Templar — a defensive frontliner who keeps aggro on the boss.
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

	float _attackTimer;
	TemplarShieldBashSpell _shieldBash;

	public override void _Ready()
	{
		base._Ready();
		// Stagger the first attack slightly so all three members don't hit at t=0.
		_attackTimer = AttackInterval * 0.4f;
		_shieldBash = new TemplarShieldBashSpell();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		if (!IsAlive) return;

		_attackTimer -= (float)delta;
		if (_attackTimer <= 0f)
		{
			_attackTimer = AttackInterval;
			PerformShieldBash();
		}
	}

	void PerformShieldBash()
	{
		var boss = FindBoss();
		if (boss == null) return;
		SpellPipeline.Cast(_shieldBash, this, boss);
	}

	Character FindBoss()
	{
		foreach (var node in GetTree().GetNodesInGroup(GameConstants.BossGroupName))
			if (node is Character c && c.IsAlive)
				return c;
		return null;
	}
}