using Godot;
using System;
using System.Collections.Generic;

// Καταστάσεις λειτουργίας του Minigame
public enum DrillState
{
    Idle,            // Ανενεργό / σε αναμονή εκκίνησης
    DisplayingPattern,// Αναπαραγωγή της ακολουθίας (κλειδωμένα κουμπιά για τον παίκτη)
    AwaitingInput,   // Ο παίκτης εισάγει την ακολουθία
    RoundSuccess,    // Επιτυχής γύρος
    LockoutCooldown  // Ποινή μετά από λάθος (εκτόνωση ατμού)
}

public partial class AcousticDrillMinigame : Node
{
    // Events για το UI και το Game Loop
    public event Action<int, int> OnRoundStarted;          // (Τρέχων γύρος, Σύνολο γύρων)
    public event Action<int> OnValveHighlighted;           // Δείκτης βαλβίδας που αναβοσβήνει
    public event Action<int> OnPlayerInputAccepted;        // Βήμα που πάτησε σωστά ο παίκτης
    public event Action<int, float> OnMinigameWon;         // (Γύροι που ολοκληρώθηκαν, Κάρβουνο που κερδήθηκε)
    public event Action<float> OnDrillBlowout;              // (Ποινή θερμότητας που αφαιρέθηκε)
    public event Action<DrillState> OnStateChanged;

    [ExportGroup("Dependencies")]
    [Export] private ResourceSubsystem _resourceSubsystem;
    [Export] private SkeletonCrewSubsystem _skeletonCrewSubsystem;

    [ExportGroup("Round Configurations")]
    // Αριθμός βημάτων ακολουθίας ανά γύρο (π.χ. 4, 6, 6, 7)
    [Export] public Godot.Collections.Array<int> StepsPerRound = new() { 4, 6, 6, 7 };

    // Αριθμός διαθέσιμων κουμπιών ανά γύρο (π.χ. 4, 4, 6, 6)
    [Export] public Godot.Collections.Array<int> ButtonsPerRound = new() { 4, 4, 6, 6 };

    [ExportGroup("Gameplay Tuning")]
    [Export] public float FlashDurationSec = 0.45f;        // Διάρκεια φωτισμού κάθε βαλβίδας
    [Export] public float FlashIntervalSec = 0.20f;        // Παύση ανάμεσα στα φλας
    [Export] public float FailureCooldownSec = 30.0f;      // Χρόνος αποσυμπίεσης μετά από λάθος
    [Export] public float BaseHeatPenaltyC = 6.0f;         // Πτώση θερμοκρασίας πυρήνα ανά λάθος
    [Export] public Godot.Collections.Array<float> CoalRewardsPerRound = new() { 35.0f, 75.0f, 130.0f, 220.0f };

    public DrillState CurrentState { get; private set; } = DrillState.Idle;
    public int CurrentRoundIndex { get; private set; } = 0;

    private readonly List<int> _generatedSequence = new();
    private int _playerStepIndex = 0;
    private readonly RandomNumberGenerator _rng = new();

    private double _timer = 0.0;
    private int _displayStepIndex = 0;
    private bool _isFlashing = false;

    public override void _Ready()
    {
        _rng.Randomize();
    }

    public override void _Process(double delta)
    {
        // 1. Διαχείριση χρονομέτρου για το cooldown εκτόνωσης μετά από λάθος
        if (CurrentState == DrillState.LockoutCooldown)
        {
            _timer -= delta;
            if (_timer <= 0.0)
            {
                SetState(DrillState.Idle);
            }
            return;
        }

        // 2. Αναπαραγωγή ακολουθίας βαλβίδων (Simon Says display loop)
        if (CurrentState == DrillState.DisplayingPattern)
        {
            _timer -= delta;
            if (_timer <= 0.0)
            {
                if (_isFlashing)
                {
                    // Σβήσιμο ενεργού κουμπιού και έναρξη κενού διαστήματος
                    _isFlashing = false;
                    _timer = FlashIntervalSec;
                    OnValveHighlighted?.Invoke(-1); // -1 σημαίνει καμία βαλβίδα ενεργή
                    _displayStepIndex++;

                    // Έλεγχος αν ολοκληρώθηκε η προβολή ολόκληρης της ακολουθίας
                    if (_displayStepIndex >= _generatedSequence.Count)
                    {
                        SetState(DrillState.AwaitingInput);
                    }
                }
                else
                {
                    // Άναμμα της επόμενης βαλβίδας στην ακολουθία
                    _isFlashing = true;
                    _timer = FlashDurationSec;
                    OnValveHighlighted?.Invoke(_generatedSequence[_displayStepIndex]);
                }
            }
        }
    }

    // Έναρξη νέας προσπάθειας στο Minigame
    public bool StartMinigame()
    {
        if (CurrentState == DrillState.LockoutCooldown)
        {
            GD.Print("[DRILL] Η μονάδα βρίσκεται σε φάση εκτόνωσης πίεσης (Cooldown).");
            return false;
        }

        // Απαίτηση ελάχιστης επάνδρωσης (τουλάχιστον 3 διαθέσιμοι νυχτερινοί εργάτες)
        if (_skeletonCrewSubsystem != null && _skeletonCrewSubsystem.AvailableNightHands < 3)
        {
            GD.Print("[DRILL] Ανεπαρκές προσωπικό για τη λειτουργία του κρουστικού τρυπανιού.");
            return false;
        }

        CurrentRoundIndex = 0;
        StartCurrentRound();
        return true;
    }

    // Προετοιμασία και έναρξη συγκεκριμένου γύρου
    private void StartCurrentRound()
    {
        _generatedSequence.Clear();
        _playerStepIndex = 0;
        _displayStepIndex = 0;
        _isFlashing = false;

        int totalRounds = StepsPerRound.Count;
        int stepsCount = StepsPerRound[Mathf.Clamp(CurrentRoundIndex, 0, totalRounds - 1)];
        int availableButtons = ButtonsPerRound[Mathf.Clamp(CurrentRoundIndex, 0, ButtonsPerRound.Count - 1)];

        // Παραγωγή τυχαίας ακολουθίας βάσει των διαθέσιμων κουμπιών του τρέχοντος γύρου
        for (int i = 0; i < stepsCount; i++)
        {
            _generatedSequence.Add(_rng.RandiRange(0, availableButtons - 1));
        }

        OnRoundStarted?.Invoke(CurrentRoundIndex + 1, totalRounds);

        // Μικρή καθυστέρηση πριν αρχίσει να αναβοσβήνει η ακολουθία
        _timer = 0.5;
        SetState(DrillState.DisplayingPattern);
    }

    // Επεξεργασία πατήματος κουμπιού από τον παίκτη
    public void SubmitValveInput(int valveIndex)
    {
        if (CurrentState != DrillState.AwaitingInput) return;

        // Έλεγχος αν το πάτημα ταιριάζει με το αναμενόμενο βήμα
        if (valveIndex == _generatedSequence[_playerStepIndex])
        {
            OnPlayerInputAccepted?.Invoke(valveIndex);
            _playerStepIndex++;

            // Ο παίκτης συμπλήρωσε όλη την ακολουθία του γύρου
            if (_playerStepIndex >= _generatedSequence.Count)
            {
                ResolveRoundSuccess();
            }
        }
        else
        {
            // Λάθος πάτημα -> Πνευματική εκτόνωση και απώλεια θερμότητας
            TriggerBlowoutFailure();
        }
    }

    // Επιτυχής ολοκλήρωση γύρου
    private void ResolveRoundSuccess()
    {
        SetState(DrillState.RoundSuccess);

        // Απόδοση κάρβουνου για τον τρέχοντα γύρο
        float reward = CoalRewardsPerRound.Count > CurrentRoundIndex ? CoalRewardsPerRound[CurrentRoundIndex] : 50.0f;
        _resourceSubsystem?.AddCoal(reward);

        CurrentRoundIndex++;

        // Έλεγχος αν ολοκληρώθηκαν όλοι οι διαθέσιμοι γύροι
        if (CurrentRoundIndex >= StepsPerRound.Count)
        {
            GD.Print("[DRILL] Εξόρυξη ολοκληρώθηκε πλήρως! Όλοι οι γύροι καθαρίστηκαν.");
            OnMinigameWon?.Invoke(CurrentRoundIndex, reward);
            SetState(DrillState.Idle);
        }
        else
        {
            // Μετάβαση στον επόμενο γύρο μετά από μικρή παύση 1 δευτερολέπτου
            GetTree().CreateTimer(1.0).Timeout += StartCurrentRound;
        }
    }

    // Αποτυχία ακολουθίας (Blowout)
    private void TriggerBlowoutFailure()
    {
        GD.Print("[DRILL] Λάθος ακολουθία! Διαφυγή ατμού και πτώση θερμότητας πυρήνα!");

        // Αφαίρεση θερμότητας από τη γεννήτρια
        _resourceSubsystem?.DrainCoreHeat(BaseHeatPenaltyC);
        OnDrillBlowout?.Invoke(BaseHeatPenaltyC);

        // Κλείδωμα σε κατάσταση cooldown
        _timer = FailureCooldownSec;
        SetState(DrillState.LockoutCooldown);
    }

    private void SetState(DrillState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(CurrentState);
    }
}