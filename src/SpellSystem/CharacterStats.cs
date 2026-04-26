using System.Collections.Generic;
using healerfantasy.SpellResources;

namespace healerfantasy.SpellSystem;

/// <summary>
/// A snapshot of a character's computed stats at the moment a spell is cast.
/// Created fresh each cast by aggregating the character's base values with all
/// active <see cref="ICharacterModifier"/>s from their talents.
/// </summary>
public class CharacterStats
{
	public float MaxHealth { get; set; }
	public float MaxMana { get; set; }

	/// <summary>0.0 – 1.0. E.g. 0.2 = 20% crit chance.</summary>
	public float CritChance { get; set; }

	/// <summary>How much FinalValue is multiplied on a crit. Default 1.5×.</summary>
	public float CritMultiplier { get; set; } = 1.5f;

	/// <summary>Multiplier applied to all damage spells before modifier pipeline runs. Default 1.0.</summary>
	public float DamageMultiplier { get; set; } = 1.0f;

	/// <summary>Multiplier applied to all healing spells before modifier pipeline runs. Default 1.0.</summary>
	public float HealingMultiplier { get; set; } = 1.0f;

	public float CastSpeedMultiplier { get; set; } = 1.0f;

	public Dictionary<SpellSchool, float> SpellSchoolDamageMultipliers = new()
	{
		{ SpellSchool.Nature, 1.0f },
		{ SpellSchool.Holy, 1.0f },
		{ SpellSchool.Void, 1.0f },
		{ SpellSchool.Chronomancy, 1.0f },
		{ SpellSchool.Generic, 1.0f }
	};

	/// <summary>
	/// Multiplier applied to all incoming damage before shield/health reduction.
	/// Values above 1.0 increase damage taken (debuffs); below 1.0 reduce it (mitigation).
	/// Default 1.0 (no modification).
	/// </summary>
	public float DamageTakenMultiplier { get; set; } = 1.0f;

	/// <summary>
	/// When true, the next non-instant spell cast by this character will be
	/// fired immediately, bypassing the normal cast timer.
	/// Set by talents such as <see cref="Talents.Chronomancy.TemporalMomentumTalent"/>.
	/// </summary>
	public bool NextCastIsInstant { get; set; } = false;
}