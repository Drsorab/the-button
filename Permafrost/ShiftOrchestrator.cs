using Godot;
using System;

public partial class ShiftOrchestrator : Control
{
    // Χρονικά διαστήματα κύκλων (σε δευτερόλεπτα)
    private const double BaseCycleSeconds = 108.0 * 60.0;
    private const double SurgeCycleSeconds = 72.0 * 60.0;
    private const double ScrapCycleSeconds = 45.0 * 60.0;

    [ExportGroup("Subsystems")]
    [Export] private ShiftClock _shiftClock;
    [Export] private PressureSubsystem _pressureSubsystem;
    [Export] private ResourceSubsystem _resourceSubsystem;
    [Export] private PopulationSubsystem_old _populationSubsystem;
    [Export] private SkeletonCrewSubsystem _skeletonCrewSubsystem;

    [ExportGroup("Consoles & Views")]
    [Export] private Control _dayShiftConsole;
    [Export] private SkeletonCrewConsole _skeletonConsole;
    [Export] private DiagnosticTerminal _diagnosticTerminal;
    [Export] private ShiftDebriefModal _debriefModal;
    [Export] private TopBarView _topBarView;
    [Export] private PressureGaugeView _pressureGaugeView;

    [ExportGroup("Day Shift Mechanics")]
    [Export] private NixieTimer _nixieTimer;
    [Export] private FuseRack _fuseRack;
    [Export] private DecompressionLever _decompressionLever;
    [Export] private HotWeldOverdrive _hotWeldOverdrive;

    private int _cyclesCompletedThisShift = 0;
    private double _shiftElapsedSeconds = 0.0;
    private bool _isShiftActive = false;
    private int _shiftCasualtiesStart = 0;

    public override void _Ready()
    {
        InitializeDayShiftMechanics();
        InitializeSubsystems();
        InitializeLoggingPipeline();

        // Σύνδεση επιβεβαίωσης αναφοράς βάρδιας για μετάβαση στο Skeleton Crew
        if (_debriefModal != null)
        {
            _debriefModal.OnHandoverConfirmed += TransitionToSkeletonCrew;
        }

        // Αρχικός έλεγχος κατάστασης βάρδιας βάσει πραγματικού χρόνου
        if (_shiftClock != null)
        {
            _shiftClock.OnShiftModeChanged += HandleShiftModeChanged;
            _shiftClock.ForceEvaluate();
        }
    }

    public override void _Process(double delta)
    {
        if (_isShiftActive)
        {
            // === 8-HOUR ACTIVE DAY SHIFT ===
            _shiftElapsedSeconds += delta;

            // 1. Επεξεργασία Overdrive με εφαρμογή συντελεστή απόδοσης
            float efficiency = _populationSubsystem != null ? _populationSubsystem.EfficiencyFactor : 1.0f;
            _hotWeldOverdrive?.ProcessOverdrive(delta, _resourceSubsystem, _pressureSubsystem, efficiency);

            // 2. Εφαρμογή επιτάχυνσης χρόνου στο Nixie Timer
            if (_nixieTimer != null && _hotWeldOverdrive != null)
            {
                _nixieTimer.TimeScale = _hotWeldOverdrive.TimeDilationMultiplier;
            }

            // 3. Επεξεργασία κινδύνων πληθυσμού ημέρας (παγωνιά, καταναγκαστική εργασία, hazards)
            if (_populationSubsystem != null && _resourceSubsystem != null)
            {
                _populationSubsystem.ProcessFreezingRisk(delta, _resourceSubsystem.CityTemperatureC);
                _populationSubsystem.ProcessForcedLaborRisks(delta);

                bool isOverdriveActive = _hotWeldOverdrive != null && _hotWeldOverdrive.IsActive;
                float overdriveStress = _hotWeldOverdrive != null ? _hotWeldOverdrive.StressPercent : 0.0f;
                _populationSubsystem.ProcessOverdriveHazards(delta, isOverdriveActive, overdriveStress);
            }
        }
        else
        {
            // === 16-HOUR SKELETON CREW NIGHT WATCH ===
            // 1. Κεντρική εκτέλεση προσομοίωσης νύχτας (κατανάλωση κάρβουνου, οδηγίες)
            _skeletonCrewSubsystem?.ProcessNightTick(delta);

            // 2. Έλεγχος παγωνιάς για τους κοιμώμενους κατοίκους και ασθενείς
            if (_populationSubsystem != null && _resourceSubsystem != null)
            {
                _populationSubsystem.ProcessFreezingRisk(delta, _resourceSubsystem.CityTemperatureC);
            }
        }
    }

    // Αρχικοποίηση μηχανισμών της ημερήσιας βάρδιας
    private void InitializeDayShiftMechanics()
    {
        if (_nixieTimer != null)
        {
            _nixieTimer.OnCoverThresholdChanged += HandleCoverThreshold;
            _nixieTimer.OnTimerZeroReached += HandleTimerZero;
            _nixieTimer.OnGracePeriodExpired += HandleGracePeriodExpired;
        }

        if (_decompressionLever != null)
        {
            _decompressionLever.OnVentTriggered += HandleDecompressionTriggered;
            _decompressionLever.OnVentTriggered += (mode) => _cyclesCompletedThisShift++;
        }

        if (_hotWeldOverdrive != null)
        {
            _hotWeldOverdrive.OnOverdriveBlowout += () =>
            {
                int killed = GD.RandRange(2, 4);
                LogMessage($"[DISASTER] Έκρηξη ατμού στο Hot-Weld! Απώλειες: {killed} εργάτες.");
                _populationSubsystem?.RecordCasualties(killed);
            };
        }
    }

    // Αρχικοποίηση υποσυστημάτων και UI views
    private void InitializeSubsystems()
    {
        if (_pressureSubsystem != null && _pressureGaugeView != null)
        {
            _pressureSubsystem.OnPressureChanged += (currentPressure) =>
            {
                _pressureGaugeView.UpdatePressure(currentPressure);
            };
        }

        if (_resourceSubsystem != null && _topBarView != null)
        {
            _resourceSubsystem.OnResourcesUpdated += (coal, battery, temp, scrap) =>
            {
                _topBarView.UpdateDisplay(coal, battery, temp, scrap);
            };
        }

        if (_populationSubsystem != null)
        {
            _shiftCasualtiesStart = _populationSubsystem.CasualtiesCount;
            _populationSubsystem.OnCasualtiesOccurred += (count) =>
            {
                LogMessage($"[CASUALTY REPORT] Επιβεβαιώθηκαν {count} νέες απώλειες.");
            };
        }
    }

    // Ενιαίος αγωγός καταγραφής συμβάντων στο Diagnostic Terminal
    private void InitializeLoggingPipeline()
    {
        if (_diagnosticTerminal == null) return;

        // Νυχτερινά μηνύματα
        if (_skeletonCrewSubsystem != null)
        {
            _skeletonCrewSubsystem.OnNightLogEmitted += LogMessage;
        }

        // Καταγραφή εκκίνησης
        LogMessage("[SYSTEM] Το κεντρικό σύστημα ελέγχου γεννήτριας αρχικοποιήθηκε.");
    }

    private void LogMessage(string message)
    {
        _diagnosticTerminal?.AppendLogEntry(message);
    }

    // Διαχείριση εναλλαγής μεταξύ Day Shift και Skeleton Crew
    private void HandleShiftModeChanged(ShiftMode mode)
    {
        if (mode == ShiftMode.SkeletonCrew && _isShiftActive)
        {
            _isShiftActive = false;
            LogMessage("[SHIFT] Λήξη 8ωρης Ημερήσιας Βάρδιας. Έναρξη καταγραφής αναφοράς.");
            TriggerEndOfShiftManifest();
        }
        else if (mode == ShiftMode.ActiveShift)
        {
            _isShiftActive = true;
            _cyclesCompletedThisShift = 0;
            _shiftElapsedSeconds = 0.0;

            _skeletonConsole?.DeactivateStandby();
            if (_dayShiftConsole != null) _dayShiftConsole.Visible = true;

            _resourceSubsystem?.SetActive(true);

            // Έλεγχος για μπόνους καθαρισμού αιθάλης από το Skeleton Crew
            float pressureDelayBonus = _skeletonCrewSubsystem != null
                ? _skeletonCrewSubsystem.ConcludeNightShiftAndGetPressureModifier()
                : 0.0f;

            double startingCycle = BaseCycleSeconds * (1.0 + pressureDelayBonus);

            _nixieTimer?.StartCountdown(startingCycle);
            _pressureSubsystem?.StartSimulation(startingCycle);

            LogMessage($"[SHIFT] Έναρξη Ημερήσιας Βάρδιας. Κύκλος Nixie: {startingCycle / 60.0:F0} λεπτά.");
        }
    }

    private void TriggerEndOfShiftManifest()
    {
        int currentTotalCasualties = _populationSubsystem != null ? _populationSubsystem.CasualtiesCount : 0;
        int casualtiesThisShift = currentTotalCasualties - _shiftCasualtiesStart;

        var debriefData = new ShiftDebriefData
        {
            ShiftDurationSeconds = _shiftElapsedSeconds,
            CyclesCompleted = _cyclesCompletedThisShift,
            FinalIntegrity = _pressureSubsystem != null ? _pressureSubsystem.CasingIntegrity : 100.0f,
            RemainingCoalKg = _resourceSubsystem != null ? _resourceSubsystem.CoalStockpileKg : 0.0f,
            RemainingBattery = _resourceSubsystem != null ? _resourceSubsystem.BatteryChargePercent : 0.0f,
            ScrapSalvaged = _resourceSubsystem != null ? _resourceSubsystem.MetalScrapCount : 0,
            ActiveWorkforce = _populationSubsystem != null ? _populationSubsystem.EffectiveWorkforce : 0,
            TotalWorkforce = _populationSubsystem != null ? _populationSubsystem.TotalWorkforce : 0,
            SickCount = _populationSubsystem != null ? _populationSubsystem.SickWorkers : 0,
            GravelySickCount = _populationSubsystem != null ? _populationSubsystem.GravelySickWorkers : 0,
            GeneralPopulation = _populationSubsystem != null ? _populationSubsystem.GeneralPopulation : 0,
            CasualtiesThisShift = casualtiesThisShift,
            FinalEfficiency = _populationSubsystem != null ? _populationSubsystem.EfficiencyFactor : 1.0f
        };

        _debriefModal?.DisplayManifest(debriefData);
    }

    private void TransitionToSkeletonCrew()
    {
        if (_dayShiftConsole != null) _dayShiftConsole.Visible = false;

        double secondsUntilTomorrow = _shiftClock != null ? _shiftClock.GetSecondsUntilNextActiveShift() : 16.0 * 3600.0;
        _skeletonConsole?.ActivateStandby(secondsUntilTomorrow);
        LogMessage("[SHIFT] Ενεργοποίηση Skeleton Crew. Μετάβαση σε κατάσταση αναμονής.");
    }

    private void HandleCoverThreshold(bool isOpen)
    {
        if (!_isShiftActive) return;
        _decompressionLever?.SetLockedState(!isOpen);
    }

    private void HandleTimerZero()
    {
        LogMessage("[CRITICAL] Ο χρόνος μηδενίστηκε! Παράθυρο χάριτος σε εξέλιξη!");
    }

    private void HandleGracePeriodExpired()
    {
        LogMessage("[CRITICAL] Εκπνοή χρόνου! Έλεγχος ασφαλειών...");

        if (_fuseRack != null && _fuseRack.TryConsumeFuse(out double durationBonus))
        {
            LogMessage("[WARNING] Κάηκε ασφάλεια! Απορρόφηση υπέρτασης.");
            _pressureSubsystem?.DischargePressure(20.0f, 15.0f);
            _resourceSubsystem?.SurgeElectricHeat(50.0f, -10.0f);

            _nixieTimer?.StartCountdown(durationBonus);
            _pressureSubsystem?.StartSimulation(durationBonus);
        }
        else
        {
            HandleTotalCoreMeltdown();
        }
    }

    private void HandleDecompressionTriggered(VentMode_old mode)
    {
        double nextCycleDuration = BaseCycleSeconds;

        switch (mode)
        {
            case VentMode_old.SafeRelease:
                _pressureSubsystem?.DischargePressure(10.0f, 2.0f);
                _resourceSubsystem?.SurgeElectricHeat(20.0f, 5.0f);
                nextCycleDuration = BaseCycleSeconds;
                LogMessage("[VENT] Ασφαλής εκτόνωση ατμού ολοκληρώθηκε.");
                break;

            case VentMode_old.HeatSurge:
                _pressureSubsystem?.DischargePressure(10.0f, 6.0f);
                _resourceSubsystem?.SurgeElectricHeat(100.0f, 35.0f);
                nextCycleDuration = SurgeCycleSeconds;
                LogMessage("[VENT] Εκτόνωση θερμικού κύματος στην πόλη.");
                break;

            case VentMode_old.ScrapScour:
                _pressureSubsystem?.DischargePressure(10.0f, 12.0f);
                _resourceSubsystem?.AddScrap(15);
                nextCycleDuration = ScrapCycleSeconds;
                LogMessage("[VENT] Συλλογή μετάλλων (Scrap Scour) ολοκληρώθηκε.");
                break;

            case VentMode_old.HotWeld:
                LogMessage("[WARNING] Αδυναμία εκτόνωσης κατά τη λειτουργία HotWeld.");
                break;
        }

        _nixieTimer?.StartCountdown(nextCycleDuration);
        _pressureSubsystem?.StartSimulation(nextCycleDuration);
    }

    private void HandleTotalCoreMeltdown()
    {
        LogMessage("[DISASTER] Ολική κατάρρευση πυρήνα! Η γεννήτρια έσβησε.");
        _isShiftActive = false;
        _nixieTimer?.SetFailureDisplay();
        _pressureSubsystem?.StopSimulation();
        _resourceSubsystem?.SetActive(false);
        _decompressionLever?.SetLockedState(true);
        _populationSubsystem?.RecordCasualties(25);
    }
}