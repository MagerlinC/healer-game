using System.Collections.Generic;
using System.Linq;
using healerfantasy.SpellResources.Chronomancy;
using healerfantasy.SpellResources.Generic;
using healerfantasy.SpellResources.Holy;
using healerfantasy.SpellResources.Nature;
using healerfantasy.SpellResources.Sanguimancy;
using healerfantasy.SpellResources.Void;
using UltimateSpellResource = healerfantasy.SpellResources.Void.UltimateSpellResource;

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

	/// <summary>
	/// All available ultimate spells, one per school. These occupy the dedicated
	/// ultimate slot (key R) rather than the regular 6-slot loadout.
	/// </summary>
	public static readonly List<UltimateSpellResource> UltimateSpells =
	[
		new ArchangelOfLightSpell(),
		new OneWithNatureSpell(),
		new VoidsEmbraceSpell(),
		new MirrorImage(),
		new VampiricEmbrace()
	];

	public static readonly List<SpellResource> VoidSpells =
	[
		new ShadowBoltSpell(),
		new VoidDrainSpell(),
		new TouchOfAffliction(),
		new DecaySpellResource(),
		new SoulShatterSpell(),
		new ConsumingVoid(),
		new VoidsEmbraceSpell()
	];

	public static readonly List<SpellResource> ChronomancySpells =
	[
		new RewindSpell(),
		new TimeWarpSpell(),
		new TimeLoopSpell(),
		new ManaLoopSpell(),
		new HasteSpell(),
		new TemporalWardSpell(),
		new MirrorImage()
	];

	public static readonly List<SpellResource> NatureSpells =
	[
		new RenewingBloomSpell(),
		new WildGrowthSpell(),
		new PoisonBoltSpell(),
		new LeafShadeSpell(),
		new SwarmOfTheForest(),
		new NourishSpell(),
		new OneWithNatureSpell()
	];

	public static readonly List<SpellResource> HolySpells =
	[
		new BurstOfLightSpell(),
		new ReinvigorateSpell(),
		new TouchOfLightSpell(),
		new WaveOfIncandescenceSpell(),
		new DivineAegisSpell(),
		new HolyNovaSpell(),
		new ArchangelOfLightSpell()
	];

	public static readonly List<SpellResource> SanguimancySpells =
	[
		new SanguinePact(),
		new VitalSurgeSpell(),
		new SanguineDrainSpell(),
		new ExsanguinateSpell(),
		new NovaOfSacrifice(),
		new VampiricEmbrace()
	];

	public static readonly List<SpellResource> AllSpells = new List<SpellResource>()
		.Concat(VoidSpells)
		.Concat(ChronomancySpells)
		.Concat(NatureSpells)
		.Concat(HolySpells)
		.Concat(SanguimancySpells)
		.ToList();
}