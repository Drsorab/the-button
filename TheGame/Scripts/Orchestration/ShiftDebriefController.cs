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
        private int _fusesAtDayStart = 0;

        // Απαιτούμενο προσωπικό νυχτερινής βάρδιας (Skeleton Crew)
        private const int IdealNightHands = 10;

        public override void _Ready()
        {
            // Ανάκτηση του GameOrchestrator
            _gameOrchestrator = GetTree().Root.GetNodeOrNull<GameOrchestrator>("TheGame");

            // Σύνδεση στοιχείων διεπαφής χρήστη
            var debriefVBox = GetNode<VBoxContainer>("DebriefVBox");
            _debriefManifestLabel = debriefVBox.GetNode<RichTextLabel>("DebriefManifestLabel");
            _nightStaffingLabel = debriefVBox.GetNode<Label>("NightStaffingLabel");
            _doubleShiftWarningLabel = debriefVBox.GetNode<Label>("DoubleShiftWarningLabel");
            _confirmHandoverButton = debriefVBox.GetNode<Button>("ConfirmHandoverButton");

            // Ενεργοποίηση BBCode (Σημαντικό για τα χρώματα [color])
            _debriefManifestLabel.BbcodeEnabled = true;

            _confirmHandoverButton.Pressed += OnConfirmHandoverPressed;

            // Σύνδεση με το σήμα αλλαγής φάσης του Simulator.
            // Σημείωση: Χρησιμοποιούμε την ProcessTick του Orchestrator για να παρακολουθούμε
            // τις φάσεις. 
            if (_gameOrchestrator != null)
            {
                // Αν δεν έχεις event PhaseTransitionTriggered στον Orchestrator, 
                // θα χρειαστεί να το προσθέσεις. Στα προηγούμενα βήματα το είχαμε 
                // βάλει στην HandlePhaseChange(wasDayBefore).
                _gameOrchestrator.PhaseTransitionTriggered += OnPhaseTransition;
            }

            Visible = false;
        }

        private void OnPhaseTransition(int newPhase)
        {
            CyclePhase phase = (CyclePhase)newPhase;

            // Το Πρωινό Σφύριγμα: Όταν η φάση γίνεται DayActive (π.χ. στην αρχή του παιχνιδιού 
            // ή μετά από το NightBleed).
            if (phase == CyclePhase.DayActive)
            {
                CaptureDayStartMetrics();
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

        // Λήψη του Snapshot της αρχής της Ημέρας
        private void CaptureDayStartMetrics()
        {
            if (_gameOrchestrator == null) return;

            _coalAtDayStart = _gameOrchestrator.Logistics.CoalStockpile;
            _casingAtDayStart = _gameOrchestrator.Boiler.CasingIntegrity;
            _sickAtDayStart = _gameOrchestrator.Population.SickWorkers + _gameOrchestrator.Population.GraveWorkers;
            _deadAtDayStart = _gameOrchestrator.Population.DeadWorkers;
            _fusesAtDayStart = _gameOrchestrator.Inventory.GetConsumableCount("Boiler Fuse");
        }

        // Δημιουργία και εμφάνιση της αναφοράς βάρδιας
        private void GenerateDebriefManifest()
        {
            if (_gameOrchestrator == null) return;

            var pop = _gameOrchestrator.Population;
            var boiler = _gameOrchestrator.Boiler;
            var logistics = _gameOrchestrator.Logistics;
            var inventory = _gameOrchestrator.Inventory;

            // Υπολογισμός απωλειών και παραγωγής
            double netCoal = logistics.CoalStockpile - _coalAtDayStart;
            double casingLost = Math.Max(0.0, _casingAtDayStart - boiler.CasingIntegrity);

            // Fuses που καταναλώθηκαν (ή κατασκευάστηκαν)
            int currentFuses = inventory.GetConsumableCount("Boiler Fuse");
            int netFuses = currentFuses - _fusesAtDayStart;

            int currentSick = pop.SickWorkers + pop.GraveWorkers;
            int newSick = Math.Max(0, currentSick - _sickAtDayStart);
            int newDead = Math.Max(0, pop.DeadWorkers - _deadAtDayStart);
            int currentPatches = inventory.GetConsumableCount("Metal Patch");

            _debriefManifestLabel.Clear();
            _debriefManifestLabel.AppendText("[center][b]ΑΝΑΦΟΡΑ ΛΗΞΗΣ ΗΜΕΡΗΣΙΑΣ ΒΑΡΔΙΑΣ[/b][/center]\n\n");

            // Ένδειξη για Κάρβουνο
            if (netCoal >= 0)
                _debriefManifestLabel.AppendText($"• Ισοζύγιο Άνθρακα: [color=green]+{netCoal:F1} kg[/color]\n");
            else
                _debriefManifestLabel.AppendText($"• Ισοζύγιο Άνθρακα: [color=yellow]{netCoal:F1} kg[/color]\n");

            // Ένδειξη για Κέλυφος Γεννήτριας
            if (casingLost > 0)
                _debriefManifestLabel.AppendText($"• Φθορά Κελύφους: [color=red]-{casingLost:F1}%[/color] (Τρέχουσα: {boiler.CasingIntegrity:F1}%)\n");
            else
                _debriefManifestLabel.AppendText($"• Φθορά Κελύφους: [color=green]Αμετάβλητη[/color] (Τρέχουσα: {boiler.CasingIntegrity:F1}%)\n");

            // Ένδειξη για Προσωπικό
            _debriefManifestLabel.AppendText($"• Νέοι Τραυματισμοί/Ασθενείς: [color=orange]{newSick}[/color]\n");
            _debriefManifestLabel.AppendText($"• Νέοι Θάνατοι: [color=red]{newDead}[/color]\n");

            // Ένδειξη για Αιθάλη (Σημαντική, καθώς αν είναι ψηλά, τη νύχτα θα έχουν πρόβλημα)
            double sootPct = boiler.SootRatio * 100.0;
            string sootColor = sootPct > 50.0 ? "red" : "white";
            _debriefManifestLabel.AppendText($"• Επίπεδο Αιθάλης (Soot): [color={sootColor}]{sootPct:F1}%[/color]\n");

            // Αναφορά Ασφαλειών (Fuses)
            if (netFuses != 0)
            {
                string fuseStr = netFuses > 0 ? $"+{netFuses}" : $"{netFuses}";
                _debriefManifestLabel.AppendText($"• Αλλαγή Αποθέματος Ασφαλειών: {fuseStr} (Διαθέσιμες: {currentFuses})\n");
            }

            // Υπόμνηση για ρήγματα! Αν έχει τρύπα, πρέπει να ειδοποιηθεί.
            if (boiler.IsHullCracked)
            {
                _debriefManifestLabel.AppendText("\n[color=red][b]>>> ΚΡΙΣΙΜΟ: ΡΗΓΜΑ ΣΤΟ ΚΕΛΥΦΟΣ! <<<\nΑπαιτείται 1 Metal Patch. Διαθέσιμα Patches: {currentPatches}[/b][/color]\n");
            }

            // --- Έλεγχος διαθέσιμου προσωπικού για τη νυχτερινή βάρδια ---
            int availableHealthy = pop.HealthyWorkers;

            // Ελέγχουμε αν υπάρχει ήδη εκχωρημένο προσωπικό στα νυχτερινά πόστα
            // Αυτό είναι σημαντικό, γιατί μπορεί ο παίκτης να έχει βάλει κόσμο στην "Εντατική"
            int assignedToNightShift = 0;
            if (_gameOrchestrator.JobManager.Jobs.ContainsKey(JobType.IntensiveCare))
                assignedToNightShift += _gameOrchestrator.JobManager.Jobs[JobType.IntensiveCare].AssignedWorkers;

            if (_gameOrchestrator.JobManager.Jobs.ContainsKey(JobType.NightHullRepair))
                assignedToNightShift += _gameOrchestrator.JobManager.Jobs[JobType.NightHullRepair].AssignedWorkers;

            if (availableHealthy < IdealNightHands && assignedToNightShift < IdealNightHands)
            {
                _nightStaffingLabel.Text = $"Υγιείς Εργάτες: {availableHealthy} / {IdealNightHands} Απαιτούμενοι για ασφαλή νύχτα";
                _nightStaffingLabel.Modulate = new Color(1f, 0.4f, 0.4f); // Κόκκινο

                _doubleShiftWarningLabel.Visible = true;
                _doubleShiftWarningLabel.Text = "ΠΡΟΕΙΔΟΠΟΙΗΣΗ: Το εργατικό δυναμικό έχει αποδεκατιστεί. Η νυχτερινή συντήρηση και φροντίδα ενδέχεται να υπολειτουργήσουν, αυξάνοντας τους κινδύνους!";
                _doubleShiftWarningLabel.Modulate = new Color(1f, 0.6f, 0.2f); // Πορτοκαλί
            }
            else
            {
                _nightStaffingLabel.Text = $"Υγιείς Εργάτες: {availableHealthy} (Επαρκείς για νυχτερινές εργασίες)";
                _nightStaffingLabel.Modulate = new Color(0.4f, 1f, 0.4f); // Πράσινο
                _doubleShiftWarningLabel.Visible = false;
            }
        }

        // Ολοκλήρωση απολογισμού (Κλείσιμο του παραθύρου)
        private void OnConfirmHandoverPressed()
        {
            if (_gameOrchestrator != null)
            {
                // Το DispatchLog δεν υπάρχει στον Orchestrator ως μέθοδος, 
                // αλλά μπορούμε να καλέσουμε το Action:
                // _gameOrchestrator.OnGameEvent?.Invoke("[SHIFT] Ο απολογισμός διαβάστηκε. Ανακατανομή προσωπικού για το λυκόφως (Twilight).");

                // Σε περίπτωση που θέλεις να στείλεις ένα LogEvent (όπως το είχες):
                // Αν ο Orchestrator δεν έχει DispatchLog, τότε μπορείς να χρησιμοποιήσεις το OnGameEvent:
                GD.Print("[SHIFT] Ο απολογισμός διαβάστηκε. Ανακατανομή προσωπικού για το λυκόφως (Twilight).");
            }

            Visible = false;
        }
    }
}