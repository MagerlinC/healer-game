using Godot;
using healerfantasy.Items;

/// <summary>
/// A row of <see cref="ItemEffectIndicator"/> badges shown below the healer's
/// party frame while item effects are active.
///
/// Subscribes to <see cref="ItemEffectBus"/> on <c>_Ready</c> and replays
/// any effects that were already active (e.g. a Pendant charge that carried
/// over from a previous fight).  Unsubscribes cleanly in <c>_ExitTree</c>.
///
/// Mirrors the <see cref="CharacterFrame.EffectBar"/> pattern used for spell
/// effects, but lives below the health panel rather than above it, and is
/// currently only added to the healer's <see cref="PartyFrame"/>.
/// </summary>
public partial class ItemEffectBar : HBoxContainer
{
    // ── constructor ───────────────────────────────────────────────────────────
    public ItemEffectBar()
    {
        AddThemeConstantOverride("separation", 3);
        // No forced minimum height — the bar collapses to zero when no item
        // effects are active, so the healer frame stays the same height as the
        // other party frames (which have no ItemEffectBar).  When indicators
        // are present they carry their own 32 px CustomMinimumSize.
        MouseFilter = MouseFilterEnum.Ignore;
    }

    // ── lifecycle ─────────────────────────────────────────────────────────────
    public override void _Ready()
    {
        ItemEffectBus.ItemEffectActivated += OnItemEffectActivated;
        ItemEffectBus.ItemEffectDeactivated += OnItemEffectDeactivated;

        // Pick up any effects that fired before this bar entered the tree.
        ItemEffectBus.ReplayCurrentState(OnItemEffectActivated);
    }

    public override void _ExitTree()
    {
        ItemEffectBus.ItemEffectActivated -= OnItemEffectActivated;
        ItemEffectBus.ItemEffectDeactivated -= OnItemEffectDeactivated;
    }

    // ── private ───────────────────────────────────────────────────────────────
    void OnItemEffectActivated(string effectId, Texture2D? icon, string displayName, string description)
    {
        // Remove stale indicator first so re-activation doesn't create duplicates.
        OnItemEffectDeactivated(effectId);
        AddChild(new ItemEffectIndicator(effectId, icon, displayName, description));
    }

    void OnItemEffectDeactivated(string effectId)
    {
        foreach (var child in GetChildren())
        {
            if (child is ItemEffectIndicator ind && ind.EffectId == effectId)
            {
                ind.QueueFree();
                return;
            }
        }
    }
}
