using Godot;
using healerfantasy;
using SpellResource = healerfantasy.SpellResources.SpellResource;

public partial class CastBar : ProgressBar
{
	float _duration;
	float _remaining;
	bool _isCasting = false;

	public override void _Ready()
	{
		base._Ready();
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CastStarted),
			Callable.From((SpellResource spell) => StartCast(spell))
		);
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Player.CastCancelled),
			Callable.From(StopCast)
		);
		Visible = false;
	}
	public override void _Process(double delta)
	{
		if (!_isCasting) return;

		_remaining -= (float)delta;

		var progress = 1f - _remaining / _duration;
		Value = Mathf.Clamp(progress, 0f, 1f);

		if (_remaining <= 0f)
		{
			StopCast();
		}
	}

	public void StartCast(SpellResource spell)
	{
		_duration = spell.CastTime;
		_remaining = spell.CastTime;
		_isCasting = true;

		MinValue = 0;
		MaxValue = 1;
		Value = 0;
		Visible = true;
	}

	void StopCast()
	{
		_isCasting = false;
		Visible = false;
	}
}