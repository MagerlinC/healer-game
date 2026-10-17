using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Tallow Queen — Wax Shell.
///
/// An instant-cast spell (CastTime = 0) that triggers the Queen's signature
/// defensive mechanic:
///
///   1. The Queen's sprite changes to the wax-shell visual.
///   2. She gains a <see cref="WaxShellShield"/> absorb shield.
///   3. She begins an 8-second cast of Snuff Out — a wipe-level nuke —
///      shown on the BossCastBar via CastWindupStarted.
///   4. If the shield is destroyed before the 8 seconds elapse, Wax Shell
///      cracks open: Snuff Out is cancelled and the Queen resumes normal attacks.
///   5. If Snuff Out completes, every living party member takes 1 000 damage.
///
/// All state management lives in <see cref="TheTallowQueen"/> — this
/// spell's Apply simply calls into the boss to start the mechanic.
/// </summary>
[GlobalClass]
public partial class BossQueenWaxShellSpell : SpellResource
{
	/// <summary>Absorb shield applied when Wax Shell activates.</summary>
	public float WaxShellShield = 1000f;

	/// <summary>Reference to the Queen — must be set before SpellPipeline.Cast is called.</summary>
	public Character Boss { get; set; }

	public BossQueenWaxShellSpell()
	{
		Name = "Wax Shell";
		Description = "The Queen seals herself in a shell of hardened wax, absorbing 1 000 damage. " +
		              "While sealed she channels Snuff Out — crack the shell before it " +
		              "completes or every flame in the party is put out.";
		Tags = SpellTags.None;
		ManaCost = 0f;
		CastTime = 0f;
		TargetingType = TargetingType.Enemy;
	}

	public override void Apply(SpellContext ctx)
	{
		if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive)
		{
			GD.PrintErr("[WaxShell] Apply called with null/dead Boss — aborting.");
			return;
		}

		(Boss as TheTallowQueen)?.StartWaxShell(WaxShellShield);
	}
}