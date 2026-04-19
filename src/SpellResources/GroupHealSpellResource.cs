using Godot;

namespace healerfantasy.SpellResources;

/// <summary>
/// Heals every member of the "party" group for a fixed amount.
/// The <paramref name="target"/> parameter is intentionally ignored —
/// the caster's scene tree is used to reach all party members instead.
/// </summary>
[GlobalClass]
public partial class GroupHealSpellResource : SpellResource
{
	[Export] public float HealAmount = 25f;

	public override void Act(Character caster, Character _target)
	{
		foreach (var node in caster.GetTree().GetNodesInGroup("party"))
		{
			if (node is Character c)
				c.Heal(HealAmount);
		}
	}

	public GroupHealSpellResource()
	{
		Name = "Group Heal";
		Description = $"Restores {HealAmount} health to all party members.";
		ManaCost = 20f;
		CastTime = 2f;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/healer/healer2.png");
	}
}