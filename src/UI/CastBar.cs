using Godot;
using healerfantasy;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// Player cast bar. Shown while the player is channelling a spell.
/// Extends <see cref="CastBarBase"/> and wires the <see cref="Player.CastStarted"/>
/// and <see cref="Player.CastCancelled"/> signals automatically.
/// </summary>
public partial class CastBar : CastBarBase
{
	public override void _Ready()
	{
		base._Ready();

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CastStarted),
			Callable.From((SpellResource spell, float adjustedDuration) =>
				StartCast(spell, adjustedDuration)));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CastCancelled),
			Callable.From(StopCast));
	}
}
