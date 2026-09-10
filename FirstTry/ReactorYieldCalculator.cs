using Godot;
using System;

public partial class ReactorYieldCalculator : Node
{
    // 1. Παράθυρα Χρόνου Πατήματος
    [ExportGroup("Timing Windows")]
    [Export] public double AvailableWindowBeforeZero = 10.0; // Πόσα δευτερόλεπτα πριν το 0 ξεκλειδώνει το κουμπί
    [Export] public double GracePeriodAfterZero = 10.0;      // Πόσα δευτερόλεπτα μετά το 0 έχεις περιθώριο

    // 2. Παράθυρο Sweet Spot
    [ExportGroup("Sweet Spot Settings")]
    [Export] public double SweetSpotStart = 5.0;            // Ξεκινάει στα 5 δευτερόλεπτα πριν το 0
    [Export] public double SweetSpotEnd = 0.0;              // Τελειώνει ακριβώς στο 0
    [Export] public float SweetSpotBonusMultiplier = 0.35f; // 4. +35% bonus πόρων αν πατηθεί στο sweet spot

    // 3. Min / Max Βάσεις Πόρων (χωρίς bonus/penalties)
    [ExportGroup("Base Resource Yields (Max Yield)")]
    [Export] public int MaxMetalScraps = 20;
    [Export] public int MaxElectricity = 15;
    [Export] public int MaxHeat = 25;

    [ExportGroup("Minimum Resource Yields (Floors)")]
    [Export] public int MinMetalScraps = 3;
    [Export] public int MinElectricity = 2;
    [Export] public int MinHeat = 4;

    [ExportGroup("Decay Settings")]
    [Export] public float DecaySteepness = 3.0f; // Πόσο επιθετική είναι η εκθετική πτώση μετά το 0

    // Υπολογίζει τα resources με βάση τη χρονική στιγμή πατήματος
    public ResourceYield CalculateYield(double currentTimeAtPress, float coreEfficiency = 1.0f)
    {
        ResourceYield result = new ResourceYield();
        float timingMultiplier = 1.0f;

        // 1. Περίπτωση Α: Sweet Spot (μεταξύ 5s και 0s)
        if (currentTimeAtPress <= SweetSpotStart && currentTimeAtPress >= SweetSpotEnd)
        {
            timingMultiplier = 1.0f + SweetSpotBonusMultiplier;
            result.WasSweetSpot = true;
        }
        // 2. Περίπτωση Β: Έγκαιρο πάτημα πριν το sweet spot
        else if (currentTimeAtPress > SweetSpotStart)
        {
            timingMultiplier = 1.0f;
            result.WasSweetSpot = false;
        }
        // 3. Περίπτωση Γ: Καθυστερημένο πάτημα (< 0s) -> Εκθετική μείωση
        else if (currentTimeAtPress < 0)
        {
            result.WasSweetSpot = false;

            float lateFactor = Mathf.Clamp((float)(-currentTimeAtPress / GracePeriodAfterZero), 0.0f, 1.0f);
            timingMultiplier = Mathf.Exp(-DecaySteepness * lateFactor);
        }

        // Διασφάλιση ορίων υγείας πυρήνα (0.0 έως 1.0)
        float clampedEfficiency = Mathf.Clamp(coreEfficiency, 0.0f, 1.0f);

        // Συνολικός συντελεστής απόδοσης: Timing * Υγεία Πυρήνα
        result.EfficiencyPercentage = timingMultiplier * clampedEfficiency;

        // Εφαρμογή του ενιαίου multiplier απευθείας στο CalculateResource
        result.MetalScraps = CalculateResource(MaxMetalScraps, MinMetalScraps, result.EfficiencyPercentage);
        result.Electricity = CalculateResource(MaxElectricity, MinElectricity, result.EfficiencyPercentage);
        result.Heat = CalculateResource(MaxHeat, MinHeat, result.EfficiencyPercentage);

        return result;
    }

    private int CalculateResource(int maxVal, int minVal, float multiplier)
    {
        if (multiplier >= 1.0f)
        {
            // Bonus Sweet Spot: +% απευθείας πάνω στο Max
            return Mathf.RoundToInt(maxVal * multiplier);
        }

        // Καθυστέρηση: Ομαλή πτώση από το Max προς το Min
        int decayed = Mathf.RoundToInt(minVal + (maxVal - minVal) * multiplier);
        return Mathf.Clamp(decayed, minVal, maxVal);
    }
}