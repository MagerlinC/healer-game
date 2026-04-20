#nullable enable
using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class SpellResource : Resource
{
    [Export] public string    Name;
    [Export] public string    Description;
    [Export] public float     ManaCost;
    [Export] public float     CastTime;
    [Export] public Texture2D Icon;

    /// <summary>
    /// Tags that describe this spell's school and behaviour.
    /// Used by the modifier pipeline to gate modifiers conditionally.
    /// Set these in each subclass constructor.
    /// </summary>
    public SpellTags Tags { get; protected set; } = SpellTags.None;

    // ── Pipeline integration ─────────────────────────────────────────────────

    /// <summary>
    /// The raw numeric value that seeds <see cref="SpellContext.BaseValue"/>.
    /// Override in subclasses to return the primary magnitude (heal amount,
    /// damage amount, HoT per-tick value, etc.).
    /// Returns 0 by default for spells with no meaningful numeric output.
    /// </summary>
    public virtual float GetBaseValue() => 0f;

    /// <summary>
    /// Resolves the full target list for this cast.
    /// Single-target spells return a one-element list (the default).
    /// Group spells override this to return all valid targets.
    /// </summary>
    public virtual List<Character> ResolveTargets(Character caster, Character explicitTarget)
        => new() { explicitTarget };

    /// <summary>
    /// Execute the spell using the fully-processed <see cref="SpellContext"/>.
    /// Override in subclasses to read <see cref="SpellContext.FinalValue"/>
    /// and <see cref="SpellContext.Targets"/> so that modifier pipeline output
    /// is honoured.
    ///
    /// The default implementation falls back to <see cref="Act"/> for
    /// backwards compatibility with any subclass that has not yet been updated.
    /// </summary>
    public virtual void Apply(SpellContext ctx)
    {
        // Fallback: call the old API. Subclasses should override Apply directly.
        Act(ctx.Caster, ctx.Target ?? ctx.Caster);
    }

    /// <summary>
    /// Legacy direct-cast entry point. Kept for backwards compatibility.
    /// Prefer overriding <see cref="Apply"/> in new spell subclasses.
    /// </summary>
    public virtual void Act(Character caster, Character target)
    {
        GD.Print("Base spell has no effect.");
    }
}
