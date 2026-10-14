using System.Collections.Generic;
using System.Linq;
using healerfantasy.SpellResources.Wick;
using healerfantasy.SpellResources.Generic;
using healerfantasy.SpellResources.Flame;
using healerfantasy.SpellResources.Ember;
using healerfantasy.SpellResources.Kindling;
using healerfantasy.SpellResources.Umbra;
using UltimateSpellResource = healerfantasy.SpellResources.Umbra.UltimateSpellResource;

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
		new BecomeTheSunSpell(),
		new OneWithTheAshSpell(),
		new EmbraceTheDarkSpell(),
		new TwinFlameSpell(),
		new SharedFlameSpell()
	];

	public static readonly List<SpellResource> UmbraSpells =
	[
		new UmbralBoltSpell(),
		new SiphonTheDarkSpell(),
		new TouchOfAffliction(),
		new RotSpell(),
		new SoulShatterSpell(),
		new SwallowingDarkSpell(),
		new EmbraceTheDarkSpell()
	];

	public static readonly List<SpellResource> WickSpells =
	[
		new UnburnSpell(),
		new BurnBrightSpell(),
		new RelightSpell(),
		new DripSpell(),
		new QuickenTheWickSpell(),
		new WaxSealSpell(),
		new TwinFlameSpell()
	];

	public static readonly List<SpellResource> EmberSpells =
	[
		new SmoulderSpell(),
		new SpreadingEmbersSpell(),
		new CinderSpell(),
		new AshVeilSpell(),
		new MothswarmSpell(),
		new StokeSpell(),
		new OneWithTheAshSpell()
	];

	public static readonly List<SpellResource> FlameSpells =
	[
		new FlareSpell(),
		new RekindleSpell(),
		new CandleTouchSpell(),
		new WildfireSpell(),
		new LanternWardSpell(),
		new FlashfireSpell(),
		new BecomeTheSunSpell()
	];

	public static readonly List<SpellResource> KindlingSpells =
	[
		new BurningPactSpell(),
		new LifeflameSpell(),
		new DrawTheHeatSpell(),
		new FeedThePyreSpell(),
		new NovaOfSacrifice(),
		new SharedFlameSpell()
	];

	public static readonly List<SpellResource> AllSpells = new List<SpellResource>()
		.Concat(UmbraSpells)
		.Concat(WickSpells)
		.Concat(EmberSpells)
		.Concat(FlameSpells)
		.Concat(KindlingSpells)
		.ToList();
}