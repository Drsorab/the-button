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

            // --- 1. Διαχείριση Πίεσης (Pressure) ---
            if (currentPhase == CyclePhase.DayActive)
            {
                double netPressureRate = 0.0;

                // Το νερό δεν "νιώθει" όλη τη θερμοκρασία γιατί η καπνιά δρα ως μόνωση
                double insulationFactor = 1.0 - (SootFraction * GameSettings.Instance.SootInsulationMultiplier);
                double effectiveWaterTemp = coreTemp * Math.Max(0.1, insulationFactor); // Δεν πέφτει ποτέ κάτω από το 10% της θερμότητας

                if (effectiveWaterTemp >= GameSettings.Instance.BoilingPointThreshold)
                {
                    double basePressureRate = (effectiveWaterTemp / GameSettings.Instance.OptimalCoreTemp) * GameSettings.Instance.BasePressureGenerationRate;
                    double crackLeak = IsHullCracked ? GameSettings.Instance.HullCrackedLeakRate : 0.0;

                    netPressureRate = basePressureRate - crackLeak;

                    if (IsOverdriveActive) netPressureRate *= 2.0;
                }
                else
                {
                    // Αν δεν βράζει, η πίεση πέφτει μόνο αν υπάρχει ρήγμα στο κέλυφος
                    double crackLeak = IsHullCracked ? GameSettings.Instance.HullCrackedLeakRate : 0.0;
                    netPressureRate = -crackLeak;
                }

                SteamPressure += netPressureRate * deltaTime;
            }
            else if (currentPhase == CyclePhase.NightBleed)
            {
                // Αργή αποσυμπίεση κατά τη διάρκεια της νύχτας
                double nightLeak = GameSettings.Instance.NightBleedRate * deltaTime;
                double crackLeak = IsHullCracked ? GameSettings.Instance.HullCrackedLeakRate : 0.0;

                SteamPressure -= (nightLeak + crackLeak);
            }
            else
            {
                // Σε ενδιάμεσες φάσεις (π.χ. Twilight), χάνουμε πίεση μόνο αν υπάρχει ρήγμα
                double crackLeak = IsHullCracked ? GameSettings.Instance.HullCrackedLeakRate : 0.0;
                SteamPressure -= crackLeak;
            }

            // Όριο βάσει του Settings
            SteamPressure = Math.Clamp(SteamPressure, 0.0, GameSettings.Instance.MaxPressure);

            // --- 2. Διαχείριση Ζημιάς Κελύφους (Casing Integrity) ---

            // Υπερπίεση (Over-pressurization damage)
            if (SteamPressure > GameSettings.Instance.PressureDamageThreshold)
            {
                CasingIntegrity -= (GameSettings.Instance.CasingDamagePressureMultiplier * deltaTime);
            }

            // Ακραία θερμοκρασία
            if (coreTemp >= GameSettings.Instance.HeatDamageThreshold)
            {
                CasingIntegrity -= (GameSettings.Instance.CasingDamageHeatMultiplier * deltaTime);
            }

            // --- 3. Σύστημα Overdrive ---
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

        public void AddSoot(double amount)
        {
            SootFraction = Math.Clamp(SootFraction + amount, 0.0, 1.0);
        }

        public void RemoveSoot(double amount)
        {
            SootFraction = Math.Clamp(SootFraction - amount, 0.0, 1.0);
        }

        public void DamageCasing(double amount)
        {
            CasingIntegrity = Math.Clamp(CasingIntegrity - amount, 0.0, 100.0);
        }
    }
}