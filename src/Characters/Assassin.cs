using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

/// <summary>
/// The Assassin — a fast melee damage dealer.
/// Attacks every <see cref="AttackInterval"/> seconds with Sinister Strike.
/// Lower damage per hit than the Templar, but attacks nearly twice as often.
/// </summary>
public partial class Assassin : PartyMember
{
    /// <summary>Seconds between each Sinister Strike.</summary>
    [Export] public float AttackInterval = 1.5f;

    float _attackTimer;
    AssassinSinisterStrikeSpell _sinisterStrike;

    public override void _Ready()
    {
        base._Ready();
        // Stagger first attack so it doesn't sync with the Templar.
        _attackTimer   = AttackInterval * 0.7f;
        _sinisterStrike = new AssassinSinisterStrikeSpell();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!IsAlive) return;

        _attackTimer -= (float)delta;
        if (_attackTimer <= 0f)
        {
            _attackTimer = AttackInterval;
            PerformSinisterStrike();
        }
    }

    void PerformSinisterStrike()
    {
        var boss = FindBoss();
        if (boss == null) return;
        SpellPipeline.Cast(_sinisterStrike, this, boss);
    }

    Character FindBoss()
    {
        foreach (var node in GetTree().GetNodesInGroup(GameConstants.BossGroupName))
            if (node is Character c && c.IsAlive)
                return c;
        return null;
    }
}
