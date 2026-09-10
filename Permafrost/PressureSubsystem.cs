using Godot;
using System;

public partial class PressureSubsystem : Node
{
    // Events για ενημέρωση του UI και του Manager
    public event Action<float> OnPressureChanged;
    public event Action<float> OnIntegrityChanged;

    [Export] public float MaxPressure = 100.0f;
    [Export] public float CurrentPressure { get; private set; } = 15.0f;
    [Export] public float CasingIntegrity { get; private set; } = 100.0f;

    private bool _isSimulating = false;
    private float _buildupRate = 0.0f;

    public void StartSimulation(double countdownDuration)
    {
        _isSimulating = true;
        // Υπολογισμός ρυθμού ώστε να φτάσει στο 90% όταν ανοίξει το παράθυρο εκτόνωσης
        float targetPressure = 90.0f;
        _buildupRate = (targetPressure - CurrentPressure) / (float)countdownDuration;
    }

    public void StopSimulation()
    {
        _isSimulating = false;
    }

    public override void _Process(double delta)
    {
        if (!_isSimulating) return;

        CurrentPressure = Mathf.Clamp(CurrentPressure + (_buildupRate * (float)delta), 0.0f, MaxPressure);
        OnPressureChanged?.Invoke(CurrentPressure);
    }

    // Εκτόνωση πίεσης ανάλογα με τη λειτουργία του μοχλού
    public void DischargePressure(float targetLevel, float integrityWearPercent)
    {
        CurrentPressure = targetLevel;
        ApplyWear(integrityWearPercent);
        OnPressureChanged?.Invoke(CurrentPressure);
    }

    // Ζημιά στην κάσα (από φυσιολογική φθορά ή έκρηξη ασφάλειας)
    public void ApplyWear(float amount)
    {
        CasingIntegrity = Mathf.Clamp(CasingIntegrity - amount, 0.0f, 100.0f);
        OnIntegrityChanged?.Invoke(CasingIntegrity);
    }

    // Επισκευή κάσας (Cold Patch ή Hot Weld)
    public void RestoreIntegrity(float amount)
    {
        CasingIntegrity = Mathf.Clamp(CasingIntegrity + amount, 0.0f, 100.0f);
        OnIntegrityChanged?.Invoke(CasingIntegrity);
    }
}