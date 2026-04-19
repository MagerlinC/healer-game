#nullable enable
using Godot;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class SpellResource : Resource
{
	[Export] public string Name;
	[Export] public string Description;
	[Export] public float ManaCost;
	[Export] public float CastTime;
	[Export] public Texture2D Icon;
	public virtual void Act(Character caster, Character target)
	{
		GD.Print("Base spell has no effect.");
	}
}