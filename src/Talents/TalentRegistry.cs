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
			Description = "After healing a target, the most-injured untreated party member is also healed for 6 HP.",
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
		},
		new()
		{
			Name = "Future Sight",
			Description = "Chronomancy healing spells are 20% more effective.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_24.png",
			School = SpellSchool.Chronomancy,
			TalentRow = 3,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new FutureSightTalent())
		},
		new()
		{
			Name = "Temporal Momentum",
			Description = "Casting a Chronomancy spell makes your next non-Chronomancy spell instant.",
			IconPath = AssetConstants.TalentIconAssets + "monk/Monk_25.png",
			School = SpellSchool.Chronomancy,
			TalentRow = 3,
			Configure = (t, _) =>
			{
				var temporalMomentum = new TemporalMomentumTalent();
				t.SpellModifiers.Add(temporalMomentum);
				t.CharacterModifiers.Add(temporalMomentum);
			}
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