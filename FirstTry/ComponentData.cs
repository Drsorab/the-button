using Godot;

[GlobalClass]
public partial class ComponentData : Resource
{
    [ExportGroup("Identity & Visuals")]
    [Export] public string ItemId = "gear_valve_01";
    [Export] public string DisplayName = "Brass Pressure Valve";
    [Export(PropertyHint.MultilineText)] public string Description = "Improves heat dissipation and expands grace period.";
    [Export] public ComponentType Type = ComponentType.Normal;
    [Export] public Texture2D Icon;

    [ExportGroup("Cost to Purchase")]
    [Export] public int MetalScrapCost = 50;
    [Export] public int ElectricityCost = 0;
    [Export] public int HeatCost = 0;

    [ExportGroup("Timing Modifiers (Additive)")]
    [Export] public double GracePeriodModifier = 0.0;          // π.χ. +2.0s
    [Export] public double DefaultCycleTimeModifier = 0.0;     // π.χ. +10.0s
    [Export] public double SweetSpotStartModifier = 0.0;       // π.χ. +1.5s
    [Export] public double SweetSpotEndModifier = 0.0;
    [Export] public float SweetSpotBonusMultiplierMod = 0.0f;  // π.χ. +0.15 (+15%)
    [Export] public float DecaySteepnessModifier = 0.0f;       // π.χ. -0.5 (πιο ομαλή πτώση)

    [ExportGroup("Conversion Modifiers (Additive)")]
    [Export] public float ElectricityToHeatRateMod = 0.0f;     // π.χ. +0.25 (1.25x conversion)
    [Export] public double SecondsReducedPerHeatMod = 0.0;     // π.χ. +5.0s ανά heat

    [ExportGroup("Resource Yield Modifiers (Additive)")]
    [Export] public int MaxMetalScrapsMod = 0;
    [Export] public int MinMetalScrapsMod = 0;
    [Export] public int MaxElectricityMod = 0;
    [Export] public int MinElectricityMod = 0;
    [Export] public int MaxHeatMod = 0;
    [Export] public int MinHeatMod = 0;
}