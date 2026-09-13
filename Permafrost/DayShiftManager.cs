using Godot;
using System;

public partial class DayShiftManager : Control
{
    // Χρονικά διαστήματα κύκλων (σε δευτερόλεπτα)
    private const double BaseCycleSeconds = 108.0 * 60.0;
    private const double SurgeCycleSeconds = 72.0 * 60.0;
    private const double ScrapCycleSeconds = 45.0 * 60.0;

    // Αναφορές στους κόμβους του Scene Tree
    [Export] private NixieTimer _nixieTimer;
    [Export] private ShiftClock _shiftClock;
    [Export] private PressureSubsystem _pressureSubsystem;
    [Export] private ResourceSubsystem _resourceSubsystem;
    [Export] private FuseRack _fuseRack;
    [Export] private DecompressionLever _decompressionLever;
    [Export] private TopBarView _topBarView;
    [Export] private HotWeldOverdrive _hotWeldOverdrive;
    [Export] private PressureGaugeView _pressureGaugeView;
    [Export] private PopulationSubsystem _populationSubsystem;

    [Export] private ShiftDebriefModal _debriefModal;
    [Export] private SkeletonCrewConsole _skeletonConsole;
    [Export] private Control _dayShiftConsole; // Ο κύριος πίνακας ημέρας
    private int _cyclesCompletedThisShift = 0;
    private double _shiftElapsedSeconds = 0.0;

    private bool _isShiftActive = false;
    private int _shiftCasualtiesStart = 0;
    public override void _Ready()
    {
        // 2. Σύνδεση με το NixieTimer
        _nixieTimer.OnCoverThresholdChanged += HandleCoverThreshold;
        _nixieTimer.OnTimerZeroReached += HandleTimerZero;
        _nixieTimer.OnGracePeriodExpired += HandleGracePeriodExpired;

        // 3. Σύνδεση με τον μοχλό εκτόνωσης
        _decompressionLever.OnVentTriggered += HandleDecompressionTriggered;
        // 1. Καταγραφή αρχικών απωλειών κατά την εκκίνηση της βάρδιας
        if (_populationSubsystem != null)
        {
            _shiftCasualtiesStart = _populationSubsystem.CasualtiesCount;
            _populationSubsystem.OnCasualtiesOccurred += HandleCasualtiesAlert;
        }

        // 2. Σύνδεση υπερθέρμανσης Overdrive με απώλειες εργατών
        if (_hotWeldOverdrive != null)
        {
            _hotWeldOverdrive.OnOverdriveBlowout += () =>
            {
                // Το σκάσιμο σωληνώσεων ατμού σκοτώνει 2-4 εργάτες
                int killed = GD.RandRange(2, 4);
                GD.Print($"[DISASTER] Έκρηξη ατμού στο Hot-Weld! Απώλειες: {killed} εργάτες.");
                _populationSubsystem?.RecordCasualties(killed);
            };
        }

        // 1. Καταμέτρηση κύκλων εκτόνωσης
        _decompressionLever.OnVentTriggered += (mode) =>
        {
            _cyclesCompletedThisShift++;
        };
        if (_debriefModal != null)
        {
            _debriefModal.OnHandoverConfirmed += TransitionToSkeletonCrew;
        }

        _pressureSubsystem.OnPressureChanged += (currentPressure) =>
        {
            _pressureGaugeView?.UpdatePressure(currentPressure);
        };


        _resourceSubsystem.OnResourcesUpdated += (coal, battery, temp, scrap) =>
        {
            _topBarView.UpdateDisplay(coal, battery, temp, scrap);
        };

        // Αρχικός έλεγχος κατάστασης βάρδιας
        _shiftClock.OnShiftModeChanged += HandleShiftModeChanged;
        _shiftClock.ForceEvaluate();
    }
    public override void _Process(double delta)
    {
        if (!_isShiftActive) return;

        // 1. Επεξεργασία λειτουργίας Overdrive
        _hotWeldOverdrive.ProcessOverdrive(delta, _resourceSubsystem, _pressureSubsystem);

        // 2. Εφαρμογή της επιτάχυνσης χρόνου στο Nixie Timer
        _nixieTimer.TimeScale = _hotWeldOverdrive.TimeDilationMultiplier;

        // 3. Σύνδεση θερμοκρασίας πόλης με την επιβίωση των πολιτών
        // Χρησιμοποιεί την τρέχουσα θερμοκρασία από το ResourceSubsystem
        _populationSubsystem?.ProcessFreezingRisk(delta, _resourceSubsystem.CityTemperatureC);
        _populationSubsystem?.ProcessForcedLaborRisks(delta);
        _populationSubsystem?.ProcessOverdriveHazards(delta, _hotWeldOverdrive.IsActive, _hotWeldOverdrive.StressPercent);
    }
    // Διαχείριση αλλαγής βάρδιας (Active Shift vs. Skeleton Crew)
    private void HandleShiftModeChanged(ShiftMode mode)
    {
        if (mode == ShiftMode.SkeletonCrew && _isShiftActive)
        {
            // Η 8ωρη βάρδια μόλις ολοκληρώθηκε -> Προβολή Αναφοράς (Debrief)
            _isShiftActive = false;
            TriggerEndOfShiftManifest();
        }
        else if (mode == ShiftMode.ActiveShift)
        {
            _isShiftActive = true;
            _cyclesCompletedThisShift = 0;
            _shiftElapsedSeconds = 0.0;
            _skeletonConsole?.DeactivateStandby();
            _dayShiftConsole.Visible = true;

            _resourceSubsystem.SetActive(true);
            _nixieTimer.StartCountdown(BaseCycleSeconds);
            _pressureSubsystem.StartSimulation(BaseCycleSeconds);
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
        // Απόκρυψη του πίνακα ημέρας
        _dayShiftConsole.Visible = false;

        // Ενεργοποίηση της οθόνης Skeleton Crew με υπολειπόμενο χρόνο μέχρι την επόμενη βάρδια
        double secondsUntilTomorrow = _shiftClock.GetSecondsUntilNextActiveShift();
        _skeletonConsole?.ActivateStandby(secondsUntilTomorrow);
    }

    // Άνοιγμα / Κλείσιμο καλύμματος μοχλού όταν φτάσει στο όριο του Nixie
    private void HandleCoverThreshold(bool isOpen)
    {
        if (!_isShiftActive) return;
        _decompressionLever.SetLockedState(!isOpen);
    }

    // Ο χρόνος έφτασε στο 000:00 (Κρίσιμο παράθυρο Grace Period)
    private void HandleTimerZero()
    {
        GD.Print("[ALERT] Πίεση σε κρίσιμο σημείο! Το παράθυρο χάριτος ξεκίνησε!");
    }

    // Το παράθυρο χάριτος έληξε χωρίς εκτόνωση -> Κάψιμο ασφάλειας & επαναφορά
    private void HandleGracePeriodExpired()
    {
        GD.Print("[CRITICAL] Εκπνοή χρόνου! Έλεγχος ασφαλειών...");

        // Προσπάθεια απορρόφησης της υπέρτασης από το FuseRack
        if (_fuseRack.TryConsumeFuse(out double durationBonus))
        {
            // Μερική ζημιά και θερμική απώλεια λόγω του στιγμιαίου blow-out
            _pressureSubsystem.DischargePressure(20.0f, 15.0f);
            _resourceSubsystem.SurgeElectricHeat(50.0f, -10.0f);

            // Επανεκκίνηση με τη χρονική παράταση που έδωσε η ασφάλεια (π.χ. 30 λεπτά)
            _nixieTimer.StartCountdown(durationBonus);
            _pressureSubsystem.StartSimulation(durationBonus);
        }
        else
        {
            // Καμία διαθέσιμη ασφάλεια -> Πλήρης καταστροφή
            HandleTotalCoreMeltdown();
        }
    }

    // Εκτέλεση εκτόνωσης ανάλογα με τη θέση του μοχλού
    private void HandleDecompressionTriggered(VentMode mode)
    {
        double nextCycleDuration = BaseCycleSeconds;

        switch (mode)
        {
            case VentMode.SafeRelease:
                _pressureSubsystem.DischargePressure(10.0f, 2.0f);
                _resourceSubsystem.SurgeElectricHeat(20.0f, 5.0f);
                nextCycleDuration = BaseCycleSeconds;
                break;

            case VentMode.HeatSurge:
                _pressureSubsystem.DischargePressure(10.0f, 6.0f);
                _resourceSubsystem.SurgeElectricHeat(100.0f, 35.0f);
                nextCycleDuration = SurgeCycleSeconds;
                break;

            case VentMode.ScrapScour:
                _pressureSubsystem.DischargePressure(10.0f, 12.0f);
                _resourceSubsystem.AddScrap(15);
                nextCycleDuration = ScrapCycleSeconds;
                break;

            case VentMode.HotWeld:

                    GD.Print("[WARNING] Δεν μπορεις να κανεις discharge οσο εισαι σε HotWeld mode.");
                
                break;
        }

        // Εκκίνηση του επόμενου κύκλου στο Nixie Timer και στον προσομοιωτή πίεσης
        _nixieTimer.StartCountdown(nextCycleDuration);
        _pressureSubsystem.StartSimulation(nextCycleDuration);
    }

    // Καταστροφική διάρρηξη της γεννήτριας (Game Over)
    private void HandleTotalCoreMeltdown()
    {
        GD.Print("[GAME OVER] Πλήρης διάρρηξη γεννήτριας. Η πόλη πάγωσε.");
        _isShiftActive = false;
        _nixieTimer.SetFailureDisplay();
        _pressureSubsystem.StopSimulation();
        _resourceSubsystem.SetActive(false);
        _decompressionLever.SetLockedState(true);

        _populationSubsystem?.RecordCasualties(25);
    }

    private void HandleCasualtiesAlert(int count)
    {
        GD.Print($"[CASUALTY REPORT] Επιβεβαιώθηκαν {count} νέες απώλειες από το ψύχος/ατύχημα.");
        // Μελλοντικά: screen shake, κόκκινο flash ή σειρήνα
    }
}