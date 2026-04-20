using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class HealSpellResource : SpellResource
{
    [Export] public float HealAmount = 25f;

    public HealSpellResource()
    {
        Name        = "Heal";
        Description = $"Restores {HealAmount} health to the target.";
        ManaCost    = 5f;
        CastTime    = 1.5f;
        Tags        = SpellTags.Healing;
        Icon        = GD.Load<Texture2D>("res://assets/spell-icons/healer/healer1.png");
    }

    public override float GetBaseValue() => HealAmount;

    public override void Apply(SpellContext ctx)
    {
        ctx.Target?.Heal(ctx.FinalValue);
    }
}
