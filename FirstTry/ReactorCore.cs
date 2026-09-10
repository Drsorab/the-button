using Godot;
using System;

//public enum ShiftMode
//{
//    ActiveShift,   // 8ωρο ενεργού παίκτη: Υψηλή παραγωγή, πλήρης έλεγχος
//    SkeletonCrew   // 16ωρο συντήρησης: Ελάχιστη παραγωγή, έντονη ψύξη
//}

public partial class ReactorCore : Node
{
    // Signals για ενημέρωση του UI
    [Signal] public delegate void TemperatureChangedEventHandler(float newTemp);
    [Signal] public delegate void CoreDamageTakenEventHandler(float currentHealth, float damageDelta);
    [Signal] public delegate void ShiftModeChangedEventHandler(int newMode);

    [ExportGroup("Core Temperature Specs")]
    [Export] public float CurrentTemperature = 280.0f;     // Τρέχουσα θερμοκρασία (°C)
    [Export] public float AmbientTemperature = 18.0f;      // Εξωτερική θερμοκρασία περιβάλλοντος (°C)
    [Export] public float ThermalCapacity = 1500.0f;       // C_th: Θερμοχωρητικότητα (αντίσταση στην αλλαγή θερμοκρασίας)
    [Export] public float HeatLossCoefficient = 2.2f;      // k: Συντελεστής διαρροής θερμότητας (ποιότητα μόνωσης)

    [ExportGroup("Power & Heat Inputs (kW)")]
    [Export] public float SkeletonPassiveHeat = 12.0f;     // Ελάχιστη ισχύς συντήρησης στο Skeleton Crew
    [Export] public float HeatPerEnergyUnit = 8.5f;        // Πόση θερμική ισχύ αποδίδει κάθε μονάδα Heat από το πάτημα

    [ExportGroup("Safety & Integrity")]
    [Export] public float CriticalTempThreshold = 95.0f;   // Κάτω από αυτό το όριο ο πυρήνας παγώνει και φθείρεται
    [Export] public float MaxCoreHealth = 100.0f;          // Δομική ακεραιότητα πυρήνα                                                       

    [Export] public float DamagePerSecondBelowCritical = 0.5f; // Ζημιά ανά δευτερόλεπτο υποθερμίας

    public ShiftMode CurrentMode { get; private set; } = ShiftMode.ActiveShift;
    public float CurrentHealth { get; private set; }
    public float EfficiencyMultiplier => Mathf.Clamp(CurrentHealth / MaxCoreHealth, 0.0f, 1.0f);

    // Συσσωρευμένη θερμική ισχύς προς διοχέτευση από το πάτημα του κουμπιού
    private float _transientHeatBuffer = 0.0f;

    public override void _Ready()
    {
        CurrentHealth = MaxCoreHealth;
    }

    public override void _Process(double delta)
    {
        UpdateThermodynamics((float)delta);
    }

    // 1. Διοχέτευση παραχθείσας θερμότητας από το πάτημα κουμπιού του παίκτη
    public void InjectHeatFromReset(int consumedHeatUnits)
    {
        if (CurrentMode != ShiftMode.ActiveShift) return;

        // Μετατροπή των πόντων θερμότητας σε θερμική ισχύ (kW)
        _transientHeatBuffer += consumedHeatUnits * HeatPerEnergyUnit;
    }

    // 2. Εναλλαγή μεταξύ Βάρδιας και Skeleton Crew
    public void SetShiftMode(ShiftMode newMode)
    {
        if (CurrentMode == newMode) return;

        CurrentMode = newMode;
        _transientHeatBuffer = 0.0f; // Μηδενισμός στιγμιαίας ώθησης
        EmitSignal(SignalName.ShiftModeChanged, (int)CurrentMode);

        GD.Print($"[CORE] Εναλλαγή Κατάστασης: {CurrentMode}");
    }

    // 3. Βασικός Θερμοδυναμικός Κύκλος (Newton's Law of Cooling)
    private void UpdateThermodynamics(float delta)
    {
        // Α. Υπολογισμός εισερχόμενης θερμότητας (Q_in)
        float heatIn = 0.0f;

        if (CurrentMode == ShiftMode.ActiveShift)
        {
            // Στη βάρδια απορροφάται σταδιακά η ώθηση θερμότητας από τα πατήματα
            heatIn = _transientHeatBuffer;
            _transientHeatBuffer = Mathf.MoveToward(_transientHeatBuffer, 0.0f, 15.0f * delta);
        }
        else
        {
            // Στο Skeleton Crew λειτουργεί μόνο ο καυστήρας συντήρησης
            heatIn = SkeletonPassiveHeat;
        }

        // Β. Υπολογισμός διαρροής προς το περιβάλλον (Q_loss = k * (T - T_amb))
        float heatLoss = HeatLossCoefficient * (CurrentTemperature - AmbientTemperature);

        // Γ. Μεταβολή θερμοκρασίας: dT/dt = (Q_in - Q_loss) / C_th
        float deltaTemp = ((heatIn - heatLoss) / ThermalCapacity) * delta;
        CurrentTemperature = Mathf.Max(AmbientTemperature, CurrentTemperature + deltaTemp);

        EmitSignal(SignalName.TemperatureChanged, CurrentTemperature);

        // Δ. Έλεγχος Ζημιάς λόγω Υποθερμίας
        CheckCoreIntegrity(delta);
    }

    // 4. Υπολογισμός Φθοράς
    private void CheckCoreIntegrity(float delta)
    {
        // Αν η θερμοκρασία πέσει κάτω από το κρίσιμο όριο
        if (CurrentTemperature < CriticalTempThreshold)
        {
            // Όσο πιο χαμηλά πέφτει από το όριο, τόσο πιο γρήγορα φθείρεται
            float severityFactor = 1.0f + ((CriticalTempThreshold - CurrentTemperature) / CriticalTempThreshold);
            float damage = DamagePerSecondBelowCritical * severityFactor * delta;

            CurrentHealth = Mathf.Max(0.0f, CurrentHealth - damage);
            EmitSignal(SignalName.CoreDamageTaken, CurrentHealth, damage);

            if (CurrentHealth <= 0.0f)
            {
                GD.PrintErr("[CRITICAL FAILURE] Ο πυρήνας ράγισε από παρατεταμένη υποθερμία!");
            }
        }
    }
}