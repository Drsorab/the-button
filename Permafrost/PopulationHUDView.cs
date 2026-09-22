using Godot;
using System;

public partial class PopulationHUDView : Control
{
    [Export] private PopulationSubsystem_old _populationSubsystem;

    // HUD Button Elements
    private Button _triggerButton;
    private Label _triggerLabel;

    // Modal Elements
    private PanelContainer _ledgerModal;
    private Label _efficiencyLabel;
    private Label _generalPopLabel;
    private Label _workforceLabel;
    private Label _sickLabel;
    private Label _gravelySickLabel;
    private Label _casualtiesLabel;

    private CheckButton _sickLaborCheck;
    private CheckButton _gravelyLaborCheck;
    private Button _closeButton;

    public override void _Ready()
    {
        // 1. Hooking UI References
        _triggerButton = GetNode<Button>("HUDTriggerButton");
        _triggerLabel = GetNode<Label>("HUDTriggerButton/TriggerLabel");

        _ledgerModal = GetNode<PanelContainer>("LedgerModal");
        _efficiencyLabel = GetNode<Label>("LedgerModal/MarginContainer/VBoxContainer/EfficiencyLabel");
        _generalPopLabel = GetNode<Label>("LedgerModal/MarginContainer/VBoxContainer/GeneralPopLabel");
        _workforceLabel = GetNode<Label>("LedgerModal/MarginContainer/VBoxContainer/WorkforceLabel");
        _sickLabel = GetNode<Label>("LedgerModal/MarginContainer/VBoxContainer/SickLabel");
        _gravelySickLabel = GetNode<Label>("LedgerModal/MarginContainer/VBoxContainer/GravelySickLabel");
        _casualtiesLabel = GetNode<Label>("LedgerModal/MarginContainer/VBoxContainer/CasualtiesLabel");

        _sickLaborCheck = GetNode<CheckButton>("LedgerModal/MarginContainer/VBoxContainer/EmergencyOrders/SickLaborCheckbox");
        _gravelyLaborCheck = GetNode<CheckButton>("LedgerModal/MarginContainer/VBoxContainer/EmergencyOrders/GravelyLaborCheckbox");
        _closeButton = GetNode<Button>("LedgerModal/MarginContainer/VBoxContainer/CloseButton");

        // 2. Signals
        _triggerButton.Pressed += OpenLedger;
        _closeButton.Pressed += CloseLedger;

        _sickLaborCheck.Toggled += (active) =>
        {
            if (_populationSubsystem != null)
                _populationSubsystem.ForcedSickLabor = active;
        };

        _gravelyLaborCheck.Toggled += (active) =>
        {
            if (_populationSubsystem != null)
                _populationSubsystem.ForcedGravelySickLabor = active;
        };

        // 3. Subsystem Binding
        if (_populationSubsystem != null)
        {
            _populationSubsystem.OnPopulationLedgerUpdated += UpdateDisplay;

            // Force initial render immediately on start:
            UpdateDisplay(
                _populationSubsystem.EffectiveWorkforce,
                _populationSubsystem.GeneralPopulation,
                _populationSubsystem.SickWorkers,
                _populationSubsystem.GravelySickWorkers,
                _populationSubsystem.CasualtiesCount,
                _populationSubsystem.EfficiencyFactor
            );
        }

        _ledgerModal.Visible = false;
    }

    private void OpenLedger()
    {
        _ledgerModal.Visible = true;
        _ledgerModal.Modulate = new Color(1, 1, 1, 0);

        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_ledgerModal, "modulate:a", 1.0f, 0.2f);
    }

    private void CloseLedger()
    {
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenProperty(_ledgerModal, "modulate:a", 0.0f, 0.15f);
        tween.TweenCallback(Callable.From(() => _ledgerModal.Visible = false));
    }

    private void UpdateDisplay(int effectiveWorkforce, int generalPop, int sick, int gravelySick, int casualties, float efficiency)
    {
        // Υπολογισμός συνολικού ζωντανού πληθυσμού χωρίς διπλομετρήσεις
        int totalLiving = _populationSubsystem != null
            ? _populationSubsystem.TotalLivingPopulation
            : generalPop + effectiveWorkforce + sick + gravelySick;

        // Ενημέρωση κειμένου στο κουμπί του HUD (π.χ. CREW: 46 / 116)
        _triggerLabel.Text = $"CREW: {effectiveWorkforce} / {totalLiving}";

        // Χρωματική προειδοποίηση κουμπιού ανάλογα με την κατάσταση
        if (gravelySick > 0 || casualties > 0)
        {
            _triggerButton.Modulate = new Color(1.5f, 0.5f, 0.5f, 1.0f); // Κόκκινο για θανάτους ή σοβαρά τραύματα
        }
        else if (sick > 0)
        {
            _triggerButton.Modulate = new Color(1.3f, 1.1f, 0.5f, 1.0f); // Κίτρινο για απλούς τραυματισμούς
        }
        else
        {
            _triggerButton.Modulate = Colors.White;
        }

        // Ενημέρωση της αναλυτικής λίστας στο Ledger Modal
        int effPercent = Mathf.RoundToInt(efficiency * 100.0f);
        _efficiencyLabel.Text = $"Grid Efficiency: {effPercent}% (Baseline: 45 Hands)";
        _efficiencyLabel.Modulate = efficiency < 1.0f ? new Color(1.0f, 0.4f, 0.4f) : new Color(0.4f, 1.0f, 0.4f);

        _generalPopLabel.Text = $"• General Population (Dependents): {generalPop}";
        _workforceLabel.Text = $"• Active Generator Crew: {effectiveWorkforce}";
        _sickLabel.Text = $"• Lightly Sick: {sick} (24h Recovery)";
        _gravelySickLabel.Text = $"• Gravely Sick: {gravelySick} (48h Stabilization)";
        _casualtiesLabel.Text = $"• Casualties Incurred: {casualties} souls";
    }
}