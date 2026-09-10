using Godot;
using System;

public partial class TopBarView : MarginContainer
{
    private Label _coalLabel;
    private Label _batteryLabel;
    private Label _tempLabel;
    private Label _scrapLabel;

    public override void _Ready()
    {
        // Εύρεση των Labels για κάθε πόρο
        _coalLabel = GetNode<Label>("ResourcesHBox/CoalDisplay/Value");
        _batteryLabel = GetNode<Label>("ResourcesHBox/BatteryDisplay/Value");
        _tempLabel = GetNode<Label>("ResourcesHBox/TempDisplay/Value");
        _scrapLabel = GetNode<Label>("ResourcesHBox/ScrapDisplay/Value");
    }

    // Ενημέρωση όλων των ενδείξεων στην οθόνη
    public void UpdateDisplay(float coalKg, float batteryPercent, float tempC, int scrapCount)
    {
        if (_coalLabel != null)
            _coalLabel.Text = $"{Mathf.RoundToInt(coalKg)} kg";

        if (_batteryLabel != null)
            _batteryLabel.Text = $"{Mathf.RoundToInt(batteryPercent)}%";

        if (_tempLabel != null)
        {
            _tempLabel.Text = $"{Mathf.RoundToInt(tempC)}°C";
            // Αλλαγή χρώματος σε παγωμένο μπλε αν η θερμοκρασία πέσει επικίνδυνα
            _tempLabel.Modulate = tempC <= -60.0f ? new Color(0.4f, 0.7f, 1.0f) : Colors.White;
        }

        if (_scrapLabel != null)
            _scrapLabel.Text = scrapCount.ToString();
    }
}