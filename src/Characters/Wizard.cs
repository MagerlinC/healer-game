using Godot;
using healerfantasy;
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
    [Export] public float CastInterval = 3.0f;

    float _castTimer;
    WizardArcaneBlastSpell _arcaneBlast;

    public override void _Ready()
    {
        base._Ready();
        // Stagger first cast so it doesn't overlap with the melee members.
        _castTimer  = CastInterval * 0.6f;
        _arcaneBlast = new WizardArcaneBlastSpell();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!IsAlive) return;

        _castTimer -= (float)delta;
        if (_castTimer <= 0f)
        {
            _castTimer = CastInterval;
            CastArcaneBlast();
        }
    }

    void CastArcaneBlast()
    {
        var boss = FindPreferredBoss();
        if (boss == null) return;
        SpellPipeline.Cast(_arcaneBlast, this, boss);
    }
}
