using Godot;
using System;

public struct ResourceYield
{
    public int MetalScraps;
    public int Electricity;
    public int Heat;
    public bool WasSweetSpot;
    public float EfficiencyPercentage; // π.χ. 125% για sweet spot, 30% για καθυστερημένο πάτημα
}

public partial class ResourceManager : Node
{
    public event Action<int, int, int> OnResourcesChanged;

    public int MetalScraps { get; private set; } = 0;
    public int Electricity { get; private set; } = 0;
    public int Heat { get; private set; } = 0;

    // Μεταβλητές Μετατροπής (τροποποιήσιμες από upgrades)
    [Export] public float ElectricityToHeatRate = 1.0f; // 1 kW = 1 Heat, π.χ. +50% αποδοτικότητα 1.5f
    [Export] public double SecondsReducedPerHeat = 30.0; // 2 Heat = 60s (1 min) μείωση

    public void AddResources(ResourceYield yield)
    {
        MetalScraps += yield.MetalScraps;
        Electricity += yield.Electricity;
        Heat += yield.Heat;

        OnResourcesChanged?.Invoke(MetalScraps, Electricity, Heat);

        GD.Print($"[RESOURCES GAINED] Metal: +{yield.MetalScraps}, Elec: +{yield.Electricity}, Heat: +{yield.Heat} (Eff: {yield.EfficiencyPercentage:P0})");
    }

    // 1. Μετατροπή όλου του ρεύματος σε θερμότητα
    public void ConvertElectricityToHeat()
    {
        if (Electricity <= 0) return;

        int convertedHeat = Mathf.FloorToInt(Electricity * ElectricityToHeatRate);
        Heat += convertedHeat;
        Electricity = 0;

        OnResourcesChanged?.Invoke(MetalScraps, Electricity, Heat);
    }

    // 2. Κατανάλωση όλης της θερμότητας για μείωση χρόνου
    public double ConsumeHeatForTimeReduction()
    {
        if (Heat <= 0) return 0.0;

        double totalSecondsToReduce = Heat * SecondsReducedPerHeat;
        Heat = 0; // Καταναλώνεται όλη η θερμότητα

        OnResourcesChanged?.Invoke(MetalScraps, Electricity, Heat);
        return totalSecondsToReduce;
    }

    public bool TrySpendResources(int metal, int elec, int heat)
    {
        if (MetalScraps >= metal && Electricity >= elec && Heat >= heat)
        {
            MetalScraps -= metal;
            Electricity -= elec;
            Heat -= heat;
            OnResourcesChanged?.Invoke(MetalScraps, Electricity, Heat);
            return true;
        }
        return false;
    }
}