using Godot;
using healerfantasy;
using SpellResource = healerfantasy.SpellResources.SpellResource;

public partial class CastBar : ProgressBar
{
    private float _duration;
    private float _remaining;
    private bool _isCasting = false;

    public override void _Ready()
    {
        base._Ready();
        GlobalAutoLoad.SubscribeToSignal(
            nameof(Player.CastStarted),
            Callable.From((SpellResource spell) => StartCast(spell))
        );
        Visible = false;
    }
    public override void _Process(double delta)
    {
        if (!_isCasting) return;

        _remaining -= (float)delta;

        float progress = 1f - (_remaining / _duration);
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

    private void StopCast()
    {
        _isCasting = false;
        Visible = false;
    }
}
