using Godot;
using System;
using System.Resources;


public partial class Main : Control
{
    [Export] public double DefaultCycleTime = 10800.0;
    [Export] public double MinCycleTime = 10.0;
    private NixieTimer _nixieTimer;
    private ResetButton _resetButton;
    private FuseRack _fuseRack;
    private ResourceManager _resourceManager;
    private ReactorYieldCalculator _yieldCalculator;
    private ComponentBayUI _componentBay;
    private ComponentManager _componentManager;
    private ShiftClock _shiftClock;
    private ReactorCore _reactorCore;
    private DebugOverlay _debugOverlay;

    public override void _Ready()
    {
        // 1. Εύρεση Nodes από τη σκηνή (βεβαιώσου για τα ακριβή ονόματα)
        _nixieTimer = GetNodeOrNull<NixieTimer>("TimerDisplay");
        _resetButton = GetNodeOrNull<ResetButton>("ButtonAssembly/HatchButton");
        _fuseRack = GetNodeOrNull<FuseRack>("FuseRack");
        _componentBay = GetNodeOrNull<ComponentBayUI>("ComponentBay");
        _shiftClock = GetNode<ShiftClock>("ShiftClock");
        // Έλεγχος αν βρέθηκαν τα κρίσιμα UI nodes
        if (_nixieTimer == null)
            GD.PrintErr("[ERROR] Δεν βρέθηκε το NixieTimer! Έλεγξε το path 'TimerDisplay'.");

        if (_resetButton == null)
            GD.PrintErr("[ERROR] Δεν βρέθηκε το ResetButton! Έλεγξε το path 'ButtonAssembly/HatchButton'.");

        if (_componentBay == null)
            GD.PrintErr("[ERROR] Δεν βρέθηκε το ComponentBayUI! Έλεγξε αν το όνομα είναι 'ComponentBay' και αν έχει το ComponentBayUI.cs attached.");

        // 2. Δημιουργία των Managers
        _resourceManager = new ResourceManager();
        AddChild(_resourceManager);

        _yieldCalculator = new ReactorYieldCalculator();
        AddChild(_yieldCalculator);

        _componentManager = new ComponentManager();
        AddChild(_componentManager);

        _reactorCore = new ReactorCore();
        AddChild(_reactorCore);

        // Σύνδεση σημάτων για debugging
        _reactorCore.TemperatureChanged += (temp) =>
        {
            // Εδώ ενημερώνεις μελλοντικά το μανόμετρο/θερμόμετρο στην οθόνη
        };

        _reactorCore.CoreDamageTaken += (health, dmg) =>
        {
            GD.PrintErr($"[DANGER] Υποθερμία! Health: {health:F1}% (-{dmg:F2})");
        };

        _shiftClock.OnShiftModeChanged += OnShiftModeChanged;
        _shiftClock.ForceEvaluate();

        _debugOverlay = GetNode<DebugOverlay>("DebugOverlay");
        _debugOverlay.Initialize(_reactorCore, _resourceManager, _yieldCalculator);

        // 3. Αρχικοποίηση (Μόνο αν τα nodes υπάρχουν)
        if (_nixieTimer != null && _yieldCalculator != null && _resourceManager != null)
        {
            _componentManager.Initialize(this, _yieldCalculator, _resourceManager, _nixieTimer);
        }

        if (_componentBay != null && _componentManager != null && _resourceManager != null)
        {
            _componentBay.Initialize(_componentManager, _resourceManager);
        }

        // 4. Σύνδεση Events
        if (_nixieTimer != null && _yieldCalculator != null)
        {
            _nixieTimer.CoverOpenThreshold = _yieldCalculator.AvailableWindowBeforeZero;
            _nixieTimer.GracePeriod = _yieldCalculator.GracePeriodAfterZero;

            if (_resetButton != null)
            {
                _resetButton.OnButtonPressed += OnManualReset;
                _nixieTimer.OnCoverThresholdChanged += OnCoverThresholdChanged;
            }

            _nixieTimer.OnTimerZeroReached += CheckFuseBackup;
            _nixieTimer.OnGracePeriodExpired += TriggerSystemFailure;

            _nixieTimer.StartCountdown(DefaultCycleTime);
        }
    }

    private void OnCoverThresholdChanged(bool canOpen)
    {
        // Αν είμαστε σε Skeleton Crew, το κουμπί δεν ανοίγει ποτέ
        if (_reactorCore != null && _reactorCore.CurrentMode == ShiftMode.SkeletonCrew)
        {
            _resetButton.SetEnabled(false);
            return;
        }

        // Κατά τη βάρδια, ακολουθεί κανονικά το threshold του χρονομέτρου
        _resetButton.SetEnabled(canOpen);
    }

    private void OnShiftModeChanged(ShiftMode newMode)
    {
        // 1. Ενημέρωση θερμοδυναμικού πυρήνα
        _reactorCore.SetShiftMode(newMode);

        if (newMode == ShiftMode.SkeletonCrew)
        {
            _nixieTimer.SetSkeletonCrewMode();
            _resetButton.SetEnabled(false);
            GD.Print("[SHIFT] Skeleton Crew ενεργό -> Πάγωμα σε 00:00 & κλείδωμα.");
        }
        else
        {
            // 1. Βασική διάρκεια κύκλου (π.χ. 600s = 10 λεπτά)
            //double baseCycleDuration = 600.0;

            // 2. Υπολογισμός μείωσης χρόνου από το Heat (SecondsReducedPerHeat, π.χ. 30s ανά Heat)
            double timeReduction = _resourceManager.Heat * _resourceManager.SecondsReducedPerHeat;

            // 3. Τελικός χρόνος (με κατώτατο όριο ασφαλείας π.χ. 15s ώστε να προλαβαίνει να πατηθεί)
            double finalCycleDuration = Math.Max(15.0, DefaultCycleTime - timeReduction);

            // 4. Άμεση εκκίνηση
            _nixieTimer.ResumeActiveShift(finalCycleDuration);

            GD.Print($"[SHIFT] Έναρξη Active Shift! Εκκίνηση timer στα {finalCycleDuration:F1}s (Μείωση: -{timeReduction:F1}s από {_resourceManager.Heat} Heat).");
        }
    }

    private void OnManualReset()
    {
        if (_reactorCore.CurrentMode == ShiftMode.SkeletonCrew) return;
        double pressTime = _nixieTimer.CurrentTime;

        float coreEfficiency = _reactorCore.CurrentHealth / _reactorCore.MaxCoreHealth;

        ResourceYield yield = _yieldCalculator.CalculateYield(pressTime, coreEfficiency);
        _resourceManager.AddResources(yield);

        // 1. Μετατροπή Electricity σε Heat
        _resourceManager.ConvertElectricityToHeat();

        // 2. Κατανάλωση Heat για μείωση χρόνου επόμενου κύκλου
        int heatProduced = _resourceManager.Heat;
        double timeReduction = _resourceManager.ConsumeHeatForTimeReduction();
        double nextCycleDuration = Math.Max(MinCycleTime, DefaultCycleTime - timeReduction);

        // 3. Διοχέτευση της παραχθείσας θερμότητας στον φυσικό πυρήνα
        _reactorCore.InjectHeatFromReset(heatProduced);

        // 4. Επανεκκίνηση χρονομέτρου
        _nixieTimer.StartCountdown(nextCycleDuration);
        _resetButton.SetEnabled(false);
    }

    private void CheckFuseBackup()
    {
        if (_fuseRack != null && _fuseRack.TryConsumeFuse(out double addedTime))
        {
            _nixieTimer.StartCountdown(addedTime);
            _resetButton.SetEnabled(false);
        }
    }

    private void TriggerSystemFailure()
    {
        _nixieTimer.SetFailureDisplay();
        _resetButton.SetEnabled(false);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            return;

        switch (keyEvent.Keycode)
        {
            // 1. Αγορά νέας θέσης ασφάλειας (Socket)
            case Key.S:
                if (_fuseRack.TryBuySlot())
                    GD.Print("[DEBUG] Αγοράστηκε νέα θέση (Slot)!");
                break;

            // 2. Αγορά / Τοποθέτηση νέας ασφάλειας (Fuse)
            case Key.F:
                if (_fuseRack.TryBuyFuse())
                    GD.Print("[DEBUG] Αγοράστηκε και τοποθετήθηκε νέα ασφάλεια (Fuse)!");
                break;

            // 3. Προσομοίωση πατήματος του κεντρικού κουμπιού (Space)
            case Key.Space:
                if (!_resetButton.Disabled)
                {
                    OnManualReset();
                    GD.Print("[DEBUG] Χειροκίνητο Reset μέσω Space!");
                }
                break;

            // 4. Δοκιμή: Τοποθέτηση Normal Component στο πρώτο ελεύθερο slot (0-7)
            case Key.C:
                ComponentData normalItem = GD.Load<ComponentData>("res://Components/valve.tres");
                if (normalItem != null)
                {
                    if (_componentBay.AutoEquip(normalItem))
                        GD.Print($"[DEBUG] Τοποθετήθηκε {normalItem.DisplayName} σε Normal Slot!");
                }
                else
                {
                    GD.PrintErr("[DEBUG] Δεν βρέθηκε το αρχείο res://Components/valve.tres");
                }
                break;
            case Key.M:
                _resourceManager.AddResources(new ResourceYield { MetalScraps = 100, Electricity = 50, Heat = 20 });
                GD.Print("[DEBUG] Προστέθηκαν +100 Metal, +50 Elec, +20 Heat!");
                break;

            // 5. Δοκιμή: Τοποθέτηση Premium Component στο πρώτο ελεύθερο premium slot (0-1)
            case Key.P:
                ComponentData premiumItem = GD.Load<ComponentData>("res://Components/premiumValve.tres");

                if (_componentBay.AutoEquip(premiumItem))
                    GD.Print("[DEBUG] Τοποθετήθηκε Premium Component!");
                break;
            case Key.B:
                var nextMode = _reactorCore.CurrentMode == ShiftMode.ActiveShift
                    ? ShiftMode.SkeletonCrew
                    : ShiftMode.ActiveShift;
                _reactorCore.SetShiftMode(nextMode);
                break;

            // Δοκιμή: Ρίξε απότομα τη θερμοκρασία για να δεις τη ζημιά υποθερμίας
            case Key.H:
                _reactorCore.CurrentTemperature = 80.0f; // Κάτω από το όριο των 95°C
                GD.Print("[DEBUG] Η θερμοκρασία έπεσε στους 80°C!");
                break;
            case Key.N:
                _shiftClock.SimulateMidnightTransition();
                GD.Print("[DEBUG] Προσομοίωση Μεσάνυχτων: Ενεργοποιήθηκε η αυριανή βάρδια!");
                break;
        }
    }


}