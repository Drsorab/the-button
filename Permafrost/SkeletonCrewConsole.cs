using Godot;
using System;

public partial class SkeletonCrewConsole : Control
{
    [Export] private Label _timerLabel;
    private double _secondsUntilNextShift = 16.0 * 3600.0; // 16 ώρες baseline
    private bool _isStandby = false;

    public override void _Ready()
    {
        if (_timerLabel == null)
            _timerLabel = GetNodeOrNull<Label>("Content/NextShiftTimerLabel");
    }

    public void ActivateStandby(double remainingSecondsToNextShift)
    {
        _secondsUntilNextShift = remainingSecondsToNextShift;
        _isStandby = true;
        Visible = true;
    }

    public void DeactivateStandby()
    {
        _isStandby = false;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!_isStandby) return;

        _secondsUntilNextShift = Math.Max(0.0, _secondsUntilNextShift - delta);

        int hours = (int)(_secondsUntilNextShift / 3600);
        int minutes = (int)((_secondsUntilNextShift % 3600) / 60);
        int seconds = (int)(_secondsUntilNextShift % 60);

        if (_timerLabel != null)
        {
            _timerLabel.Text = $"NEXT ACTIVE SHIFT IN: {hours:D2}h {minutes:D2}m {seconds:D2}s";
        }
    }
}