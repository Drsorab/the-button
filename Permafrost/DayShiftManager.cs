using Godot;
using System;

public partial class DayShiftManager : Node
{
    // Χρονικά διαστήματα κύκλων (σε δευτερόλεπτα)
    private const double BaseCycleSeconds = 108.0 * 60.0;
    private const double SurgeCycleSeconds = 72.0 * 60.0;
    private const double ScrapCycleSeconds = 45.0 * 60.0;
    private const double HotWeldCycleSeconds = 30.0 * 60.0;
    private const double EmergencyPenaltyCycleSeconds = 30.0 * 60.0;

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
    private bool _isShiftActive = false;

    public override void _Ready()
    {
        // 1. Σύνδεση με το ShiftClock
        _shiftClock.OnShiftModeEvaluated += HandleShiftModeChanged;

        // 2. Σύνδεση με το NixieTimer
        _nixieTimer.OnCoverThresholdChanged += HandleCoverThreshold;
        _nixieTimer.OnTimerZeroReached += HandleTimerZero;
        _nixieTimer.OnGracePeriodExpired += HandleGracePeriodExpired;

        // 3. Σύνδεση με τον μοχλό εκτόνωσης
        _decompressionLever.OnVentTriggered += HandleDecompressionTriggered;
        _pressureSubsystem.OnPressureChanged += (currentPressure) =>
        {
            _pressureGaugeView?.UpdatePressure(currentPressure);
        };


        _resourceSubsystem.OnResourcesUpdated += (coal, battery, temp, scrap) =>
        {
            _topBarView.UpdateDisplay(coal, battery, temp, scrap);
        };

        // Αρχικός έλεγχος κατάστασης βάρδιας
        _shiftClock.ForceEvaluate();
    }
    public override void _Process(double delta)
    {
        if (!_isShiftActive) return;

        // 1. Επεξεργασία λειτουργίας Overdrive
        _hotWeldOverdrive.ProcessOverdrive(delta, _resourceSubsystem, _pressureSubsystem);

        // 2. Εφαρμογή της επιτάχυνσης χρόνου στο Nixie Timer
        _nixieTimer.TimeScale = _hotWeldOverdrive.TimeDilationMultiplier;
    }
    // Διαχείριση αλλαγής βάρδιας (Active Shift vs. Skeleton Crew)
    private void HandleShiftModeChanged(ShiftMode mode)
    {
        if (mode == ShiftMode.ActiveShift)
        {
            GD.Print("[DAY SHIFT] Έναρξη ενεργής βάρδιας!");
            _isShiftActive = true;
            _resourceSubsystem.SetActive(true);
            _nixieTimer.ResumeActiveShift(BaseCycleSeconds);
            _pressureSubsystem.StartSimulation(BaseCycleSeconds);
        }
        else
        {
            GD.Print("[DAY SHIFT] Μετάβαση σε Skeleton Crew.");
            _isShiftActive = false;
            _resourceSubsystem.SetActive(false);
            _pressureSubsystem.StopSimulation();
            _decompressionLever.SetLockedState(true);
            _nixieTimer.SetSkeletonCrewMode();
        }
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
    }
}