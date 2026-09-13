using Godot;
using System;

public partial class PressureGaugeView : Control
{
    [Export] private Node2D _needlePivot;

    [Export] public float MinAngleDeg = -7.2f;
    [Export] public float MaxAngleDeg = 279.5f;
    [Export] public float MaxPressureValue = 100.0f;
    [Export] public float FollowSpeed = 12.0f; // Ταχύτητα ομαλής παρακολούθησης της βελόνας

    private float _targetRad = 0.0f;
    private Tween _dischargeTween;
    private bool _isDischargeTweening = false;

    public override void _Ready()
    {
        if (_needlePivot == null)
            _needlePivot = GetNodeOrNull<Node2D>("GaugeNeedle");

        _targetRad = Mathf.DegToRad(MinAngleDeg);
        if (_needlePivot != null)
        {
            _needlePivot.Rotation = _targetRad;
        }
    }

    public override void _Process(double delta)
    {
        if (_needlePivot == null || _isDischargeTweening)
            return;

        // Συνεχής, ομαλή προσέγγιση της βελόνας χωρίς καμία GC κατανομή μνήμης
        _needlePivot.Rotation = Mathf.LerpAngle(_needlePivot.Rotation, _targetRad, (float)delta * FollowSpeed);
    }

    /// <summary>
    /// Καλείται από το PressureSubsystem.
    /// instant: θέσε true μόνο σε απότομη εκτόνωση/DischargePressure για εφέ απότομης πτώσης.
    /// </summary>
    public void UpdatePressure(float currentPressure, bool instant = false)
    {
        float t = Mathf.Clamp(currentPressure / MaxPressureValue, 0.0f, 1.0f);
        float targetDeg = Mathf.Lerp(MinAngleDeg, MaxAngleDeg, t);
        _targetRad = Mathf.DegToRad(targetDeg);

        if (_needlePivot == null) return;

        if (instant)
        {
            // Ακύρωση τυχόν προηγούμενου tween
            if (_dischargeTween != null && _dischargeTween.IsValid())
            {
                _dischargeTween.Kill();
            }

            // Στιγμιαία απότομη κίνηση με ελατήριο (Back ease) για βιομηχανική αίσθηση εκτόνωσης
            _isDischargeTweening = true;
            _dischargeTween = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            _dischargeTween.TweenProperty(_needlePivot, "rotation", _targetRad, 0.22f);
            _dischargeTween.Finished += () => _isDischargeTweening = false;
        }
    }
}