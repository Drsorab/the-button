using System;

namespace PermafrostChieftain.Simulation
{
    public class BoilerSubsystem
    {
        public double SteamPressure { get; private set; }
        public double CasingIntegrity { get; private set; } = 100.0;
        public double StressLevel { get; private set; } = 0.0;
        public bool IsHullCracked { get; private set; } = false;
        public double CurrentSoot { get; private set; } = 0.0;
        // Υπολογίζει το ποσοστό (0.0 έως 1.0) της καπνιάς για να εφαρμόσει τα πέναλτι
        public double SootRatio => Math.Clamp(CurrentSoot / GameSettings.Instance.MaxSootCapacity, 0.0, 1.0);
        // Αντί για επίπεδο ρυθμό διαρροής, κρατάμε το *Μέγεθος του Ρήγματος* (Crack Area)
        public double CrackSizeArea { get; private set; } = 0.0;

        public bool IsGroaning { get; private set; } = false;
        private double _gracePeriodTimer = 0.0;

        // Γεγονός για ειδοποιήσεις UI/Log
        public event Action<string> OnBoilerAlert;

        public void Initialize(double startingIntegrity)
        {
            CasingIntegrity = startingIntegrity;
            SteamPressure = 0.0;
        }

        public void AddSteam(double amount)
        {
            SteamPressure += amount;
        }

        public void DeductSteam(double amount)
        {
            SteamPressure = Math.Max(0.0, SteamPressure - amount);
        }

        public void ProcessTick(double deltaTime)
        {
            if (deltaTime <= 0.0) return;

            // =========================================================
            // ΦΑΣΗ 1: ΟΡΙΟ ΑΣΦΑΛΕΙΑΣ (SAFE PRESSURE CAPABILITY)
            // =========================================================
            // Όσο πιο χαμηλό το Integrity, τόσο πέφτει η ικανότητα συγκράτησης πίεσης
            double integrityFactor = CasingIntegrity / 100.0;
            double safePressure = GameSettings.Instance.MaxSafePressureAtFullIntegrity * integrityFactor * GameSettings.Instance.IntegrityToSafePressureMultiplier;
            safePressure = Math.Max(10.0, safePressure); // Το ελάχιστο όριο (ώστε να μη σπάει στο απόλυτο μηδέν)

            // =========================================================
            // ΦΑΣΗ 2: ΣΥΣΣΩΡΕΥΣΗ ΚΑΙ ΑΠΟΔΕΣΜΕΥΣΗ ΣΤΡΕΣ (FATIGUE & RELAXATION)
            // =========================================================
            if (SteamPressure > safePressure)
            {
                double overpressure = SteamPressure - safePressure;

                // Εφαρμογή πολλαπλασιαστή Στρες λόγω καπνιάς (Option B)
                double sootStressMultiplier = 1.0 + (SootRatio * (GameSettings.Instance.SootPenalty_StressAccumulationMultiplier - 1.0));

                StressLevel += overpressure * GameSettings.Instance.StressAccumulationPerOverpressure * sootStressMultiplier * deltaTime;
            }
            else if (StressLevel > 0)
            {
                // ΦΥΣΙΚΗ: Το μέταλλο ηρεμεί. Όσο πιο ΚΑΤΩ από το όριο είμαστε (Safety Margin), τόσο πιο γρήγορα ηρεμεί.
                double safetyMargin = safePressure - SteamPressure;
                double reliefRate = GameSettings.Instance.BaseStressReliefRate + (safetyMargin * 0.05); // Bonus ηρεμία

                StressLevel = Math.Max(0.0, StressLevel - (reliefRate * deltaTime));
            }

            // =========================================================
            // ΦΑΣΗ 3: ΧΡΟΝΟΣ ΧΑΡΙΤΟΣ & ΠΑΡΑΜΟΡΦΩΣΗ (GRACE PERIOD)
            // =========================================================
            if (StressLevel >= GameSettings.Instance.StressToDamageThreshold)
            {
                if (!IsGroaning)
                {
                    IsGroaning = true;
                    OnBoilerAlert?.Invoke("ΠΡΟΕΙΔΟΠΟΙΗΣΗ: Το κέλυφος παραμορφώνεται! Εκτονώστε την πίεση!");
                }

                _gracePeriodTimer += deltaTime;

                if (_gracePeriodTimer >= GameSettings.Instance.StressBreakGracePeriod)
                {
                    // Το μέταλλο έσπασε. Μόνιμη ζημιά στην Αντοχή (Integrity).
                    CasingIntegrity = Math.Max(0.0, CasingIntegrity - GameSettings.Instance.IntegrityDamagePerStressBreak);
                    StressLevel = 0.0; // Εκτόνωση της μηχανικής τάσης λόγω της θραύσης
                    _gracePeriodTimer = 0.0;
                    IsGroaning = false;

                    OnBoilerAlert?.Invoke($"ΖΗΜΙΑ: Το κέλυφος υπέστη μόνιμη φθορά! Αντοχή: {CasingIntegrity:F1}%");

                    RollForHullCrack(); // Έλεγχος για δημιουργία οπής
                }
            }
            else if (IsGroaning && StressLevel < GameSettings.Instance.StressToDamageThreshold)
            {
                // Ο παίκτης πρόλαβε να ρίξει την πίεση εγκαίρως!
                IsGroaning = false;
                _gracePeriodTimer = 0.0;
                OnBoilerAlert?.Invoke("ΑΝΑΚΟΥΦΙΣΗ: Η πίεση έπεσε, το μέταλλο σταμάτησε να τρίζει.");
            }

            // =========================================================
            // ΦΑΣΗ 4: ΔΥΝΑΜΙΚΗ ΡΕΥΣΤΩΝ ΓΙΑ ΔΙΑΡΡΟΕΣ (BERNOULLI'S LEAK PHYSICS)
            // =========================================================
            if (IsHullCracked && SteamPressure > 0)
            {
                // Η τρύπα (CrackArea) μεγαλώνει όσο μένει αδιόρθωτη
                CrackSizeArea += GameSettings.Instance.CrackLeakEscalationRate * deltaTime;

                // Αρχή του Bernoulli: Η ποσότητα που διαφεύγει εξαρτάται από το μέγεθος της τρύπας ΚΑΙ 
                // την τετραγωνική ρίζα της τρέχουσας πίεσης. Υψηλή πίεση = Βίαιη εκτόνωση!
                double steamLost = CrackSizeArea * Math.Sqrt(SteamPressure) * deltaTime;

                DeductSteam(steamLost);
            }
        }

        private void RollForHullCrack()
        {
            if (IsHullCracked) return;

            Random rng = new Random();
            // Πιθανότητα βάσει της εναπομείνασας αντοχής. Στο 100% είναι σχεδόν 0, στο 0% είναι 100%
            double crackChance = 1.0 - (CasingIntegrity / 100.0);

            if (rng.NextDouble() < crackChance)
            {
                IsHullCracked = true;
                // Αρχικό μέγεθος της τρύπας
                CrackSizeArea = GameSettings.Instance.InitialCrackLeakRate;
                OnBoilerAlert?.Invoke("ΚΑΤΑΣΤΡΟΦΙΚΗ ΑΣΤΟΧΙΑ: Ρήγμα στο κέλυφος! Ανεξέλεγκτη διαρροή ατμού!");
            }
        }

        public void ApplyMetalPatch()
        {
            if (IsHullCracked)
            {
                IsHullCracked = false;
                CrackSizeArea = 0.0; // Το μπάλωμα μηδενίζει το μέγεθος της τρύπας
                OnBoilerAlert?.Invoke("ΕΠΙΣΚΕΥΗ: Το ρήγμα μπαλώθηκε επιτυχώς! Η διαρροή σταμάτησε.");
            }
        }

        public void RepairIntegrity(double amount)
        {
            CasingIntegrity = Math.Min(100.0, CasingIntegrity + amount);
        }

        // Καλείται από τον Orchestrator όταν καίγεται κάρβουνο
        public void AddSoot(double coalBurned)
        {
            CurrentSoot = Math.Min(GameSettings.Instance.MaxSootCapacity, CurrentSoot + (coalBurned * GameSettings.Instance.SootAccumulationPerKgCoal));
        }

        // Καλείται από το JobManager όταν ολοκληρώνεται το καθάρισμα
        public void RemoveSoot(double amount)
        {
            CurrentSoot = Math.Max(0.0, CurrentSoot - amount);
        }
        // Καλείται ΜΟΝΟ από το Cold Start σενάριο (ή από ειδικά in-game events)
        public void ForceHullCrack()
        {
            if (!IsHullCracked)
            {
                IsHullCracked = true;
                CrackSizeArea = GameSettings.Instance.InitialCrackLeakRate;
                OnBoilerAlert?.Invoke("ΚΑΤΑΣΤΡΟΦΙΚΗ ΑΣΤΟΧΙΑ: Ρήγμα στο κέλυφος! Ανεξέλεγκτη διαρροή ατμού!");
            }
        }
    }
}