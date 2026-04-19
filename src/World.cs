using Godot;

/// <summary>
/// Root script for the World scene.
/// Wires each character's HealthChanged signal to the matching PartyUI slot
/// so the bars stay in sync without any character needing to know about the UI.
///
/// Slot order matches PartyUI.MemberDefs:
///   0 = Templar | 1 = Healer (Player) | 2 = Assassin | 3 = Wizard
/// </summary>
public partial class World : Node2D
{
    public override void _Ready()
    {
        var ui = GetNode<GameUI>("PartyUI");

        Wire(GetNode<Character>("Templar"),  ui, 0);
        Wire(GetNode<Character>("Player"),   ui, 1);
        Wire(GetNode<Character>("Assassin"), ui, 2);
        Wire(GetNode<Character>("Wizard"),   ui, 3);
    }

    // ── helpers ──────────────────────────────────────────────────────────────
    private static void Wire(Character c, GameUI ui, int slot)
    {
        // Then keep it in sync via signal
        c.HealthChanged += (current, max) => ui.SetHealth(slot, current, max);
    }
}
