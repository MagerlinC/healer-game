using healerfantasy.SpellSystem;

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
	public static readonly TalentDefinition[] All =
	{
		new()
		{
			Name = "Elemental Specialist",
			Description = "+20% damage to Fire, Cold & Lightning spells.",
			IconPath = "res://assets/talent-icons/monk/Monk_5.png",
			Configure = (t, _) =>
				t.SpellModifiers.Add(new ElementalSpecialistTalent())
		},

		new()
		{
			Name = "Critical Amplifier",
			Description = "+20% critical strike chance on all spells.",
			IconPath = "res://assets/talent-icons/monk/Monk_11.png",
			Configure = (t, _) =>
				t.CharacterModifiers.Add(new CriticalAmplifierTalent())
		},

		new()
		{
			Name = "Shielding Reinvigoration",
			Description = "Healing a target grants a 5s shield equal to 20% of the healing done.",
			IconPath = "res://assets/talent-icons/monk/Monk_17.png",
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new ShieldingReinvigorationTalent { EffectIcon = icon })
		},

		new()
		{
			Name = "Critical Infusion",
			Description = "Critical hits grant a 10s buff amplifying the damage and healing of the next spell by 30%.",
			IconPath = "res://assets/talent-icons/monk/Monk_23.png",
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new CriticalInfusionTalent { EffectIcon = icon })
		},
		new()
		{
			Name = "Arcane Hunger",
			Description =
				"When casting a spell, spend up to 10% of maximum mana to increase its damage or healing by 1% per 1 mana spent (maximum of 50%).",
			IconPath = "res://assets/talent-icons/monk/Monk_13.png",
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new ArcaneHungerTalent())
		},
		new()
		{
			Name = "Critical Recharge",
			Description =
				"Critical hits restore 20 mana.",
			IconPath = "res://assets/talent-icons/monk/Monk_21.png",
			Configure = (t, icon) =>
				t.SpellModifiers.Add(new CriticalRechargeTalent())
		}

	};
}