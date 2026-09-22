using Godot;
using System;

// Οι 3 καταστάσεις λειτουργίας του λέβητα κατά τη διάρκεια της νύχτας
public enum DraftStance_old
{
    BankedEmbers,  // Χαμηλή κατανάλωση κάρβουνου, επικίνδυνη πτώση θερμοκρασίας
    SteadyIdle,    // Ισορροπημένη κατανάλωση και συντήρηση θερμοκρασίας
    ThermalSurge   // Υψηλή κατανάλωση κάρβουνου, προστασία από παγωνιά
}

// Οι νυχτερινές οδηγίες συντήρησης
public enum NightDirective
{
    None,
    SootScouring,     // Καθαρισμός αιθάλης: Επιβραδύνει τη συσσώρευση πίεσης το πρωί
    ColdPatching      // Επισκευή κάσας: Καταναλώνει scrap για επαναφορά της ακεραιότητας
}

public partial class SkeletonCrewSubsystem : Node
{
    // Events για ενημέρωση του UI και των υποσυστημάτων
    public event Action<DraftStance_old> OnDraftStanceChanged;
    public event Action<NightDirective> OnDirectiveChanged;
    public event Action<int, int> OnNightStaffingUpdated; // (Διαθέσιμα χέρια, Ανατεθειμένα)
    public event Action<string> OnNightLogEmitted;

    // Αναφορές στα κοινόχρηστα υποσυστήματα
    [Export] private PopulationSubsystem_old _populationSubsystem;
    [Export] private ResourceSubsystem _resourceSubsystem;
    [Export] private PressureSubsystem _pressureSubsystem;

    // Παράμετροι κατανάλωσης και συντήρησης
    [Export] public float BaseCoalBurnRatePerSec = 0.25f; // Βασική κατανάλωση κάρβουνου ανά δευτερόλεπτο
    [Export] public float PatchingScrapCostPerHour = 2.0f; // Κόστος scrap ανά ώρα για επιδιορθώσεις
    [Export] public float PatchingRepairRatePerHour = 1.5f; // Ποσοστό επισκευής κελύφους ανά ώρα

    // Τρέχουσα κατάσταση της νυχτερινής βάρδιας
    public DraftStance_old CurrentStance { get; private set; } = DraftStance_old.SteadyIdle;
    public NightDirective CurrentDirective { get; private set; } = NightDirective.None;

    public int AvailableNightHands { get; private set; } = 0;
    public int AssignedNightHands { get; private set; } = 0;

    // Τροποποιητής συσσώρευσης πίεσης για την πρωινή βάρδια (καθαρισμός αιθάλης)
    public float MorningPressureDelayBonus { get; private set; } = 0.0f;

    // Χρονόμετρα για τα περιοδικά ticks
    private double _tickTimer = 0.0;
    private const double TickInterval = 1.0; // Εκτέλεση υπολογισμών ανά δευτερόλεπτο
    private DateTime _lastSimulationUtc;

    public override void _Ready()
    {
        _lastSimulationUtc = DateTime.UtcNow;
        RecalculateAvailableHands();
    }

    // Υπολογισμός διαθέσιμων εργατών (20% των υγιών εργατών βάσης)
    public void RecalculateAvailableHands()
    {
        if (_populationSubsystem == null)
        {
            AvailableNightHands = 0;
            return;
        }

        // Αυστηρή άντληση μόνο από τους υγιείς εργάτες της πρωινής βάρδιας
        AvailableNightHands = Math.Max(0, (int)Math.Floor(_populationSubsystem.BaseHealthyWorkforce * 0.20f));
        EvaluateStaffingConstraints();
    }

    // 1. Ρύθμιση της θέσης του διαφράγματος καύσης (Draft Stance)
    public bool SetDraftStance(DraftStance_old newStance)
    {
        // Απαιτούνται τουλάχιστον 2 εργάτες για τη ρύθμιση του λέβητα
        if (AvailableNightHands < 2)
        {
            CurrentStance = DraftStance_old.BankedEmbers;
            OnNightLogEmitted?.Invoke("ΑΝΕΠΑΡΚΕΣ ΠΡΟΣΩΠΙΚΟ: Ο λέβητας κλειδώθηκε αναγκαστικά σε Banked Embers.");
            OnDraftStanceChanged?.Invoke(CurrentStance);
            return false;
        }

        CurrentStance = newStance;
        OnDraftStanceChanged?.Invoke(CurrentStance);
        OnNightLogEmitted?.Invoke($"ΡΥΘΜΙΣΤΗΣ ΚΑΥΣΗΣ: Η κατάσταση τέθηκε σε {CurrentStance}.");
        return true;
    }

    // 2. Ανάθεση νυχτερινής οδηγίας συντήρησης
    public bool SetDirective(NightDirective newDirective)
    {
        if (newDirective == NightDirective.None)
        {
            CurrentDirective = NightDirective.None;
            OnDirectiveChanged?.Invoke(CurrentDirective);
            UpdateAssignedCrew();
            return true;
        }

        // Έλεγχος διαθέσιμου προσωπικού ανάλογα με την οδηγία
        int requiredCrew = newDirective == NightDirective.ColdPatching ? 4 : 3;
        int currentFreeCrew = AvailableNightHands - 2; // Κρατάμε πάντα 2 εργάτες για τον λέβητα

        if (currentFreeCrew < requiredCrew)
        {
            OnNightLogEmitted?.Invoke($"ΑΔΥΝΑΜΙΑ ΑΝΑΘΕΣΗΣ: Απαιτούνται {requiredCrew} ελεύθεροι εργάτες για την οδηγία {newDirective}.");
            return false;
        }

        CurrentDirective = newDirective;
        OnDirectiveChanged?.Invoke(CurrentDirective);
        UpdateAssignedCrew();
        OnNightLogEmitted?.Invoke($"ΝΥΧΤΕΡΙΝΗ ΟΔΗΓΙΑ: Ενεργοποιήθηκε η διαδικασία {CurrentDirective}.");
        return true;
    }

    private void UpdateAssignedCrew()
    {
        int crewCount = 2; // Ελάχιστη επάνδρωση για τον λέβητα
        if (CurrentDirective == NightDirective.ColdPatching) crewCount += 4;
        else if (CurrentDirective == NightDirective.SootScouring) crewCount += 3;

        AssignedNightHands = Math.Min(crewCount, AvailableNightHands);
        OnNightStaffingUpdated?.Invoke(AvailableNightHands, AssignedNightHands);
    }

    // 3. Επαλήθευση επάνδρωσης σε περίπτωση απωλειών ή ασθενειών
    private void EvaluateStaffingConstraints()
    {
        if (AvailableNightHands < 2)
        {
            CurrentStance = DraftStance_old.BankedEmbers;
            CurrentDirective = NightDirective.None;
            OnDraftStanceChanged?.Invoke(CurrentStance);
            OnDirectiveChanged?.Invoke(CurrentDirective);
        }
        else if (CurrentDirective != NightDirective.None)
        {
            int required = CurrentDirective == NightDirective.ColdPatching ? 6 : 5; // 2 λέβητας + απαίτηση οδηγίας
            if (AvailableNightHands < required)
            {
                OnNightLogEmitted?.Invoke($"ΑΚΥΡΩΣΗ ΟΔΗΓΙΑΣ: Το προσωπικό μειώθηκε κάτω από το ελάχιστο όριο.");
                CurrentDirective = NightDirective.None;
                OnDirectiveChanged?.Invoke(CurrentDirective);
            }
        }

        UpdateAssignedCrew();
    }

    // 4. Κύριος κύκλος εκτέλεσης σε πραγματικό χρόνο
    public void ProcessNightTick(double delta)
    {
        _tickTimer += delta;
        if (_tickTimer < TickInterval) return;

        double elapsed = _tickTimer;
        _tickTimer = 0.0;
        _lastSimulationUtc = DateTime.UtcNow;

        ExecuteSimulationStep(elapsed);
    }

    // Ενιαία λογική προσομοίωσης (χρησιμοποιείται τόσο realtime όσο και στο catch-up)
    private void ExecuteSimulationStep(double elapsedSeconds)
    {
        if (_resourceSubsystem == null) return;

        // Α. Υπολογισμός κατανάλωσης κάρβουνου και ρύθμιση θερμοκρασίας
        float coalMultiplier = 1.0f;
        float targetTemp = -52.0f;

        switch (CurrentStance)
        {
            case DraftStance_old.BankedEmbers:
                coalMultiplier = 0.4f;
                targetTemp = -75.0f;
                break;
            case DraftStance_old.SteadyIdle:
                coalMultiplier = 1.0f;
                targetTemp = -52.0f;
                break;
            case DraftStance_old.ThermalSurge:
                coalMultiplier = 1.8f;
                targetTemp = -36.0f;
                break;
        }

        float coalToConsume = (float)(BaseCoalBurnRatePerSec * coalMultiplier * elapsedSeconds);
        bool hasCoal = _resourceSubsystem.TryConsumeCoal(coalToConsume);

        if (!hasCoal)
        {
            // Εάν τελειώσει το κάρβουνο, η θερμοκρασία κατακρημνίζεται στους -90°C
            targetTemp = -90.0f;
            OnNightLogEmitted?.Invoke("ΚΡΙΣΙΜΟ: Εξαντλήθηκε το κάρβουνο! Ο λέβητας σβήνει!");
        }

        // Σταδιακή προσαρμογή της θερμοκρασίας πόλης προς το στόχο
        _resourceSubsystem.CityTemperatureC = Mathf.MoveToward(_resourceSubsystem.CityTemperatureC, targetTemp, (float)(0.5f * elapsedSeconds));

        // Β. Εκτέλεση ενεργής νυχτερινής οδηγίας
        ExecuteDirective(elapsedSeconds);
    }

    private void ExecuteDirective(double elapsedSeconds)
    {
        if (CurrentDirective == NightDirective.ColdPatching)
        {
            // Υπολογισμός αναλογικού κόστους scrap και αποκατάστασης κάσας
            float scrapCost = (float)((PatchingScrapCostPerHour / 3600.0) * elapsedSeconds);
            if (_resourceSubsystem.MetalScrapCount >= scrapCost && _resourceSubsystem.TryConsumeScrap((int)Math.Ceiling(scrapCost)))
            {
                float repairAmount = (float)((PatchingRepairRatePerHour / 3600.0) * elapsedSeconds);
                _pressureSubsystem?.RestoreIntegrity(repairAmount);
            }
            else
            {
                OnNightLogEmitted?.Invoke("ΟΔΗΓΙΑ ΣΤΑΜΑΤΗΣΕ: Ανεπάρκεια Scrap για επισκευή κελύφους.");
                SetDirective(NightDirective.None);
            }
        }
        else if (CurrentDirective == NightDirective.SootScouring)
        {
            // Αύξηση του μπόνους καθαρισμού αιθάλης (έως 20% μέγιστο)
            float bonusGain = (float)((0.20f / 3600.0) * elapsedSeconds);
            MorningPressureDelayBonus = Mathf.Clamp(MorningPressureDelayBonus + bonusGain, 0.0f, 0.20f);
        }
    }

    // 5. Λογική εξομοίωσης χρόνου αποσύνδεσης (Offline Delta Catch-Up)
    public void ProcessOfflineCatchUp(DateTime lastSavedUtc)
    {
        DateTime now = DateTime.UtcNow;
        if (now <= lastSavedUtc) return;

        double totalOfflineSeconds = (now - lastSavedUtc).TotalSeconds;
        GD.Print($"[SKELETON CREW] Εκτέλεση προσομοίωσης αποσύνδεσης για {totalOfflineSeconds:F1} δευτερόλεπτα.");

        // Εκτέλεση προσομοίωσης σε διακριτά βήματα για σταθερότητα
        const double stepSize = 60.0; // Βήματα του 1 λεπτού
        while (totalOfflineSeconds > 0)
        {
            double currentStep = Math.Min(totalOfflineSeconds, stepSize);
            ExecuteSimulationStep(currentStep);
            totalOfflineSeconds -= currentStep;
        }

        _lastSimulationUtc = now;
        RecalculateAvailableHands();
    }

    // 6. Πρωινή παράδοση (Reset για την επόμενη βάρδια)
    public float ConcludeNightShiftAndGetPressureModifier()
    {
        float bonus = MorningPressureDelayBonus;
        MorningPressureDelayBonus = 0.0f;
        CurrentDirective = NightDirective.None;
        CurrentStance = DraftStance_old.SteadyIdle;
        return bonus;
    }
}