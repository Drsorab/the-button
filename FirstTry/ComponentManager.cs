using Godot;
using System.Collections.Generic;

public partial class ComponentManager : Node
{
    private readonly List<ComponentData> _equippedComponents = new();

    // Αναφορές στα υπόλοιπα συστήματα
    private Main _main;
    private ReactorYieldCalculator _yieldCalc;
    private ResourceManager _resManager;
    private NixieTimer _timer;

    // Αποθήκευση των εργοστασιακών (Base) τιμών
    private double _baseCycleTime;
    private double _baseGracePeriod;
    private double _baseSweetSpotStart;
    private double _baseSweetSpotEnd;
    private float _baseSweetSpotBonus;
    private float _baseDecaySteepness;
    private float _baseElectricityToHeatRate;
    private double _baseSecondsReducedPerHeat;
    private int _baseMaxMetal, _baseMinMetal;
    private int _baseMaxElectricity, _baseMinElectricity;
    private int _baseMaxHeat, _baseMinHeat;

    private bool _isInitialized = false;

    // Αρχικοποίηση και καταγραφή των Base τιμών
    public void Initialize(Main main, ReactorYieldCalculator yieldCalc, ResourceManager resManager, NixieTimer timer)
    {
        _main = main;
        _yieldCalc = yieldCalc;
        _resManager = resManager;
        _timer = timer;

        // Καταγραφή Base τιμών
        _baseCycleTime = main.DefaultCycleTime;
        _baseGracePeriod = timer.GracePeriod;
        _baseSweetSpotStart = yieldCalc.SweetSpotStart;
        _baseSweetSpotEnd = yieldCalc.SweetSpotEnd;
        _baseSweetSpotBonus = yieldCalc.SweetSpotBonusMultiplier;
        _baseDecaySteepness = yieldCalc.DecaySteepness;

        _baseElectricityToHeatRate = resManager.ElectricityToHeatRate;
        _baseSecondsReducedPerHeat = resManager.SecondsReducedPerHeat;

        _baseMaxMetal = yieldCalc.MaxMetalScraps;
        _baseMinMetal = yieldCalc.MinMetalScraps;
        _baseMaxElectricity = yieldCalc.MaxElectricity;
        _baseMinElectricity = yieldCalc.MinElectricity;
        _baseMaxHeat = yieldCalc.MaxHeat;
        _baseMinHeat = yieldCalc.MinHeat;

        _isInitialized = true;
    }

    // 1. Τοποθέτηση Component
    public void EquipComponent(ComponentData component)
    {
        if (component == null) return;

        _equippedComponents.Add(component);
        GD.Print($"[EQUIP] Τοποθετήθηκε: {component.DisplayName}");

        // Αυτόματος επανυπολογισμός stats
        ApplyStats();
    }

    // 2. Αφαίρεση Component
    public void UnequipComponent(ComponentData component)
    {
        if (_equippedComponents.Remove(component))
        {
            GD.Print($"[UNEQUIP] Αφαιρέθηκε: {component.DisplayName}");

            // Αυτόματος επανυπολογισμός stats
            ApplyStats();
        }
    }

    // 3. Εφαρμογή αθροιστικών modifiers ξεκινώντας ΠΑΝΤΑ από τα Base Values
    private void ApplyStats()
    {
        if (!_isInitialized) return;

        double finalGrace = _baseGracePeriod;
        double finalCycle = _baseCycleTime;
        double finalSweetStart = _baseSweetSpotStart;
        double finalSweetEnd = _baseSweetSpotEnd;
        float finalSweetBonus = _baseSweetSpotBonus;
        float finalDecay = _baseDecaySteepness;

        float finalElecToHeat = _baseElectricityToHeatRate;
        double finalSecPerHeat = _baseSecondsReducedPerHeat;

        int finalMaxMetal = _baseMaxMetal;
        int finalMinMetal = _baseMinMetal;
        int finalMaxElec = _baseMaxElectricity;
        int finalMinElec = _baseMinElectricity;
        int finalMaxHeat = _baseMaxHeat;
        int finalMinHeat = _baseMinHeat;

        // Άθροιση όλων των εξαρτημάτων
        foreach (var comp in _equippedComponents)
        {
            finalGrace += comp.GracePeriodModifier;
            finalCycle += comp.DefaultCycleTimeModifier;
            finalSweetStart += comp.SweetSpotStartModifier;
            finalSweetEnd += comp.SweetSpotEndModifier;
            finalSweetBonus += comp.SweetSpotBonusMultiplierMod;
            finalDecay += comp.DecaySteepnessModifier;

            finalElecToHeat += comp.ElectricityToHeatRateMod;
            finalSecPerHeat += comp.SecondsReducedPerHeatMod;

            finalMaxMetal += comp.MaxMetalScrapsMod;
            finalMinMetal += comp.MinMetalScrapsMod;
            finalMaxElec += comp.MaxElectricityMod;
            finalMinElec += comp.MinElectricityMod;
            finalMaxHeat += comp.MaxHeatMod;
            finalMinHeat += comp.MinHeatMod;
        }

        // Ανάθεση στα συστήματα
        _main.DefaultCycleTime = finalCycle;
        _timer.GracePeriod = finalGrace;

        _yieldCalc.GracePeriodAfterZero = finalGrace;
        _yieldCalc.SweetSpotStart = finalSweetStart;
        _yieldCalc.SweetSpotEnd = finalSweetEnd;
        _yieldCalc.SweetSpotBonusMultiplier = finalSweetBonus;
        _yieldCalc.DecaySteepness = finalDecay;
        _yieldCalc.MaxMetalScraps = finalMaxMetal;
        _yieldCalc.MinMetalScraps = finalMinMetal;
        _yieldCalc.MaxElectricity = finalMaxElec;
        _yieldCalc.MinElectricity = finalMinElec;
        _yieldCalc.MaxHeat = finalMaxHeat;
        _yieldCalc.MinHeat = finalMinHeat;

        _resManager.ElectricityToHeatRate = finalElecToHeat;
        _resManager.SecondsReducedPerHeat = finalSecPerHeat;

        GD.Print($"[STATS UPDATED] Active Components: {_equippedComponents.Count} | MaxMetal: {finalMaxMetal} | ElecToHeat: {finalElecToHeat} | GracePeriod: {finalGrace}s");
    }
}