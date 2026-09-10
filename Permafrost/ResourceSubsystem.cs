using Godot;
using System;

public partial class ResourceSubsystem : Node
{
    public event Action<float, float, float, int> OnResourcesUpdated;

    [Export] public float CoalStockpileKg = 380.0f;
    [Export] public float BatteryChargePercent = 50.0f;
    [Export] public float CityTemperatureC = -40.0f;
    [Export] public int MetalScrapCount = 15;

    [Export] public float CoalBurnRatePerSec = 0.05f;
    [Export] public float ElectricityChargeRatePerSec = 0.04f;
    [Export] public float HeatDecayRatePerSec = 0.02f;

    private bool _isActive = false;

    public void SetActive(bool active)
    {
        _isActive = active;
    }

    public override void _Process(double delta)
    {
        if (!_isActive) return;

        // Σταδιακή κατανάλωση κάρβουνου και φόρτιση μπαταρίας
        CoalStockpileKg = Mathf.Max(0.0f, CoalStockpileKg - (CoalBurnRatePerSec * (float)delta));
        BatteryChargePercent = Mathf.Clamp(BatteryChargePercent + (ElectricityChargeRatePerSec * (float)delta), 0.0f, 100.0f);
        CityTemperatureC -= HeatDecayRatePerSec * (float)delta;

        EmitUpdate();
    }

    // Μετατροπή μπαταρίας σε θερμότητα πόλης
    public void SurgeElectricHeat(float batteryDrainPercent, float heatBonus)
    {
        float actualDrain = Mathf.Min(BatteryChargePercent, batteryDrainPercent);
        BatteryChargePercent -= actualDrain;
        CityTemperatureC += heatBonus * (actualDrain / batteryDrainPercent);
        EmitUpdate();
    }

    // Προσθήκη / Αφαίρεση Scrap
    public bool TryConsumeScrap(int amount)
    {
        if (MetalScrapCount >= amount)
        {
            MetalScrapCount -= amount;
            EmitUpdate();
            return true;
        }
        return false;
    }

    public void AddScrap(int amount)
    {
        MetalScrapCount += amount;
        EmitUpdate();
    }

    private void EmitUpdate()
    {
        OnResourcesUpdated?.Invoke(CoalStockpileKg, BatteryChargePercent, CityTemperatureC, MetalScrapCount);
    }
}