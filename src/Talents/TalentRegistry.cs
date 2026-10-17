using System;
using System.Collections.Generic;
using System.Linq;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents.Wick;
using healerfantasy.Talents.Generic;
using healerfantasy.Talents.Flame;
using healerfantasy.Talents.Ember;
using healerfantasy.Talents.Pyre;
using healerfantasy.Talents.Umbra;

namespace healerfantasy.Talents;

/// <summary>
/// Central list of every talent available in the game.
/// Add new <see cref="TalentDefinition"/> entries here to expose them in
/// the talent selector UI — no other file needs changing.
///
/// Each entry defines Name, Description, and IconPath exactly once.
/// <see cref="TalentDefinition.CreateTalent"/> forwards them to the built
/// <see cref="Talent"/> automatically; <see cref="TalentDefinition.Configure"/>
/// only needs to wire up the modifiers.
/// </summary>
public static class TalentRegistry
{
	public static readonly List<TalentDefinition> UmbraTalents =
	[
		new()
		{
			Name = "Umbral Specialist",
			Description = "20% increased umbral damage.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_5.png",
			TalentRow = 0,
			School = SpellSchool.Umbra,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new UmbralSpecialistTalent())
		},
		new()
		{
			Name = "Entropic Surge",
			Description = "Umbra spells have a 15% chance to deal double damage.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_9.png",
			TalentRow = 1,
			School = SpellSchool.Umbra,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new EntropicSurgeTalent())
		},
		new()
		{
			Name = "Siphoning Dark",
			Description = "Dealing umbral damage restores 1 kindling.",
			IconPath = AssetConstants.TalentIconAssets + "void/siphoning-void.png",
			TalentRow = 2,
			School = SpellSchool.Umbra,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new SiphoningDarkTalent())
		},
		new()
		{
			Name = "Necrotic Touch",
			Description = "Umbral damage spells have a 25% chance to apply a free 3-tick, 5-damage-per-tick DoT.",
			IconPath = AssetConstants.TalentIconAssets + "void/necrotic-touch.png",
			TalentRow = 3,
			School = SpellSchool.Umbra,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new NecroticTouchTalent())
		},
		new()
		{
			Name = "Umbral Resonance",
			Description = "Casting Umbral Bolt consumes all DoTs on the target, instantly dealing their remaining damage.",
			IconPath = AssetConstants.TalentIconAssets + "void/void-resonance.png",
			TalentRow = 3,
			School = SpellSchool.Umbra,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new UmbralResonanceTalent())
		}
	];

	public static readonly List<TalentDefinition> FlameTalents =
	[
		new()
		{
			Name = "Shielding Rekindle",
			Description = "Restoring light to a target grants a 5s shield equal to 20% of the light restored.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_17.png",
			School = SpellSchool.Flame,
			TalentRow = 0,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new ShieldingRekindleTalent { EffectIcon = icon })
		},
		new()
		{
			Name = "Sacred Ground",
			Description = "Restoring spells return 2 kindling for each target they reach.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_2.png",
			School = SpellSchool.Flame,
			TalentRow = 1,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new SacredGroundTalent())
		},
		new()
		{
			Name = "Sacred Momentum",
			Description = "Flame spells are 15% more effective when the target is below 50% light.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_3.png",
			School = SpellSchool.Flame,
			TalentRow = 2,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new SacredMomentumTalent())
		},
		new()
		{
			Name = "Radiant Infusion",
			Description = "Restoring spells grant a 6s buff that increases the target's damage by 15%.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_15.png",
			School = SpellSchool.Flame,
			TalentRow = 3,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new RadiantInfusionTalent { EffectIcon = icon })
		},
		new()
		{
			Name = "Illumination",
			Description = "Critical strikes that restore light refund 50% of the spell's kindling cost.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_16.png",
			School = SpellSchool.Flame,
			TalentRow = 3,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new IlluminationTalent())
		}
	];

	public static readonly List<TalentDefinition> EmberTalents =
	[
		new()
		{
			Name = "Banked Embers",
			Description = "Ember restoring spells are 20% more effective.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_6.png",
			School = SpellSchool.Ember,
			TalentRow = 0,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new BankedEmbersTalent())
		},
		new()
		{
			Name = "Searing Cinders",
			Description = "Ember damage spells deal 20% more damage.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_7.png",
			School = SpellSchool.Ember,
			TalentRow = 1,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new SearingCindersTalent())
		},
		new()
		{
			Name = "Spreading Warmth",
			Description =
				"Casting a restoring spell on a target also restores 8 light to the party member with the least light.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_8.png",
			School = SpellSchool.Ember,
			TalentRow = 2,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new SpreadingWarmthTalent())
		},
		new()
		{
			Name = "Deep Coals",
			Description = "Casting lingering light on a target that already has lingering light active returns 4 kindling.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_18.png",
			School = SpellSchool.Ember,
			TalentRow = 3,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new DeepCoalsTalent())
		},
		new()
		{
			Name = "Overburn",
			Description = "Lingering light ticks for 20% more on targets above 75% light.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_19.png",
			School = SpellSchool.Ember,
			TalentRow = 3,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new OverburnTalent())
		}
	];

	public static readonly List<TalentDefinition> WickTalents =
	[
		new()
		{
			Name = "Acceleration",
			Description =
				"Casting a Wick spell increases haste for 10%. Stacks up to 3 times.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_15.png",
			School = SpellSchool.Wick,
			TalentRow = 0,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new AccelerationTalent { EffectIcon = icon })
		},
		new()
		{
			Name = "Temporal Flow",
			Description = "Passively increases haste by 15%.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_12.png",
			School = SpellSchool.Wick,
			TalentRow = 1,
			Configure = (t, _) =>
				t.CharacterModifiers.Add(new TemporalFlowTalent())
		},
		new()
		{
			Name = "Kindling Rift",
			Description = "Wick spells have a 20% chance to refund their full kindling cost.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_14.png",
			School = SpellSchool.Wick,
			TalentRow = 2,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new ManaRiftTalent())
		},
		new()
		{
			Name = "Future Sight",
			Description =
				"Casting a Wick spell makes your next non-Wick spell always crit.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_24.png",
			School = SpellSchool.Wick,
			TalentRow = 3,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new FutureSightTalent
				{
					EffectIcon = icon
				})
		},
		new()
		{
			Name = "Temporal Momentum",
			Description = "Casting a Wick spell makes your next non-Wick spell instant.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_25.png",
			School = SpellSchool.Wick,
			TalentRow = 3,
			Configure = (t, icon) =>
			{
				var temporalMomentum = new TemporalMomentumTalent
				{
					EffectIcon = icon
				};
				t.SpellModifiers.Add(temporalMomentum);
			}
		}
	];

	public static readonly List<TalentDefinition> GenericTalents =
	[
		new()
		{
			Name = "Critical Amplifier",
			Description = "+10% base critical strike chance on all spells.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_11.png",
			School = SpellSchool.Generic,
			TalentRow = 0,
			Configure = (t, _) =>
				t.CharacterModifiers.Add(new CriticalAmplifierTalent())
		},
		new()
		{
			Name = "Critical Recharge",
			Description =
				"Critical hits restore 10 kindling.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_21.png",
			School = SpellSchool.Generic,
			TalentRow = 0,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new CriticalRechargeTalent())
		},
		new()
		{
			Name = "Gathered Kindling",
			Description =
				"Gather 2 additional kindling per second.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_29.png",
			School = SpellSchool.Generic,
			TalentRow = 0,
			Configure = (t, icon) =>
				t.CharacterModifiers.Add(new ManaRecoveryTalent())
		},
		new()
		{
			Name = "Arcane Hunger",
			Description =
				"When casting a spell, spend up to 10% of maximum kindling to increase its damage or light restored by 1% per 1 kindling spent (maximum of 50%).",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_13.png",
			School = SpellSchool.Generic,
			TalentRow = 1,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new ArcaneHungerTalent())
		},
		new()
		{
			Name = "Critical Infusion",
			Description = "Critical hits grant a 10s buff amplifying the next spell's damage and light restored by 30%.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_23.png",
			School = SpellSchool.Generic,
			TalentRow = 1,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new CriticalInfusionTalent { EffectIcon = icon })
		}
	];

	public static readonly List<TalentDefinition> PyreTalents =
	[
		new()
		{
			Name = "Crimson Rebound",
			Description = "After casting a Pyre spell, recover 40% of the light it burned over 5 seconds.",
			IconPath = AssetConstants.TalentIconAssets + "sanguimancy/crimson-rebound.png",
			School = SpellSchool.Pyre,
			TalentRow = 0,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new CrimsonReboundTalent { EffectIcon = icon })
		},
		new()
		{
			Name = "Bloodthirst",
			Description = "Pyre spells deal 25% more damage and restore 25% more light.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_9.png",
			School = SpellSchool.Pyre,
			TalentRow = 1,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new BloodthirstTalent())
		},
		new()
		{
			Name = "Pyre Ward",
			Description =
				"When you burn your own light on a Pyre spell, all living allies gain a shield equal to the light burned for 10 seconds.",
			IconPath = AssetConstants.TalentIconAssets + "sanguimancy/sanguine-ward.png",
			School = SpellSchool.Pyre,
			TalentRow = 2,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new PyreWardTalent { EffectIcon = icon })
		},
		new()
		{
			Name = "Desperate Blaze",
			Description = "Pyre spells are up to 40% more effective the less light you have.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_20.png",
			School = SpellSchool.Pyre,
			TalentRow = 3,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new DesperateBlazeTalent())
		},
		new()
		{
			Name = "Phoenix Heart",
			Description = "Casting a Pyre spell that leaves you below 40% light restores the light it burned.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_26.png",
			School = SpellSchool.Pyre,
			TalentRow = 3,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new PhoenixHeartTalent())
		}
	];

	public static readonly List<TalentDefinition> AllTalents =
		GenericTalents.Concat(UmbraTalents).Concat(FlameTalents).Concat(EmberTalents).Concat(WickTalents)
			.Concat(PyreTalents).ToList();

	/// <summary>
	/// Returns up to <paramref name="count"/> random talent offers for the victory screen.
	///
	/// Eligibility rules:
	///   • Talent not already in <paramref name="acquired"/>.
	///   • Row-0 talents are always eligible.
	///   • Row-N talents require at least one acquired talent in row N−1 of the same school.
	///
	/// Weight modifiers (each applied to at most ONE offer slot per school):
	///   • Acquired-school bonus  — for school X with N acquired talents, the first
	///     offer picked from school X has its base weight multiplied by (1 + N × 0.1).
	///   • Affinity bonus         — the first offer picked from the <paramref name="affinity"/>
	///     school gets an additional ×1.5 multiplier (stacks with the above).
	///
	/// The "one slot per school" cap ensures players are nudged toward their
	/// preferred schools without being locked into them.
	///
	/// If fewer eligible talents exist than requested, the full eligible set is returned.
	/// </summary>
	public static List<TalentDefinition> GetRandomOffers(
		IEnumerable<TalentDefinition> acquired,
		SpellSchool? affinity = null,
		int count = 3)
	{
		var acquiredList = acquired.ToList();
		var acquiredNames = new HashSet<string>(acquiredList.Select(d => d.Name));

		// Count how many talents the player has acquired per school.
		var schoolCounts = acquiredList
			.GroupBy(d => d.School)
			.ToDictionary(g => g.Key, g => g.Count());

		var eligible = AllTalents.Where(t =>
		{
			if (acquiredNames.Contains(t.Name)) return false;
			// Pyre requires the Castle of Blood, Wick the Tallow Queen, to have been defeated in a previous run.
			if (!PlayerProgressStore.IsSchoolUnlocked(t.School))
				return false;
			if (t.TalentRow == 0) return true;
			// Requires at least one talent from the previous row in the same school.
			return AllTalents.Any(other =>
				other.School == t.School &&
				other.TalentRow == t.TalentRow - 1 &&
				acquiredNames.Contains(other.Name));
		}).ToList();

		if (eligible.Count == 0) return new List<TalentDefinition>();

		var rng = new Random();
		var pool = eligible.ToList();
		var result = new List<TalentDefinition>();

		// Track which schools have already had their one-slot weight boost applied
		// to a picked talent.  Once consumed the remaining offers from that school
		// use only the base Weight.
		var boostConsumed = new HashSet<SpellSchool>();

		while (result.Count < count && pool.Count > 0)
		{
			// Compute the effective weight for every remaining candidate.
			var weights = new double[pool.Count];
			var totalWeight = 0.0;

			for (var i = 0; i < pool.Count; i++)
			{
				var t = pool[i];
				var w = (double)t.Weight;

				if (!boostConsumed.Contains(t.School))
				{
					// Acquired-school bonus: +10% per acquired talent in this school.
					schoolCounts.TryGetValue(t.School, out var n);
					w *= 1.0 + n * 0.1;

					// Affinity bonus: ×1.5 if this is the player's preferred school.
					if (affinity.HasValue && t.School == affinity.Value)
						w *= 2;
				}

				weights[i] = w;
				totalWeight += w;
			}

			// Weighted random draw.
			var roll = rng.NextDouble() * totalWeight;
			var cumulative = 0.0;
			var chosenIdx = pool.Count - 1; // fallback: last element

			for (var i = 0; i < pool.Count; i++)
			{
				cumulative += weights[i];
				if (roll <= cumulative)
				{
					chosenIdx = i;
					break;
				}
			}

			var chosen = pool[chosenIdx];
			result.Add(chosen);
			pool.RemoveAt(chosenIdx);

			// Consume the weight boost for this school so it only applies once.
			boostConsumed.Add(chosen.School);
		}

		return result;
	}
}