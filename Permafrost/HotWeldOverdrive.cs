using Godot;
using System;

public partial class HotWeldOverdrive : Node
{
    // Events για το UI και τον Manager
    public event Action<float> OnStressChanged; // Επιστρέφει 0-100%
    public event Action<bool> OnOverdriveToggled;
    public event Action OnOverdriveBlowout; // Όταν χτυπήσει 100% stress

    [Export] public float StressPercent { get; private set; } = 0.0f;
    [Export] public bool IsActive { get; private set; } = false;

    // Παράμετροι ρυθμών (ανά δευτερόλεπτο)
    [Export] public float StressBuildRatePerSec = 5.0f;   // Φτάνει στο 100% σε 20 δευτερόλεπτα
    [Export] public float StressDecayRatePerSec = 2.5f;   // Κρυώνει σε 40 δευτερόλεπτα
    [Export] public float RepairRatePerSec = 3.0f;        // Επισκευή κάσας %
    [Export] public float ScrapConsumptionInterval = 2.5f;// Κατανάλωση scrap κάθε X (2.5) δευτερόλεπτα
    [Export] public int ScrapConsumption = 1; // Πόσα scrap καταναλώνονται ανά κύκλο
    private float _scrapTimer = 0.0f;

    // Επιστρέφει τον πολλαπλασιαστή ταχύτητας για το NixieTimer
    // Το μέγιστο όριο είναι 3.5x (όταν το Stress φτάσει στο 100%)
    // Ακόμα και αν κλείσει ο διακόπτης, η ταχύτητα αποκλιμακώνεται ομαλά 
    // ακολουθώντας την ψύξη του StressPercent πίσω στο 1.0x.
    public double TimeDilationMultiplier
    {
        get
        {
            // Βασική ταχύτητα 1.0x συν πρόσθετη επιτάχυνση έως +2.5x βάσει θερμότητας/stress
            float stressNormalized = Mathf.Clamp(StressPercent / 100.0f, 0.0f, 1.0f);
            return 1.0 + (stressNormalized * 2.5);
        }
    }

    public void ToggleOverdrive(bool enable)
    {
        if (enable == IsActive) return;

        IsActive = enable;
        OnOverdriveToggled?.Invoke(IsActive);
    }

    public void ForceShutdown()
    {
        IsActive = false;
        OnOverdriveToggled?.Invoke(false);
    }

    public void ProcessOverdrive(double delta, ResourceSubsystem resources, PressureSubsystem pressure)
    {
        if (IsActive)
        {
            // 1. Κατανάλωση Scrap ανά τακτά διαστήματα
            _scrapTimer += (float)delta;
            if (_scrapTimer >= ScrapConsumptionInterval)
            {
                _scrapTimer = 0.0f;
                if (!resources.TryConsumeScrap(ScrapConsumption))  // Number of Scrap consumption 
                {
                    // Αν τελειώσει το scrap, το σύστημα σβήνει αυτόματα
                    GD.Print("[OVERDRIVE] Εξάντληση Scrap! Αυτόματη απενεργοποίηση.");
                    ForceShutdown();
                    return;
                }
            }

            // 2. Επισκευή κάσας
            pressure.RestoreIntegrity(RepairRatePerSec * (float)delta);

            // 3. Αύξηση Stress
            StressPercent = Mathf.Clamp(StressPercent + (StressBuildRatePerSec * (float)delta), 0.0f, 100.0f);
            OnStressChanged?.Invoke(StressPercent);

            // 4. Έλεγχος Υπερθέρμανσης (Blowout στο 100%)
            if (StressPercent >= 100.0f)
            {
                TriggerBlowout(pressure);
            }
        }
        else
        {
            // Σταδιακή ψύξη και πτώση του stress όταν είναι κλειστό
            if (StressPercent > 0.0f)
            {
                StressPercent = Mathf.Clamp(StressPercent - (StressDecayRatePerSec * (float)delta), 0.0f, 100.0f);
                OnStressChanged?.Invoke(StressPercent);
            }
            _scrapTimer = 0.0f;
        }
    }

    // Καταστροφική εκτόνωση λόγω υπερβολικής παραμονής στο 100%
    private void TriggerBlowout(PressureSubsystem pressure)
    {
        GD.Print("[CRITICAL] Υπερθέρμανση Hot-Weld! Έκρηξη σωληνώσεων!");
        ForceShutdown();
        pressure.ApplyWear(25.0f); // Σοβαρή ζημιά στην κάσα
        OnOverdriveBlowout?.Invoke();
    }
}