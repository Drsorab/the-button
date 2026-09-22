using Godot;
using System;

public partial class SkeletonCrewConsole : Control
{
    [ExportGroup("Subsystem Dependencies")]
    [Export] private SkeletonCrewSubsystem _skeletonCrewSubsystem;
    [Export] private AcousticDrillMinigame _drillMinigame;

    [ExportGroup("Header Labels")]
    [Export] private Label _countdownLabel;
    [Export] private Label _crewAllocationLabel;

    [ExportGroup("Draft Damper Controls")]
    [Export] private Button _btnBanked;
    [Export] private Button _btnIdle;
    [Export] private Button _btnSurge;
    [Export] private Label _fuelEstimateLabel;

    [ExportGroup("Directive Controls")]
    [Export] private Button _btnDirNone;
    [Export] private Button _btnDirSoot;
    [Export] private Button _btnDirPatch;
    [Export] private Label _directiveStatusLabel;

    [ExportGroup("Drill Modal")]
    [Export] private Control _drillModal;
    [Export] private Button _launchDrillBtn;
    [Export] private Button _closeDrillBtn;
    [Export] private Label _drillStatusLabel;
    [Export] private Godot.Collections.Array<Button> _valveButtons = new();

    private double _secondsUntilHandover = 0.0;
    private bool _isStandbyActive = false;
    private int _lastDisplayedSecond = -1;
    public override void _Ready()
    {
        // Damper Buttons
        if (_btnBanked != null) _btnBanked.Pressed += () => _skeletonCrewSubsystem?.SetDraftStance(DraftStance_old.BankedEmbers);
        if (_btnIdle != null) _btnIdle.Pressed += () => _skeletonCrewSubsystem?.SetDraftStance(DraftStance_old.SteadyIdle);
        if (_btnSurge != null) _btnSurge.Pressed += () => _skeletonCrewSubsystem?.SetDraftStance(DraftStance_old.ThermalSurge);

        // Directive Buttons
        if (_btnDirNone != null) _btnDirNone.Pressed += () => _skeletonCrewSubsystem?.SetDirective(NightDirective.None);
        if (_btnDirSoot != null) _btnDirSoot.Pressed += () => _skeletonCrewSubsystem?.SetDirective(NightDirective.SootScouring);
        if (_btnDirPatch != null) _btnDirPatch.Pressed += () => _skeletonCrewSubsystem?.SetDirective(NightDirective.ColdPatching);

        // Drill Launcher & Modal
        if (_launchDrillBtn != null) _launchDrillBtn.Pressed += OpenDrillModal;
        if (_closeDrillBtn != null) _closeDrillBtn.Pressed += CloseDrillModal;

        // Wire Valve Click Inputs
        for (int i = 0; i < _valveButtons.Count; i++)
        {
            int valveIndex = i;
            _valveButtons[i].Pressed += () => _drillMinigame?.SubmitValveInput(valveIndex);
        }

        // Subsystem Event Subscriptions
        if (_skeletonCrewSubsystem != null)
        {
            _skeletonCrewSubsystem.OnDraftStanceChanged += UpdateDraftButtons;
            _skeletonCrewSubsystem.OnDirectiveChanged += UpdateDirectiveButtons;
            _skeletonCrewSubsystem.OnNightStaffingUpdated += UpdateStaffingDisplay;
        }

        if (_drillMinigame != null)
        {
            _drillMinigame.OnValveHighlighted += HandleValveHighlight;
            _drillMinigame.OnRoundStarted += HandleDrillRoundStarted;
            _drillMinigame.OnStateChanged += HandleDrillStateChanged;
            _drillMinigame.OnMinigameWon += (rounds, coal) => CloseDrillModal(); //autoclose when won
        }

        Visible = false;
        if (_drillModal != null) _drillModal.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!_isStandbyActive || _secondsUntilHandover <= 0) return;

        _secondsUntilHandover = Math.Max(0, _secondsUntilHandover - delta);

        // Ενημέρωση του UI μόνο όταν αλλάζει το ολόκληρο δευτερόλεπτο
        int currentSecond = (int)_secondsUntilHandover;
        if (currentSecond != _lastDisplayedSecond)
        {
            _lastDisplayedSecond = currentSecond;

            int hours = currentSecond / 3600;
            int minutes = (currentSecond % 3600) / 60;
            int seconds = currentSecond % 60;

            if (_countdownLabel != null)
            {
                _countdownLabel.Text = $"HANDOVER IN: {hours:D2}h {minutes:D2}m {seconds:D2}s";
            }
        }
    }

    public void ActivateStandby(double remainingSeconds)
    {
        _secondsUntilHandover = remainingSeconds;
        _isStandbyActive = true;
        Visible = true;

        _skeletonCrewSubsystem?.RecalculateAvailableHands();
        UpdateDraftButtons(_skeletonCrewSubsystem?.CurrentStance ?? DraftStance_old.SteadyIdle);
        UpdateDirectiveButtons(_skeletonCrewSubsystem?.CurrentDirective ?? NightDirective.None);
    }

    public void DeactivateStandby()
    {
        _isStandbyActive = false;
        Visible = false;
        if (_drillModal != null) _drillModal.Visible = false;
    }

    private void UpdateDraftButtons(DraftStance_old stance)
    {
        if (_btnBanked != null) _btnBanked.Disabled = (stance == DraftStance_old.BankedEmbers);
        if (_btnIdle != null) _btnIdle.Disabled = (stance == DraftStance_old.SteadyIdle);
        if (_btnSurge != null) _btnSurge.Disabled = (stance == DraftStance_old.ThermalSurge);
    }

    private void UpdateDirectiveButtons(NightDirective directive)
    {
        if (_btnDirNone != null) _btnDirNone.Disabled = (directive == NightDirective.None);
        if (_btnDirSoot != null) _btnDirSoot.Disabled = (directive == NightDirective.SootScouring);
        if (_btnDirPatch != null) _btnDirPatch.Disabled = (directive == NightDirective.ColdPatching);

        if (_directiveStatusLabel != null)
        {
            _directiveStatusLabel.Text = directive == NightDirective.None
                ? "Directives Idle: Quarters Resting"
                : $"Active Directive: {directive}";
        }
    }

    private void UpdateStaffingDisplay(int available, int assigned)
    {
        if (_crewAllocationLabel != null)
        {
            _crewAllocationLabel.Text = $"Night Watch Detachment: {assigned} / {available} Hands Assigned";
        }

        // Lock out drill if crew is below 3
        if (_launchDrillBtn != null)
        {
            _launchDrillBtn.Disabled = (available < 3);
        }
    }

    private void OpenDrillModal()
    {
        if (_drillModal != null)
        {
            _drillModal.Visible = true;
            _drillMinigame?.StartMinigame();
        }
    }

    private void CloseDrillModal()
    {
        if (_drillModal != null) _drillModal.Visible = false;
    }

    private void HandleValveHighlight(int activeIndex)
    {
        for (int i = 0; i < _valveButtons.Count; i++)
        {
            _valveButtons[i].Modulate = (i == activeIndex) ? new Color(2.5f, 2.0f, 0.5f) : Colors.White;
        }
    }

    private void HandleDrillRoundStarted(int round, int total)
    {
        int availableButtons = _drillMinigame.ButtonsPerRound[Mathf.Clamp(round - 1, 0, _drillMinigame.ButtonsPerRound.Count - 1)];
        for (int i = 0; i < _valveButtons.Count; i++)
        {
            _valveButtons[i].Visible = (i < availableButtons);
        }
    }

    private void HandleDrillStateChanged(DrillState state)
    {
        bool allowInput = (state == DrillState.AwaitingInput);
        for (int i = 0; i < _valveButtons.Count; i++)
        {
            _valveButtons[i].Disabled = !allowInput;
        }

        if (_drillStatusLabel != null)
        {
            _drillStatusLabel.Text = state switch
            {
                DrillState.DisplayingPattern => "ACOUSTIC PULSE ACTIVE - OBSERVE SEQUENCE",
                DrillState.AwaitingInput => "INPUT SEQUENCE NOW",
                DrillState.LockoutCooldown => "BLOWOUT! PRESSURE RECOVERY IN PROGRESS",
                _ => "DRILL DORMANT"
            };
        }
    }
}