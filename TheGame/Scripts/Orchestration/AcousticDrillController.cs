using Godot;
using System;
using System.Collections.Generic;
using PermafrostChieftain.Simulation;

namespace PermafrostChieftain.Orchestration
{
    // Καταστάσεις μηχανής εξόρυξης ακουστικού γεωτρύπανου (GDD 4.3)
    public enum DrillState
    {
        Idle,
        DisplayingPattern,
        AwaitingInput,
        LockoutCooldown,
        Completed
    }

    public partial class AcousticDrillController : PanelContainer
    {
        // Στοιχεία UI
        private Label _roundInfoLabel;
        private Label _stepInfoLabel;
        private Label _drillStatusLabel;
        private ProgressBar _lockoutCooldownBar;
        private Button _abortDrillButton;
        private Button[] _valveButtons = new Button[6];

        // Αναφορά στον κεντρικό ενορχηστρωτή
        private GameOrchestrator _gameOrchestrator;

        // Μεταβλητές κατάστασης γεωτρύπανου
        private DrillState _currentState = DrillState.Idle;
        private int _currentRound = 0; // 0 έως 3 (4 γύροι)
        private readonly List<int> _generatedPattern = new List<int>();
        private int _playerInputIndex = 0;

        // Χρονόμετρα αναπαραγωγής μοτίβου και κλειδώματος
        private double _playbackTimer = 0.0;
        private int _playbackStep = 0;
        private double _lockoutRemaining = 0.0;
        private const double StepInterval = 0.6; // 0.6s ανά βήμα επίδειξης (GDD 4.3)

        // Παράμετροι γύρων: [Μήκος μοτίβου, Πλήθος ενεργών βαλβίδων]
        private readonly (int Length, int ValvesCount)[] _roundConfigs = new (int, int)[]
        {
            (4, 4), // Γύρος 1
            (6, 4), // Γύρος 2
            (6, 6), // Γύρος 3
            (7, 6)  // Γύρος 4
        };

        public override void _Ready()
        {
            // Ανάκτηση του GameOrchestrator 3 επίπεδα πάνω:
            // AcousticDrillModal -> ModalOverlayLayer -> MainLayout -> UIRoot
            _gameOrchestrator = GetTree().Root.GetNode<GameOrchestrator>("TheGame");

            var drillVBox = GetNode<VBoxContainer>("DrillVBox");
            var headerHBox = drillVBox.GetNode<HBoxContainer>("DrillHeaderHBox");
            _roundInfoLabel = headerHBox.GetNode<Label>("RoundInfoLabel");
            _stepInfoLabel = headerHBox.GetNode<Label>("StepInfoLabel");

            var grid = drillVBox.GetNode<GridContainer>("ValveGridContainer");
            for (int i = 0; i < 6; i++)
            {
                int valveIndex = i;
                _valveButtons[i] = grid.GetNode<Button>($"ValveButton_{i}");
                _valveButtons[i].Pressed += () => OnValvePressed(valveIndex);
            }

            _drillStatusLabel = drillVBox.GetNode<Label>("DrillStatusLabel");
            _lockoutCooldownBar = drillVBox.GetNode<ProgressBar>("LockoutCooldownBar");
            _abortDrillButton = drillVBox.GetNode<Button>("AbortDrillButton");

            _abortDrillButton.Pressed += AbortDrill;

            Visible = false;
        }

        // Εκκίνηση του minigame γεωτρύπανου (καλείται από το LaunchDrillButton)
        public bool StartDrillSession()
        {
            if (_currentState == DrillState.LockoutCooldown) return false;

            // Απαιτείται 10 kg Coal και τουλάχιστον 3 χέρια (GDD 4.3)
            if (_gameOrchestrator.Logistics.CoalStockpile < 10.0)
            {
                _gameOrchestrator.DispatchLog("WARNING", "DRILL", "Ανεπαρκής άνθρακας για εκκίνηση γεωτρύπανου (απαιτούνται 10 kg).");
                return false;
            }

            _gameOrchestrator.Logistics.CoalStockpile -= 10.0;
            _currentRound = 0;
            Visible = true;
            StartRound(_currentRound);
            return true;
        }

        private void StartRound(int round)
        {
            _currentRound = round;
            _playerInputIndex = 0;
            _generatedPattern.Clear();

            var config = _roundConfigs[round];
            Random random = new Random();

            // Δημιουργία ψευδοτυχαίου μοτίβου για τον γύρο
            for (int i = 0; i < config.Length; i++)
            {
                _generatedPattern.Add(random.Next(0, config.ValvesCount));
            }

            // Ενεργοποίηση μόνο των έγκυρων κουμπιών βαλβίδας για τον συγκεκριμένο γύρο
            for (int i = 0; i < 6; i++)
            {
                _valveButtons[i].Visible = (i < config.ValvesCount);
                _valveButtons[i].Disabled = true;
            }

            _roundInfoLabel.Text = $"Γύρος {round + 1} από 4";
            _stepInfoLabel.Text = $"Βήμα 0 / {config.Length}";
            _drillStatusLabel.Text = "ΠΑΡΑΤΗΡΗΣΤΕ ΤΗ ΣΥΧΝΟΤΗΤΑ";
            _drillStatusLabel.Modulate = new Color(1f, 0.9f, 0.4f);

            _playbackStep = 0;
            _playbackTimer = 0.0;
            _currentState = DrillState.DisplayingPattern;
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;

            // Επεξεργασία αναπαραγωγής μοτίβου (DisplayingPattern)
            if (_currentState == DrillState.DisplayingPattern)
            {
                _playbackTimer += delta;
                if (_playbackTimer >= StepInterval)
                {
                    _playbackTimer -= StepInterval;
                    HighlightStep(_playbackStep);
                    _playbackStep++;

                    if (_playbackStep >= _generatedPattern.Count)
                    {
                        // Ολοκλήρωση επίδειξης - Παράδοση ελέγχου στον παίκτη
                        EndPatternPlayback();
                    }
                }
            }
            // Επεξεργασία κλειδώματος μετά από έκρηξη (LockoutCooldown)
            else if (_currentState == DrillState.LockoutCooldown)
            {
                _lockoutRemaining = Math.Max(0.0, _lockoutRemaining - delta);
                _lockoutCooldownBar.Value = _lockoutRemaining;

                if (_lockoutRemaining <= 0.0)
                {
                    _currentState = DrillState.Idle;
                    _drillStatusLabel.Text = "ΠΙΕΣΗ ΕΚΤΟΝΩΘΗΚΕ - ΕΤΟΙΜΟ";
                    _drillStatusLabel.Modulate = new Color(0.4f, 1f, 0.4f);
                }
            }
        }

        private void HighlightStep(int stepIndex)
        {
            int valve = _generatedPattern[stepIndex];
            _valveButtons[valve].Modulate = new Color(2f, 2f, 0.4f, 1f); // Έντονο φλας

            // Επαναφορά χρώματος μετά από σύντομο διάστημα
            GetTree().CreateTimer(0.3).Timeout += () =>
            {
                if (IsInstanceValid(_valveButtons[valve]))
                    _valveButtons[valve].Modulate = new Color(1f, 1f, 1f, 1f);
            };
        }

        private void EndPatternPlayback()
        {
            _currentState = DrillState.AwaitingInput;
            _drillStatusLabel.Text = "ΕΙΣΑΓΕΤΕ ΤΗΝ ΑΛΛΗΛΟΥΧΙΑ";
            _drillStatusLabel.Modulate = new Color(0.4f, 1f, 0.4f);

            var config = _roundConfigs[_currentRound];
            for (int i = 0; i < config.ValvesCount; i++)
            {
                _valveButtons[i].Disabled = false;
            }
        }

        // Χειρισμός πατήματος βαλβίδας από τον παίκτη
        private void OnValvePressed(int valveIndex)
        {
            if (_currentState != DrillState.AwaitingInput) return;

            if (valveIndex == _generatedPattern[_playerInputIndex])
            {
                // Σωστό βήμα
                _playerInputIndex++;
                _stepInfoLabel.Text = $"Βήμα {_playerInputIndex} / {_generatedPattern.Count}";

                if (_playerInputIndex >= _generatedPattern.Count)
                {
                    CompleteCurrentRound();
                }
            }
            else
            {
                // Λάθος βήμα -> Αστοχία / Έκρηξη (Blowout)
                TriggerBlowout();
            }
        }

        // Επιτυχής ολοκλήρωση γύρου και απόδοση ανταμοιβών (GDD 4.3)
        private void CompleteCurrentRound()
        {
            var logistics = _gameOrchestrator.Logistics;

            switch (_currentRound)
            {
                case 0:
                    logistics.CoalStockpile += 35.0;
                    _gameOrchestrator.DispatchLog("INFO", "DRILL", "Γεώτρηση Γύρος 1 επιτυχής: +35 kg Άνθρακας.");
                    break;
                case 1:
                    logistics.CoalStockpile += 75.0;
                    _gameOrchestrator.DispatchLog("INFO", "DRILL", "Γεώτρηση Γύρος 2 επιτυχής: +75 kg Άνθρακας.");
                    break;
                case 2:
                    logistics.CoalStockpile += 130.0;
                    _gameOrchestrator.DispatchLog("INFO", "DRILL", "Γεώτρηση Γύρος 3 επιτυχής: +130 kg Άνθρακας.");
                    break;
                case 3:
                    logistics.CoalStockpile += 220.0;
                    logistics.ScrapStockpile += 10.0;
                    _gameOrchestrator.DispatchLog("INFO", "DRILL", "Γεώτρηση Γύρος 4 ολοκληρώθηκε: +220 kg Άνθρακας, +10 Παλιοσίδερα!");
                    _currentState = DrillState.Completed;
                    _drillStatusLabel.Text = "ΦΛΕΒΑ ΕΞΑΝΤΛΗΘΗΚΕ ΠΛΗΡΩΣ";
                    AbortDrill();
                    return;
            }

            // Προχώρημα στον επόμενο γύρο
            StartRound(_currentRound + 1);
        }

        // Αστοχία: Πτώση θερμοκρασίας πυρήνα, πιθανός τραυματισμός και κλείδωμα 30s
        private void TriggerBlowout()
        {
            _currentState = DrillState.LockoutCooldown;
            _lockoutRemaining = 30.0;
            _lockoutCooldownBar.MaxValue = 30.0;
            _lockoutCooldownBar.Value = 30.0;

            // Απενεργοποίηση όλων των βαλβίδων
            for (int i = 0; i < 6; i++)
            {
                _valveButtons[i].Disabled = true;
            }

            // Θερμική ποινή: T_core -6.0°C (GDD 4.3)
            _gameOrchestrator.Thermodynamics.ApplyDirectThermalDelta(-6.0, 0.0);

            // Έλεγχος τραυματισμού χειριστή (20% πιθανότητα)
            if (new Random().NextDouble() < 0.20 && _gameOrchestrator.Population.HealthyWorkers > 0)
            {
                //_gameOrchestrator.Population.ApplyOverdriveCasualties(1);
                _gameOrchestrator.DispatchLog("CRITICAL", "DRILL", "Ακουστική υπερπίεση: Χειριστής γεωτρύπανου τραυματίστηκε σοβαρά!");
            }

            _drillStatusLabel.Text = "ΕΚΡΗΞΗ ΠΙΕΣΗΣ - ΚΛΕΙΔΩΜΑ 30s";
            _drillStatusLabel.Modulate = new Color(1f, 0.2f, 0.2f);
            _gameOrchestrator.DispatchLog("WARNING", "DRILL", "Αστοχία γεωτρύπανου! -6°C στον πυρήνα, κλείδωμα συστήματος για 30 δευτερόλεπτα.");
        }

        private void AbortDrill()
        {
            Visible = false;
            for (int i = 0; i < 6; i++)
            {
                _valveButtons[i].Disabled = true;
            }
        }
    }
}