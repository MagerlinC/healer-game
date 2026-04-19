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
        var ui       = GetNode<GameUI>("PartyUI");
        var player   = GetNode<Player>("Player");
        var templar  = GetNode<Character>("Templar");
        var assassin = GetNode<Character>("Assassin");
        var wizard   = GetNode<Character>("Wizard");

        Wire(templar,  ui, 0);
        Wire(player,   ui, 1);
        Wire(assassin, ui, 2);
        Wire(wizard,   ui, 3);

        // Bind characters so hovering a frame resolves to the right Character
        ui.BindCharacter(0, templar);
        ui.BindCharacter(1, player);
        ui.BindCharacter(2, assassin);
        ui.BindCharacter(3, wizard);

        // Give the Player a reference to the UI for hover-target resolution
        player.GameUI = ui;

        // Populate the action bar with the player's spell bindings
        ui.SetupActionBar(player.GetSpellBindings());
    }

    // ── helpers ──────────────────────────────────────────────────────────────
    private static void Wire(Character c, GameUI ui, int slot)
    {
        c.HealthChanged += (current, max) => ui.SetHealth(slot, current, max);
    }
}
