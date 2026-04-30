using System.Collections.Generic;
using System.Linq;
using healerfantasy.SpellResources.Chronomancy;
using healerfantasy.SpellResources.Generic;

namespace healerfantasy.SpellResources;

public static class SpellRegistry
{
	/// <summary>
	/// Always-available generic spells (Dispel, Deflect).
	/// These are shown in the spellbook but cannot be added to or removed from
	/// the regular loadout — they live in their own action bar slots.
	/// </summary>
	public static readonly List<SpellResource> GenericSpells =
	[
		new DispelSpell(),
		new DeflectSpell()
	];

	public static readonly List<SpellResource> VoidSpells =
	[
		new DecaySpellResource(),
		new ShadowBoltSpell(),
		new VoidDrainSpell(),
		new SoulShatterSpell(),
		new DarkPactSpell()
	];

	public static readonly List<SpellResource> ChronomancySpells =
	[
		new RewindSpell(),
		new TimeWarpSpell(),
		new TimeLoopSpell(),
		new HasteSpell(),
		new TemporalWardSpell()
	];

	public static readonly List<SpellResource> NatureSpells =
	[
		new RenewingBloomSpell(),
		new WildGrowthSpell(),
		new PoisonBoltSpell(),
		new BarkskinSpell(),
		new BoonOfTheWildsSpell(),
		new NourishSpell()
	];

	public static readonly List<SpellResource> HolySpells =
	[
		new BurstOfLightSpell(),
		new ReinvigorateSpell(),
		new TouchOfLightSpell(),
		new WaveOfIncandescenceSpell(),
		new DivineAegisSpell(),
		new HolyNovaSpell()
	];

	public static readonly List<SpellResource> AllSpells = new List<SpellResource>()
		.Concat(VoidSpells)
		.Concat(ChronomancySpells)
		.Concat(NatureSpells)
		.Concat(HolySpells)
		.ToList();
}