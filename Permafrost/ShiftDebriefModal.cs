using Godot;
using System;

public partial class ShiftDebriefModal : PanelContainer
{
    public event Action OnHandoverConfirmed;

    private Label _titleLabel;
    private Label _metricsLabel;
    private Button _handoverButton;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("DebriefBox/TitleLabel");
        _metricsLabel = GetNode<Label>("DebriefBox/MetricsLabel");
        _handoverButton = GetNode<Button>("DebriefBox/HandoverButton");

        _handoverButton.Pressed += () =>
        {
            Visible = false;
            OnHandoverConfirmed?.Invoke();
        };

        Visible = false;
    }

    public void DisplayManifest(ShiftDebriefData data)
    {
        int hours = (int)(data.ShiftDurationSeconds / 3600);
        int minutes = (int)((data.ShiftDurationSeconds % 3600) / 60);
        int effPercent = Mathf.RoundToInt(data.FinalEfficiency * 100.0f);

        _metricsLabel.Text =
            $"[GENERATOR LOG - SECTOR 4]\n\n" +
            $"• Shift Duration: {hours:D2}h {minutes:D2}m\n" +
            $"• Pressure Cycles Handled: {data.CyclesCompleted}\n" +
            $"• Hull Integrity: {Mathf.RoundToInt(data.FinalIntegrity)}%\n" +
            $"• Coal Stockpile: {Mathf.RoundToInt(data.RemainingCoalKg)} kg\n" +
            $"• Battery Charge: {Mathf.RoundToInt(data.RemainingBattery)}%\n" +
            $"• Scrap Salvaged: {data.ScrapSalvaged} units\n\n" +
            $"[PERSONNEL & WORKFORCE CENSUS]\n\n" +
            $"• Operational Crew: {data.ActiveWorkforce} / {data.TotalWorkforce} (Eff: {effPercent}%)\n" +
            $"• Lightly Sick (24h): {data.SickCount}\n" +
            $"• Gravely Sick (48h): {data.GravelySickCount}\n" +
            $"• Civilians / Dependents: {data.GeneralPopulation}\n" +
            $"• Casualties This Shift: {data.CasualtiesThisShift} souls\n\n" +
            $"STATUS: Shift concluded. Transferring control to Skeleton Watch.";

        Visible = true;
    }
}