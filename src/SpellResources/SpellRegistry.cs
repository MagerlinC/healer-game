using System.Collections.Generic;
using System.Linq;
using healerfantasy.SpellResources.Chronomancy;

namespace healerfantasy.SpellResources;

public static class SpellRegistry
{
	public static readonly List<SpellResource> VoidSpells =
	[
		new DecaySpellResource()
	];

	public static readonly List<SpellResource> ChronomancySpells =
	[
		new RewindSpell()
	];

	public static readonly List<SpellResource> NatureSpells =
	[
		new RenewingBloomSpell()

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