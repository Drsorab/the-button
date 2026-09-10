using Godot;
using System;

public partial class PressureGaugeView : Control
{
    // Αναφορά στον κόμβο Pivot (GaugeNeedle)
    [Export] private Node2D _needlePivot;

    // Γωνίες σε μοίρες: -135° είναι περίπου στο 0 PSI, +135° στο Danger (200 PSI)
    [Export] public float MinAngleDeg = -7.2f;
    [Export] public float MaxAngleDeg = 279.5f;
    [Export] public float MaxPressureValue = 100.0f;

    private Tween _activeTween;

    public override void _Ready()
    {
        if (_needlePivot == null)
            _needlePivot = GetNodeOrNull<Node2D>("GaugeNeedle");

        // Αρχική θέση δείκτη στο 0 PSI
        UpdatePressure(0.0f, true);
    }

    // Ενημέρωση της γωνίας του δείκτη βάσει της τιμής πίεσης (0-100)
    public void UpdatePressure(float currentPressure, bool instant = false)
    {
        if (_needlePivot == null) return;

        // Μετατροπή [0, MaxPressureValue] σε εύρος μοιρών [MinAngleDeg, MaxAngleDeg]
        float t = Mathf.Clamp(currentPressure / MaxPressureValue, 0.0f, 1.0f);
        float targetDeg = Mathf.Lerp(MinAngleDeg, MaxAngleDeg, t);
        float targetRad = Mathf.DegToRad(targetDeg);

        if (instant)
        {
            _needlePivot.Rotation = targetRad;
            return;
        }

        // Ακύρωση προηγούμενου ενεργού tween για αποφυγή συγκρούσεων
        if (_activeTween != null && _activeTween.IsValid())
        {
            _activeTween.Kill();
        }

        // Ομαλή περιστροφή με μικρή ταλάντωση (Back ease) για βιομηχανική αίσθηση
        _activeTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _activeTween.TweenProperty(_needlePivot, "rotation", targetRad, 0.18f);
    }
}