using System.Collections.Generic;
using System.Linq;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents.Chronomancy;

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
			IconPath = "res://assets/talent-icons/monk/Monk_5.png",
			TalentRow = 0,
			School = SpellSchool.Void,
			Configure = (t, _) =>
				t.SpellModifiers.Add(new VoidSpecialistTalent())
		}
	];

	public static readonly List<TalentDefinition> HolyTalents =
	[
		new()
		{
			Name = "Shielding Reinvigoration",
			Description = "Healing a target grants a 5s shield equal to 20% of the healing done.",
			IconPath = "res://assets/talent-icons/monk/Monk_17.png",
			School = SpellSchool.Holy,
			TalentRow = 0,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new ShieldingReinvigorationTalent { EffectIcon = icon })
		}
	];

	public static readonly List<TalentDefinition> NatureTalents = [];

	public static readonly List<TalentDefinition> ChronomancyTalents =
	[
		new()
		{
			Name = "Acceleration",
			Description =
				"Casting a chronomancy spell increases cast speed for 10%. Stacks up to 3 times.",
			IconPath = "res://assets/talent-icons/monk/Monk_17.png",
			School = SpellSchool.Chronomancy,
			TalentRow = 0,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new AccelerationTalent { EffectIcon = icon })
		}
	];

	public static readonly List<TalentDefinition> GenericTalents =
	[
		new()
		{
			Name = "Critical Amplifier",
			Description = "+20% base critical strike chance on all spells.",
			IconPath = "res://assets/talent-icons/monk/Monk_11.png",
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
			IconPath = "res://assets/talent-icons/monk/Monk_21.png",
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
			IconPath = "res://assets/talent-icons/monk/Monk_13.png",
			School = SpellSchool.Generic,
			TalentRow = 1,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new ArcaneHungerTalent())
		},
		new()
		{
			Name = "Critical Infusion",
			Description = "Critical hits grant a 10s buff amplifying the damage and healing of the next spell by 30%.",
			IconPath = "res://assets/talent-icons/monk/Monk_23.png",
			School = SpellSchool.Generic,
			TalentRow = 1,
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new CriticalInfusionTalent { EffectIcon = icon })
		}
	];

	public static readonly List<TalentDefinition> AllTalents =
		GenericTalents.Concat(VoidTalents).Concat(HolyTalents).Concat(NatureTalents).Concat(ChronomancyTalents).ToList();


}