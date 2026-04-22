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
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_10.png",
			TalentRow = 2,
			School = SpellSchool.Void,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new SiphoningVoidTalent())
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
			Description = "After healing a target, the most-injured untreated party member is also healed for 6 HP.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_8.png",
			School = SpellSchool.Nature,
			TalentRow = 2,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new FlourishingTalent())
		}
	];

	public static readonly List<TalentDefinition> ChronomancyTalents =
	[
		new()
		{
			Name = "Acceleration",
			Description =
				"Casting a chronomancy spell increases cast speed for 10%. Stacks up to 3 times.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_17.png",
			School = SpellSchool.Chronomancy,
			TalentRow = 0,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new AccelerationTalent { EffectIcon = icon })
		},
		new()
		{
			Name = "Temporal Flow",
			Description = "Passively increases cast speed by 15%.",
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
		}
	];

	public static readonly List<TalentDefinition> GenericTalents =
	[
		new()
		{
			Name = "Critical Amplifier",
			Description = "+20% base critical strike chance on all spells.",
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
				"Critical hits restore 20 mana.",
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


}