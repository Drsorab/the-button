using System;

namespace PermafrostChieftain.Simulation
{
    public class BoilerSubsystem
    {
        public double SteamPressure { get; private set; } = GameSettings.Instance.StartingPressure;
        public double CasingIntegrity { get; private set; } = 100.0;
        public double SootFraction { get; private set; } = 0.0;

        public int AvailableFuses { get; set; } = 3;

        // Φθορά Overdrive
        public bool IsOverdriveActive { get; set; } = false;
        public double OverdriveStress { get; private set; } = 0.0;
        public double OverdriveLockout { get; private set; } = 0.0;

        // Κατάσταση βλάβης αν δεν πατηθεί το κουμπί και δεν υπάρχουν ασφάλειες
        public bool IsHullCracked { get; private set; } = false;

        public void ProcessTick(double deltaTime, double coreTemp, CyclePhase currentPhase)
        {
            if (deltaTime <= 0.0) return;

            // --- 1. Υπολογισμός Αιθάλης (Soot) ---
            if (coreTemp < 450.0)
            {
                // Κάτω από 450 βαθμούς η αιθάλη συσσωρεύεται ταχύτατα
                SootFraction += (0.01 / 60.0) * deltaTime;
            }
            else if (coreTemp > 650.0)
            {
                // Πάνω από 650 βαθμούς, καίγεται και καθαρίζει ελαφρώς μόνη της
                SootFraction -= (0.005 / 60.0) * deltaTime;
            }
            SootFraction = Math.Clamp(SootFraction, 0.0, 1.0);

            // --- 2. Διαχείριση Πίεσης (Pressure) ---
            if (currentPhase == CyclePhase.DayActive)
            {
                // Τη μέρα, η πίεση αυξάνεται (Επιρροή από τη θερμοκρασία και την αιθάλη)
                double sootMultiplier = 1.0 + (SootFraction * 0.5); // H αιθάλη αυξάνει την πίεση γρηγορότερα
                double basePressureRate = (coreTemp / 550.0) * 0.5; // 0.5 Bar ανά δευτερόλεπτο σε κανονικές συνθήκες

                // Αν το κέλυφος είναι ραγισμένο, χάνουμε πίεση, καθιστώντας δύσκολη τη συγκέντρωση νομίσματος
                double crackLeak = IsHullCracked ? GameSettings.Instance.HullCrackedLeakRate : 0.0;

                double netPressureRate = (basePressureRate * sootMultiplier) - crackLeak;

                if (IsOverdriveActive) netPressureRate *= 2.0;

                SteamPressure += netPressureRate * deltaTime;
            }
            else if (currentPhase == CyclePhase.NightBleed)
            {
                // Τη νύχτα, η αποθηκευμένη πίεση αιμορραγεί αργά για να ζεστάνει την πόλη
                double nightBleedRate = GameSettings.Instance.NightBleedRate; // Ρυθμός απώλειας τη νύχτα
                SteamPressure -= nightBleedRate * deltaTime;
            }

            SteamPressure = Math.Clamp(SteamPressure, 0.0, 200.0);

            // --- 3. Διαχείριση Ζημιάς Κελύφους (Casing Integrity) ---
            // Πίεση πάνω από 180 προκαλεί συνεχή ζημιά (Over-pressurization damage)
            if (SteamPressure > 180.0)
            {
                CasingIntegrity -= (GameSettings.Instance.CasingDamagePressureMultiplier * deltaTime);
            }
            // Ακραία θερμοκρασία προκαλεί επίσης ζημιά
            if (coreTemp >= 850.0)
            {
                CasingIntegrity -= (GameSettings.Instance.CasingDamageHeatMultiplier * deltaTime);
            }

            // --- 4. Σύστημα Overdrive ---
            if (OverdriveLockout > 0.0)
            {
                OverdriveLockout -= deltaTime;
                IsOverdriveActive = false;
            }
            else if (IsOverdriveActive)
            {
                OverdriveStress += (10.0 / 60.0) * deltaTime;
                if (OverdriveStress >= 100.0)
                {
                    CasingIntegrity -= 15.0; // Τεράστια ζημιά
                    IsOverdriveActive = false;
                    OverdriveStress = 0.0;
                    OverdriveLockout = 28800.0; // 8 ώρες κλείδωμα
                }
            }
            else
            {
                if (OverdriveStress > 0.0)
                {
                    OverdriveStress -= (2.0 / 60.0) * deltaTime; // Αργή ψύξη
                }
            }

            CasingIntegrity = Math.Clamp(CasingIntegrity, 0.0, 100.0);
            OverdriveStress = Math.Clamp(OverdriveStress, 0.0, 100.0);
        }

        // Καλείται όταν ο παίκτης επιλέγει να μειώσει χειροκίνητα την αιθάλη (μέσω Flue Scour Job)
        public void CleanSoot(double amount)
        {
            SootFraction = Math.Clamp(SootFraction - amount, 0.0, 1.0);
        }

        // Καλείται για επισκευή του κελύφους (Patching Job)
        public void RepairCasing(double amount)
        {
            CasingIntegrity = Math.Clamp(CasingIntegrity + amount, 0.0, 100.0);
            if (CasingIntegrity > 50.0)
            {
                // Αν επισκευαστεί αρκετά, το ρήγμα σφραγίζει
                IsHullCracked = false;
            }
        }

        // Καλείται όταν γίνεται εκτόνωση νομίσματος (Vent Routing)
        public void DeductPressure(double amount)
        {
            SteamPressure = Math.Max(0.0, SteamPressure - amount);
        }

        // Καλείται από το Orchestrator αν λήξει ο χρόνος και ΔΕΝ έχει ασφάλειες ο παίκτης
        public void TriggerCatastrophicFailure()
        {
            IsHullCracked = true;
            CasingIntegrity -= 25.0;
            SteamPressure = Math.Max(0.0, SteamPressure - 50.0); // Χάνει πίεση βίαια λόγω ρήγματος
        }
    }
}