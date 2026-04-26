using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Generic;

/// <summary>
/// Time-sensitive parry against telegraphed boss abilities.
///
/// Cast during a boss's wind-up (while the parry window is open) to reduce
/// the incoming attack's damage to zero and play the deflect sound cue.
/// If cast outside a parry window, the spell still goes on cooldown but
/// has no effect — the player needs to time it correctly.
///
/// Always available — lives in the player's generic action bar and cannot
/// be removed from the loadout.
/// </summary>
[GlobalClass]
public partial class DeflectSpell : SpellResource
{
	const string DeflectSoundPath = "res://assets/sound-effects/deflect.wav";

	public DeflectSpell()
	{
		Name = "Deflect";
		Description = "Parries an incoming telegraphed attack, reducing its damage to zero. Must be cast during the attack wind-up.";
		ManaCost = 0f;
		CastTime = 0f;
		Cooldown = 1f;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "generic/deflect.png");
		School = SpellSchool.Generic;
		EffectType = EffectType.Helpful;
		Tags = SpellTags.None;
	}

	public override void Apply(SpellContext ctx)
	{
		var deflected = ParryWindowManager.TryDeflect();
		if (!deflected) return;

		// Play the parry sound through the caster node so it's positioned in the scene.
		if (ctx.Caster == null) return;
		var audio = new AudioStreamPlayer();
		audio.Stream = GD.Load<AudioStream>(DeflectSoundPath);
		audio.VolumeDb = -4.0f;
		ctx.Caster.AddChild(audio);
		audio.Play();
		audio.Finished += audio.QueueFree;
	}
	public static void PlayDeflectFailedSound(Character caster)
	{
		var audio = new AudioStreamPlayer();
		audio.Stream = GD.Load<AudioStream>(AssetConstants.DeflectFailedSoundPath);
		audio.VolumeDb = -4.0f;
		caster.AddChild(audio);
		audio.Play();
		audio.Finished += audio.QueueFree;
	}
}