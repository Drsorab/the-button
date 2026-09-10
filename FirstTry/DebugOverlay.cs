using Godot;
using System;

public partial class DebugOverlay : CanvasLayer
{
    // C# references (όχι exported nodes)
    public ReactorCore Core { get; private set; }
    public ResourceManager ResManager { get; private set; }
    public ReactorYieldCalculator YieldCalculator { get; private set; }

    private Label _labelCore;
    private Label _labelResources;
    private Label _labelYield;
    private bool _isVisible = true;

    public override void _Ready()
    {
        _labelCore = GetNode<Label>("MarginContainer/HBoxContainer/PanelCore/LabelCore");
        _labelResources = GetNode<Label>("MarginContainer/HBoxContainer/PanelResources/LabelResources");
        _labelYield = GetNode<Label>("MarginContainer/HBoxContainer/PanelYield/LabelYield");
    }
    public void Initialize(ReactorCore core, ResourceManager resManager, ReactorYieldCalculator yieldCalculator)
    {
        Core = core;
        ResManager = resManager;
        YieldCalculator = yieldCalculator;
    }
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.F1 || key.Keycode == Key.Quoteleft)
            {
                _isVisible = !_isVisible;
                Visible = _isVisible;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        UpdateCorePanel();
        UpdateResourcesPanel();
        UpdateYieldPanel();
    }

    // 1. Πάνελ Πυρήνα & Θερμοδυναμικής
    private void UpdateCorePanel()
    {
        if (Core == null)
        {
            _labelCore.Text = "[CORE DATA]\n(ReactorCore not linked)";
            return;
        }

        string tempWarning = Core.CurrentTemperature < Core.CriticalTempThreshold ? " [CRITICAL!]" : "";

        _labelCore.Text =
            $"=== THERMAL CORE ===\n" +
            $"Temp: {Core.CurrentTemperature:F1}°C{tempWarning}\n" +
            $"Ambient: {Core.AmbientTemperature:F1}°C\n" +
            $"Thermal Cap (C_th): {Core.ThermalCapacity:F0}\n" +
            $"Heat Loss (k): {Core.HeatLossCoefficient:F2}\n" +
            $"--------------------\n" +
            $"Skeleton Heat: {Core.SkeletonPassiveHeat:F1} kW\n" +
            $"Heat / Energy Unit: {Core.HeatPerEnergyUnit:F1} kW\n" +
            $"--------------------\n" +
            $"Critical Limit: {Core.CriticalTempThreshold:F1}°C\n" +
            $"Core Integrity: {Core.MaxCoreHealth:F0}\n" +
            $"DPS Below Limit: -{Core.DamagePerSecondBelowCritical:F2}/s";
    }

    // 2. Πάνελ Πόρων & Συντελεστών
    private void UpdateResourcesPanel()
    {
        if (ResManager == null)
        {
            _labelResources.Text = "[RESOURCES]\n(ResourceManager not linked)";
            return;
        }

        _labelResources.Text =
            $"=== CURRENT INVENTORY ===\n" +
            $"⚙ Metal Scraps: {ResManager.MetalScraps}\n" +
            $"⚡ Electricity:  {ResManager.Electricity}\n" +
            $"🔥 Heat Energy:  {ResManager.Heat}\n" +
            $"--------------------\n" +
            $"=== CONVERSIONS ===\n" +
            $"Elec -> Heat Rate: {ResManager.ElectricityToHeatRate:F2}x\n" +
            $"Sec / Heat Unit:  -{ResManager.SecondsReducedPerHeat:F0}s\n" +
            $"Status: {(ResManager.Electricity >= 1 ? "Active Grid" : "Grid Offline")}";
    }

    // 3. Πάνελ Χρονισμού & Αποδόσεων (Yield)
    private void UpdateYieldPanel()
    {
        if (YieldCalculator == null)
        {
            _labelYield.Text = "[YIELD DATA]\n(YieldCalculator not linked)";
            return;
        }

        _labelYield.Text =
            $"=== TIMING WINDOWS ===\n" +
            $"Unlock Window: -{YieldCalculator.AvailableWindowBeforeZero:F1}s\n" +
            $"Grace Window:  +{YieldCalculator.GracePeriodAfterZero:F1}s\n" +
            $"Sweet Spot:    {YieldCalculator.SweetSpotStart:F1}s to {YieldCalculator.SweetSpotEnd:F1}s\n" +
            $"Bonus Yield:   +{YieldCalculator.SweetSpotBonusMultiplier * 100:F0}%\n" +
            $"--------------------\n" +
            $"=== BASE YIELD RANGES ===\n" +
            $"Scraps: [{YieldCalculator.MinMetalScraps} - {YieldCalculator.MaxMetalScraps}]\n" +
            $"Elec:   [{YieldCalculator.MinElectricity} - {YieldCalculator.MaxElectricity}]\n" +
            $"Heat:   [{YieldCalculator.MinHeat} - {YieldCalculator.MaxHeat}]";
    }
}