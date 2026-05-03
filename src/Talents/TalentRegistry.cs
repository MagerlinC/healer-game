using System;
using System.Collections.Generic;
using System.Linq;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents.Chronomancy;
using healerfantasy.Talents.Holy;
using healerfantasy.Talents.Nature;
using healerfantasy.Talents.Void;

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
	public static readonly List<TalentDefinition> VoidTalents =
	[
		new()
		{
			Name = "Void Specialist",
			Description = "20% increased void damage.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_5.png",
			TalentRow = 0,
			School = SpellSchool.Void,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new VoidSpecialistTalent())
		},
		new()
		{
			Name = "Entropic Surge",
			Description = "Void spells have a 15% chance to deal double damage.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_9.png",
			TalentRow = 1,
			School = SpellSchool.Void,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new EntropicSurgeTalent())
		},
		new()
		{
			Name = "Siphoning Void",
			Description = "Dealing void damage restores 1 mana.",
			IconPath = AssetConstants.TalentIconAssets + "void/siphoning-void.png",
			TalentRow = 2,
			School = SpellSchool.Void,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new SiphoningVoidTalent())
		},
		new()
		{
			Name = "Necrotic Touch",
			Description = "Void damage spells have a 25% chance to apply a free 3-tick, 5-damage-per-tick DoT.",
			IconPath = AssetConstants.TalentIconAssets + "void/necrotic-touch.png",
			TalentRow = 3,
			School = SpellSchool.Void,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new NecroticTouchTalent())
		},
		new()
		{
			Name = "Void Resonance",
			Description = "Casting Shadow Bolt consumes all DoTs on the target, instantly dealing their remaining damage.",
			IconPath = AssetConstants.TalentIconAssets + "void/void-resonance.png",
			TalentRow = 3,
			School = SpellSchool.Void,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new VoidResonanceTalent())
		}
	];

	public static readonly List<TalentDefinition> HolyTalents =
	[
		new()
		{
			Name = "Shielding Reinvigoration",
			Description = "Healing a target grants a 5s shield equal to 20% of the healing done.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_17.png",
			School = SpellSchool.Holy,
			TalentRow = 0,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new ShieldingReinvigorationTalent { EffectIcon = icon })
		},
		new()
		{
			Name = "Sacred Ground",
			Description = "Healing spells restore 2 mana for each target healed.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_2.png",
			School = SpellSchool.Holy,
			TalentRow = 1,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new SacredGroundTalent())
		},
		new()
		{
			Name = "Sacred Momentum",
			Description = "Holy spells are 15% more effective when the target is below 50% health.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_3.png",
			School = SpellSchool.Holy,
			TalentRow = 2,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new SacredMomentumTalent())
		},
		new()
		{
			Name = "Radiant Infusion",
			Description = "Healing spells grant a 6s buff that increases the target's damage by 15%.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_15.png",
			School = SpellSchool.Holy,
			TalentRow = 3,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new RadiantInfusionTalent { EffectIcon = icon })
		},
		new()
		{
			Name = "Illumination",
			Description = "Healing critical strikes refund 50% of the spell's mana cost.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_16.png",
			School = SpellSchool.Holy,
			TalentRow = 3,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new IlluminationTalent())
		}
	];

	public static readonly List<TalentDefinition> NatureTalents =
	[
		new()
		{
			Name = "Verdant Strength",
			Description = "Nature healing spells are 20% more effective.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_6.png",
			School = SpellSchool.Nature,
			TalentRow = 0,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new VerdantStrengthTalent())
		},
		new()
		{
			Name = "Toxic Potency",
			Description = "Nature damage spells deal 20% more damage.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_7.png",
			School = SpellSchool.Nature,
			TalentRow = 1,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new ToxicPotencyTalent())
		},
		new()
		{
			Name = "Flourishing",
			Description =
				"Casting a healing spell on a target causes the most-injured party member to be healed for 8 HP.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_8.png",
			School = SpellSchool.Nature,
			TalentRow = 2,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new FlourishingTalent())
		},
		new()
		{
			Name = "Deep Roots",
			Description = "Casting a HoT on a target that already has one active restores 4 mana.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_18.png",
			School = SpellSchool.Nature,
			TalentRow = 3,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new DeepRootsTalent())
		},
		new()
		{
			Name = "Overgrowth",
			Description = "HoT spells tick for 20% more on targets above 75% health.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_19.png",
			School = SpellSchool.Nature,
			TalentRow = 3,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new OvergrowthTalent())
		}
	];

	public static readonly List<TalentDefinition> ChronomancyTalents =
	[
		new()
		{
			Name = "Acceleration",
			Description =
				"Casting a chronomancy spell increases haste for 10%. Stacks up to 3 times.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_17.png",
			School = SpellSchool.Chronomancy,
			TalentRow = 0,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new AccelerationTalent { EffectIcon = icon })
		},
		new()
		{
			Name = "Temporal Flow",
			Description = "Passively increases haste by 15%.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_12.png",
			School = SpellSchool.Chronomancy,
			TalentRow = 1,
			Configure = (t, _) =>
				t.CharacterModifiers.Add(new TemporalFlowTalent())
		},
		new()
		{
			Name = "Mana Rift",
			Description = "Chronomancy spells have a 20% chance to refund their full mana cost.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_14.png",
			School = SpellSchool.Chronomancy,
			TalentRow = 2,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new ManaRiftTalent())
		},
		new()
		{
			Name = "Future Sight",
			Description =
				"Casting a Chronomancy spell makes your next non-Chronomancy spell always crit.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_24.png",
			School = SpellSchool.Chronomancy,
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
			Description = "Casting a Chronomancy spell makes your next non-Chronomancy spell instant.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_25.png",
			School = SpellSchool.Chronomancy,
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
				"Critical hits restore 10 mana.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_21.png",
			School = SpellSchool.Generic,
			TalentRow = 0,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new CriticalRechargeTalent())
		},
		new()
		{
			Name = "Arcane Hunger",
			Description =
				"When casting a spell, spend up to 10% of maximum mana to increase its damage or healing by 1% per 1 mana spent (maximum of 50%).",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_13.png",
			School = SpellSchool.Generic,
			TalentRow = 1,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new ArcaneHungerTalent())
		},
		new()
		{
			Name = "Critical Infusion",
			Description = "Critical hits grant a 10s buff amplifying the damage and healing of the next spell by 30%.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_23.png",
			School = SpellSchool.Generic,
			TalentRow = 1,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new CriticalInfusionTalent { EffectIcon = icon })
		}
	];

	public static readonly List<TalentDefinition> AllTalents =
		GenericTalents.Concat(VoidTalents).Concat(HolyTalents).Concat(NatureTalents).Concat(ChronomancyTalents).ToList();

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
						w *= 1.5;
				}

				weights[i] = w;
				totalWeight += w;
			}

			// Weighted random draw.
			var roll = rng.NextDouble() * totalWeight;
			var cumulative = 0.0;
			var chosenIdx = pool.Count - 1;   // fallback: last element

			for (var i = 0; i < pool.Count; i++)
			{
				cumulative += weights[i];
				if (roll <= cumulative) { chosenIdx = i; break; }
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