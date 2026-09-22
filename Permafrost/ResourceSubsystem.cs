using Godot;
using System;

public partial class ResourceSubsystem : Node
{
    public event Action<float, float, float, int> OnResourcesUpdated;
    public event Action<float> OnCoalStockpileChanged;

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
        if (CoalStockpileKg > 0.0f)
        {
            CoalStockpileKg = Mathf.Max(0.0f, CoalStockpileKg - (CoalBurnRatePerSec * (float)delta));
            OnCoalStockpileChanged?.Invoke(CoalStockpileKg);
        }

        BatteryChargePercent = Mathf.Clamp(BatteryChargePercent + (ElectricityChargeRatePerSec * (float)delta), 0.0f, 100.0f);
        CityTemperatureC -= HeatDecayRatePerSec * (float)delta;

        EmitUpdate();
    }

    // Κατανάλωση κάρβουνου με έλεγχο επάρκειας (χρησιμοποιείται από το SkeletonCrewSubsystem)
    public bool TryConsumeCoal(float amount)
    {
        if (amount <= 0.0f) return true;

        if (CoalStockpileKg >= amount)
        {
            CoalStockpileKg -= amount;
            OnCoalStockpileChanged?.Invoke(CoalStockpileKg);
            EmitUpdate();
            return true;
        }

        // Εάν δεν επαρκεί όλη η ποσότητα, μηδενίζουμε το απόθεμα
        CoalStockpileKg = 0.0f;
        OnCoalStockpileChanged?.Invoke(CoalStockpileKg);
        EmitUpdate();
        return false;
    }

    // Προσθήκη κάρβουνου στο απόθεμα (χρησιμοποιείται από το Minigame εξόρυξης)
    public void AddCoal(float amount)
    {
        if (amount <= 0.0f) return;

        CoalStockpileKg += amount;
        OnCoalStockpileChanged?.Invoke(CoalStockpileKg);
        EmitUpdate();
    }

    // Άμεση μείωση θερμοκρασίας (χρησιμοποιείται ως ποινή αστοχίας στο Minigame)
    public void DrainCoreHeat(float tempDrop)
    {
        CityTemperatureC -= Mathf.Abs(tempDrop);
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