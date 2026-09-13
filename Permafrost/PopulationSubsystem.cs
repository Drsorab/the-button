using Godot;
using System;
using System.Collections.Generic;

public partial class PopulationSubsystem : Node
{
    // (ActiveWorkforce, GeneralPop, Sick, GravelySick, Dead, Efficiency)
    public event Action<int, int, int, int, int, float> OnPopulationLedgerUpdated;
    public event Action<int> OnCasualtiesOccurred;
    public event Action<int> OnWorkersSickened;
    public event Action<int> OnWorkersGravelyInjured;

    // --- Population Pools ---
    [Export] public int GeneralPopulation { get; private set; } = 70;
    [Export] public int TotalWorkforce { get; private set; } = 50;
    [Export] public int CasualtiesCount { get; private set; } = 0;

    // Timestamp queues (UTC real-world recovery targets)
    private readonly List<DateTime> _sickRecoveryQueue = new();
    private readonly List<DateTime> _gravelyStabilizeQueue = new();

    public int SickWorkers => _sickRecoveryQueue.Count;
    public int GravelySickWorkers => _gravelyStabilizeQueue.Count;

    // --- Forced Labor Emergency Toggles ---
    [Export] public bool ForcedSickLabor { get; set; } = false;
    [Export] public bool ForcedGravelySickLabor { get; set; } = false;

    // Πραγματικοί εργάτες που δουλεύουν ενεργά
    public int BaseHealthyWorkforce => Math.Max(0, TotalWorkforce - SickWorkers - GravelySickWorkers);

    public int EffectiveWorkforce
    {
        get
        {
            int total = BaseHealthyWorkforce;
            if (ForcedSickLabor) total += SickWorkers;
            if (ForcedGravelySickLabor) total += GravelySickWorkers;
            return Math.Max(0, total);
        }
    }

    public int TotalLivingPopulation => GeneralPopulation + TotalWorkforce;

    // --- Efficiency Tuning ---
    [Export] public int OptimalWorkforce = 45;
    [Export] public float MinEfficiencyCap = 0.25f;
    [Export] public float MaxEfficiencyCap = 1.50f;

    public float EfficiencyFactor
    {
        get
        {
            if (OptimalWorkforce <= 0) return 1.0f;
            float rawEfficiency = (float)EffectiveWorkforce / OptimalWorkforce;
            return Mathf.Clamp(rawEfficiency, MinEfficiencyCap, MaxEfficiencyCap);
        }
    }

    // --- Timers ---
    private float _freezeCheckTimer = 0.0f;
    private const float FreezeIntervalSec = 5.0f;

    private float _forcedLaborCheckTimer = 0.0f;
    private const float ForcedLaborIntervalSec = 4.0f;

    private float _hazardCheckTimer = 0.0f;
    private const float HazardIntervalSec = 3.0f;

    private float _recoveryCheckTimer = 0.0f;
    private const float RecoveryCheckIntervalSec = 5.0f;

    [Export] public double MaturationIntervalDays = 3.0;
    private double _maturationProgressDays = 0.0;

    public override void _Ready()
    {
        EmitLedgerUpdate();
    }

    public override void _Process(double delta)
    {
        _recoveryCheckTimer += (float)delta;
        if (_recoveryCheckTimer >= RecoveryCheckIntervalSec)
        {
            _recoveryCheckTimer = 0.0f;
            ProcessMedicalRecoveries();
        }
    }

    // 1. Επεξεργασία Ανάρρωσης βάσει Real-World Timestamps
    public void ProcessMedicalRecoveries()
    {
        DateTime now = DateTime.UtcNow;
        bool stateChanged = false;

        // Βήμα 1: Οι Gravely Sick μετά από 48 ώρες σταθεροποιούνται σε Sick (χρειάζονται άλλες 24 ώρες)
        for (int i = _gravelyStabilizeQueue.Count - 1; i >= 0; i--)
        {
            if (now >= _gravelyStabilizeQueue[i])
            {
                _gravelyStabilizeQueue.RemoveAt(i);
                _sickRecoveryQueue.Add(now.AddHours(24));
                stateChanged = true;
                GD.Print("[MEDIC] Ένας Gravely Sick σταθεροποιήθηκε σε Sick (απομένουν 24 ώρες).");
            }
        }

        // Βήμα 2: Οι Sick μετά από 24 ώρες αναρρώνουν πλήρως
        int recoveredCount = 0;
        for (int i = _sickRecoveryQueue.Count - 1; i >= 0; i--)
        {
            if (now >= _sickRecoveryQueue[i])
            {
                _sickRecoveryQueue.RemoveAt(i);
                recoveredCount++;
                stateChanged = true;
            }
        }

        if (recoveredCount > 0)
        {
            GD.Print($"[MEDIC] {recoveredCount} εργάτες ανάρρωσαν πλήρως και επέστρεψαν στο Workforce!");
        }

        if (stateChanged)
        {
            EmitLedgerUpdate();
        }
    }

    // 2. Εξέλιξη Ασθενειών βάσει Θερμοκρασίας Πόλης
    public void ProcessFreezingRisk(double delta, float cityTempC)
    {
        _freezeCheckTimer += (float)delta;
        if (_freezeCheckTimer < FreezeIntervalSec) return;
        _freezeCheckTimer = 0.0f;

        // Κρύο (-60°C έως -75°C): Υγιείς -> Sick, και Sick -> Gravely Sick
        if (cityTempC <= -60.0f && cityTempC > -75.0f)
        {
            if (SickWorkers > 0)
            {
                DegradeSickToGravely(1);
            }
            else if (BaseHealthyWorkforce > 0)
            {
                AddSick(1);
            }
        }
        // Πολικό Ψύχος (<= -75°C): Gravely Sick -> Θάνατος, Sick -> Gravely Sick
        else if (cityTempC <= -75.0f)
        {
            if (GravelySickWorkers > 0)
            {
                RecordCasualties(1);
            }
            else if (SickWorkers > 0)
            {
                DegradeSickToGravely(1);
            }
            else if (BaseHealthyWorkforce > 0)
            {
                AddSick(1);
            }
        }
    }

    // 3. Έλεγχος Κινδύνων Αναγκαστικής Εργασίας (Forced Labor)
    public void ProcessForcedLaborRisks(double delta)
    {
        if (!ForcedSickLabor && !ForcedGravelySickLabor) return;

        _forcedLaborCheckTimer += (float)delta;
        if (_forcedLaborCheckTimer < ForcedLaborIntervalSec) return;
        _forcedLaborCheckTimer = 0.0f;

        if (ForcedSickLabor && SickWorkers > 0)
        {
            if (GD.Randf() < 0.18f)
            {
                DegradeSickToGravely(1);
                GD.Print("[COLLAPSE] Ένας άρρωστος εργάτης κατέρρευσε στη βάρδια (έγινε Gravely Sick)!");
            }
        }

        if (ForcedGravelySickLabor && GravelySickWorkers > 0)
        {
            if (GD.Randf() < 0.25f)
            {
                GD.Print("[FATALITY] Βαριά άρρωστος εργάτης πέθανε εν ώρα αναγκαστικής εργασίας!");
                RecordCasualties(1);
            }
        }
    }

    // 4. Ακραία Γεγονότα: Overdrive, Εκρήξεις, Ατυχήματα
    public void ProcessOverdriveHazards(double delta, bool isOverdriveActive, float stressPercent)
    {
        if (!isOverdriveActive || EffectiveWorkforce <= 0) return;

        _hazardCheckTimer += (float)delta;
        if (_hazardCheckTimer < HazardIntervalSec) return;
        _hazardCheckTimer = 0.0f;

        float injuryChance = 0.05f + ((stressPercent / 100.0f) * 0.50f);
        if (GD.Randf() < injuryChance)
        {
            if (stressPercent > 80.0f)
            {
                if (GD.Randf() < 0.35f)
                {
                    GD.Print("[CATASTROPHE] Θανατηφόρο ρήγμα ατμού!");
                    RecordCasualties(1);
                }
                else
                {
                    AddGravelySick(1);
                    GD.Print("[CRITICAL ACCIDENT] Εργάτης υπέστη βαριά εγκαύματα/ακρωτηριασμό!");
                }
            }
            else
            {
                AddSick(1);
                GD.Print("[ACCIDENT] Ελαφρύ έγκαυμα ατμού εργάτη.");
            }
        }
    }

    // 5. Εισαγωγή Ασθενών με Timestamps
    public void AddSick(int count)
    {
        int actual = Math.Min(BaseHealthyWorkforce, count);
        DateTime recoveryTarget = DateTime.UtcNow.AddHours(24);

        for (int i = 0; i < actual; i++)
        {
            _sickRecoveryQueue.Add(recoveryTarget);
        }

        if (actual > 0)
        {
            OnWorkersSickened?.Invoke(actual);
            EmitLedgerUpdate();
        }
    }

    public void AddGravelySick(int count)
    {
        int actual = Math.Min(BaseHealthyWorkforce, count);
        DateTime stabilizeTarget = DateTime.UtcNow.AddHours(48);

        for (int i = 0; i < actual; i++)
        {
            _gravelyStabilizeQueue.Add(stabilizeTarget);
        }

        if (actual > 0)
        {
            OnWorkersGravelyInjured?.Invoke(actual);
            EmitLedgerUpdate();
        }
    }

    // Μετατροπή ενός υπάρχοντος Sick σε Gravely Sick (απαιτεί 48h από τώρα)
    private void DegradeSickToGravely(int count)
    {
        int toMove = Math.Min(_sickRecoveryQueue.Count, count);
        for (int i = 0; i < toMove; i++)
        {
            _sickRecoveryQueue.RemoveAt(0);
            _gravelyStabilizeQueue.Add(DateTime.UtcNow.AddHours(48));
        }

        if (toMove > 0)
        {
            OnWorkersGravelyInjured?.Invoke(toMove);
            EmitLedgerUpdate();
        }
    }

    // 6. Καταγραφή Απωλειών (Αφαίρεση από τις ουρές)
    public void RecordCasualties(int count)
    {
        if (count <= 0) return;

        // Οι βίαιες εκρήξεις/ατυχήματα στη γεννήτρια πλήττουν τους εργάτες που βρίσκονται στη βάρδια (BaseHealthyWorkforce)
        // και ΟΧΙ τους ασθενείς που αναρρώνουν στα καταλύματα
        int availableHealthy = BaseHealthyWorkforce;
        int killedFromHealthy = Math.Min(availableHealthy, count);

        // Αφαίρεση απευθείας από το συνολικό δυναμικό
        TotalWorkforce = Math.Max(0, TotalWorkforce - killedFromHealthy);
        CasualtiesCount += killedFromHealthy;

        int remainingCasualties = count - killedFromHealthy;

        // Αν οι απώλειες ξεπερνούν τους υγιείς εργάτες (π.χ. μαζικό wipeout), 
        // τότε μόνο παρασύρονται οι άρρωστοι στους θαλάμους
        if (remainingCasualties > 0)
        {
            // Πρώτα αφαιρούνται οι βαριά άρρωστοι
            while (remainingCasualties > 0 && _gravelyStabilizeQueue.Count > 0)
            {
                _gravelyStabilizeQueue.RemoveAt(0);
                TotalWorkforce--;
                CasualtiesCount++;
                remainingCasualties--;
            }

            // Έπειτα οι ελαφρά άρρωστοι
            while (remainingCasualties > 0 && _sickRecoveryQueue.Count > 0)
            {
                _sickRecoveryQueue.RemoveAt(0);
                TotalWorkforce--;
                CasualtiesCount++;
                remainingCasualties--;
            }
        }

        // Ειδοποίηση για τις πραγματικές απώλειες που καταγράφηκαν
        OnCasualtiesOccurred?.Invoke(killedFromHealthy + (count - remainingCasualties));
        EmitLedgerUpdate();
    }

    // 7. Ενηλικίωση & Events
    public void AdvanceMaturationDays(double elapsedDays)
    {
        if (GeneralPopulation <= 0) return;

        _maturationProgressDays += elapsedDays;
        if (_maturationProgressDays >= MaturationIntervalDays)
        {
            _maturationProgressDays = 0.0;
            int newRecruits = Math.Max(1, Mathf.FloorToInt(GeneralPopulation * 0.05f));
            newRecruits = Math.Min(GeneralPopulation, newRecruits);

            GeneralPopulation -= newRecruits;
            TotalWorkforce += newRecruits;

            EmitLedgerUpdate();
        }
    }

    public void AddRefugees(int extraGeneral, int extraWorkers)
    {
        GeneralPopulation += Math.Max(0, extraGeneral);
        TotalWorkforce += Math.Max(0, extraWorkers);
        EmitLedgerUpdate();
    }

    private void EmitLedgerUpdate()
    {
        OnPopulationLedgerUpdated?.Invoke(
            EffectiveWorkforce,
            GeneralPopulation,
            SickWorkers,
            GravelySickWorkers,
            CasualtiesCount,
            EfficiencyFactor
        );
    }
}