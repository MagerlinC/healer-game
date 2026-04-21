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
		new VoidDrainSpell()
	];

	public static readonly List<SpellResource> ChronomancySpells =
	[
		new RewindSpell(),
		new TimeWarpSpell(),
		new TimeLoopSpell()
	];

	public static readonly List<SpellResource> NatureSpells =
	[
		new RenewingBloomSpell(),
		new WildGrowthSpell(),
		new PoisonBoltSpell()
	];

	public static readonly List<SpellResource> HolySpells =
	[
		new BurstOfLightSpell(),
		new ReinvigorateSpell(),
		new TouchOfLightSpell(),
		new WaveOfIncandescenceSpell()
	];

	public static readonly List<SpellResource> AllSpells = new List<SpellResource>()
		.Concat(VoidSpells)
		.Concat(ChronomancySpells)
		.Concat(NatureSpells)
		.Concat(HolySpells)
		.ToList();
}