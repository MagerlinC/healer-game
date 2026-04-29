using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Queen of the Frozen Wastes — Ice Block.
///
/// An instant-cast spell (CastTime = 0) that triggers the Queen's signature
/// defensive mechanic:
///
///   1. The Queen's sprite changes to the ice-block visual.
///   2. She gains a <see cref="IceBlockShield"/> absorb shield.
///   3. She begins an 8-second cast of Absolute Zero — a wipe-level nuke —
///      shown on the BossCastBar via CastWindupStarted.
///   4. If the shield is destroyed before the 8 seconds elapse, Ice Block
///      shatters: Absolute Zero is cancelled and the Queen resumes normal attacks.
///   5. If Absolute Zero completes, every living party member takes 1 000 damage.
///
/// All state management lives in <see cref="QueenOfTheFrozenWastes"/> — this
/// spell's Apply simply calls into the boss to start the mechanic.
/// </summary>
[GlobalClass]
public partial class BossQueenIceBlockSpell : SpellResource
{
	/// <summary>Absorb shield applied when Ice Block activates.</summary>
	public float IceBlockShield = 1000f;

	/// <summary>Reference to the Queen — must be set before SpellPipeline.Cast is called.</summary>
	public Character Boss { get; set; }

	public BossQueenIceBlockSpell()
	{
		Name = "Ice Block";
		Description = "The Queen encases herself in enchanted ice, absorbing 1 000 damage. " +
		              "While encased she channels Absolute Zero — destroy the ice before it " +
		              "completes or the entire party is obliterated.";
		Tags = SpellTags.None;
		ManaCost = 0f;
		CastTime = 0f;
		EffectType = EffectType.Harmful;
	}

	public override void Apply(SpellContext ctx)
	{
		if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive)
		{
			GD.PrintErr("[IceBlock] Apply called with null/dead Boss — aborting.");
			return;
		}

		(Boss as QueenOfTheFrozenWastes)?.StartIceBlock(IceBlockShield);
	}
}
