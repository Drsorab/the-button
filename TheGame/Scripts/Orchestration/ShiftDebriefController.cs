using Godot;
using System;
using PermafrostChieftain.Simulation;

namespace PermafrostChieftain.Orchestration
{
    public partial class ShiftDebriefController : PanelContainer
    {
        // Αναφορές σε κόμβους διεπαφής χρήστη (UI)
        private RichTextLabel _debriefManifestLabel;
        private Label _nightStaffingLabel;
        private Label _doubleShiftWarningLabel;
        private Button _confirmHandoverButton;

        // Αναφορά στον κεντρικό ενορχηστρωτή
        private GameOrchestrator _gameOrchestrator;

        // Στατιστικά έναρξης ημέρας για υπολογισμό διαφορών (Deltas)
        private double _coalAtDayStart = 0.0;
        private double _casingAtDayStart = 100.0;
        private int _sickAtDayStart = 0;
        private int _deadAtDayStart = 0;

        // Απαιτούμενο προσωπικό νυχτερινής βάρδιας (Skeleton Crew)
        private const int IdealNightHands = 10;

        public override void _Ready()
        {
            // Ανάκτηση του GameOrchestrator
            _gameOrchestrator = GetTree().Root.GetNode<GameOrchestrator>("TheGame");

            // Σύνδεση στοιχείων διεπαφής χρήστη
            var debriefVBox = GetNode<VBoxContainer>("DebriefVBox");
            _debriefManifestLabel = debriefVBox.GetNode<RichTextLabel>("DebriefManifestLabel");
            _nightStaffingLabel = debriefVBox.GetNode<Label>("NightStaffingLabel");
            _doubleShiftWarningLabel = debriefVBox.GetNode<Label>("DoubleShiftWarningLabel");
            _confirmHandoverButton = debriefVBox.GetNode<Button>("ConfirmHandoverButton");

            _confirmHandoverButton.Pressed += OnConfirmHandoverPressed;

            // Σύνδεση με το σήμα αλλαγής φάσης
            if (_gameOrchestrator != null)
            {
                _gameOrchestrator.PhaseTransitionTriggered += OnPhaseTransition;
            }

            Visible = false;
        }

        private void OnPhaseTransition(int newPhase)
        {
            CyclePhase phase = (CyclePhase)newPhase;

            if (phase == CyclePhase.DayActive && _gameOrchestrator.Clock.ActiveCyclesCompletedToday == 0)
            {
                // Αποθήκευση μετρήσεων κατά την εκκίνηση της νέας ημέρας (Πρωινό Σφύριγμα)
                _coalAtDayStart = _gameOrchestrator.Logistics.CoalStockpile;
                _casingAtDayStart = _gameOrchestrator.Boiler.CasingIntegrity;
                _sickAtDayStart = _gameOrchestrator.Population.SickWorkers + _gameOrchestrator.Population.GraveWorkers;
                _deadAtDayStart = _gameOrchestrator.Population.DeadWorkers;
            }
            else if (phase == CyclePhase.TwilightWindDown)
            {
                // Η φάση προετοιμασίας για τη νύχτα ξεκινά. Εμφάνιση απολογισμού.
                GenerateDebriefManifest();
                Visible = true;
            }
            else if (phase == CyclePhase.NightBleed)
            {
                // Απόκρυψη αν ο παίκτης το άφησε ανοιχτό
                Visible = false;
            }
        }

        // Δημιουργία και εμφάνιση της αναφοράς βάρδιας
        private void GenerateDebriefManifest()
        {
            var pop = _gameOrchestrator.Population;
            var boiler = _gameOrchestrator.Boiler;
            var logistics = _gameOrchestrator.Logistics;

            // Υπολογισμός απωλειών και παραγωγής
            double netCoal = logistics.CoalStockpile - _coalAtDayStart;
            double casingLost = Math.Max(0.0, _casingAtDayStart - boiler.CasingIntegrity);

            int currentSick = pop.SickWorkers + pop.GraveWorkers;
            int newSick = Math.Max(0, currentSick - _sickAtDayStart);
            int newDead = Math.Max(0, pop.DeadWorkers - _deadAtDayStart);

            _debriefManifestLabel.Clear();
            _debriefManifestLabel.AppendText($"[b]ΑΝΑΦΟΡΑ ΛΗΞΗΣ ΗΜΕΡΗΣΙΑΣ ΒΑΡΔΙΑΣ[/b]\n");

            if (netCoal >= 0)
                _debriefManifestLabel.AppendText($"• Ισοζύγιο Άνθρακα: [color=green]+{netCoal:F1} kg[/color]\n");
            else
                _debriefManifestLabel.AppendText($"• Ισοζύγιο Άνθρακα: [color=yellow]{netCoal:F1} kg[/color]\n");

            _debriefManifestLabel.AppendText($"• Φθορά Κελύφους: [color=orange]-{casingLost:F1}%[/color] (Τρέχουσα: {boiler.CasingIntegrity:F1}%)\n");
            _debriefManifestLabel.AppendText($"• Νέοι Τραυματισμοί: [color=orange]{newSick}[/color]\n");
            _debriefManifestLabel.AppendText($"• Νέοι Θάνατοι: [color=red]{newDead}[/color]\n");
            _debriefManifestLabel.AppendText($"• Επίπεδο Αιθάλης: {boiler.SootFraction * 100.0:F1}%\n");

            // Έλεγχος διαθέσιμου προσωπικού για τη νυχτερινή βάρδια
            int availableHealthy = pop.HealthyWorkers;

            if (availableHealthy < IdealNightHands)
            {
                _nightStaffingLabel.Text = $"Υγιείς Εργάτες: {availableHealthy} / {IdealNightHands} Απαιτούμενοι";
                _nightStaffingLabel.Modulate = new Color(1f, 0.4f, 0.4f);

                _doubleShiftWarningLabel.Visible = true;
                _doubleShiftWarningLabel.Text = "ΠΡΟΕΙΔΟΠΟΙΗΣΗ: Το εργατικό δυναμικό έχει αποδεκατιστεί. Η νυχτερινή συντήρηση θα υπολειτουργεί, αυξάνοντας τους κινδύνους για αύριο!";
            }
            else
            {
                _nightStaffingLabel.Text = $"Υγιείς Εργάτες: {availableHealthy} (Επαρκείς για νυχτερινές εργασίες)";
                _nightStaffingLabel.Modulate = new Color(0.4f, 1f, 0.4f);
                _doubleShiftWarningLabel.Visible = false;
            }
        }

        // Ολοκλήρωση απολογισμού (Κλείσιμο του παραθύρου)
        private void OnConfirmHandoverPressed()
        {
            _gameOrchestrator.DispatchLog("INFO", "SHIFT", "Ο απολογισμός διαβάστηκε. Ανακατανομή προσωπικού για το λυκόφως (Twilight).");
            Visible = false;
        }
    }
}