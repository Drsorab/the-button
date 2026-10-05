using System;

namespace PermafrostChieftain.Simulation
{
    public class BoilerSubsystem
    {
        public double SteamPressure { get; private set; } = GameSettings.Instance.StartingPressure;
        public double CasingIntegrity { get; private set; } = 100.0;
        public double SootFraction { get; private set; } = 0.0;

        public int AvailableFuses { get; set; } = 3;

        // Κατάσταση βλάβης αν δεν πατηθεί το κουμπί και δεν υπάρχουν ασφάλειες
        public bool IsHullCracked { get; private set; } = false;

        public void ProcessTick(double deltaTime, double coreTemp, CyclePhase currentPhase)
        {
            if (deltaTime <= 0.0) return;

            // Η αιθάλη (soot) δρα ως μόνωση
            double insulationFactor = 1.0 - (SootFraction * GameSettings.Instance.SootInsulationMultiplier);
            double effectiveWaterTemp = coreTemp * Math.Max(0.1, insulationFactor);

            double heatDelta = effectiveWaterTemp - GameSettings.Instance.BoilingPointThreshold;
            double thermodynamicPressureRate = 0.0;

            if (heatDelta > 0)
            {
                // NORMALIZED HEAT RATIO:
                // Υπολογίζει πόσο κοντά είμαστε στην ιδανική θερμοκρασία.
                // Στους 750°C (Optimal) το ratio είναι 1.0, άρα παράγουμε ακριβώς το BasePressure (0.015).
                // Στους 350°C το ratio είναι 0.2, άρα παράγουμε το 20% της μέγιστης πίεσης.
                double optimalDelta = GameSettings.Instance.OptimalCoreTemp - GameSettings.Instance.BoilingPointThreshold;
                double heatRatio = heatDelta / optimalDelta;

                double baseBoilRate = heatRatio * GameSettings.Instance.BasePressureGenerationRate;

                // Το soot φράζει τις βαλβίδες αυξάνοντας την πίεση
                double clogMultiplier = 1.0 + (SootFraction * (GameSettings.Instance.MaxSootPressureMultiplier - 1.0));

                thermodynamicPressureRate = baseBoilRate * clogMultiplier;
            }
            else
            {
                // Ο πυρήνας είναι πιο κρύος από το βρασμό. Αρχίζει η συμπύκνωση ατμού.
                thermodynamicPressureRate = -(Math.Abs(heatDelta) * GameSettings.Instance.CondensationRateCoefficient);
            }

            double totalBleedRate = GameSettings.Instance.NaturalHullBleedRate;

            if (IsHullCracked)
            {
                totalBleedRate += GameSettings.Instance.HullCrackedLeakRate;
            }

            if (currentPhase == CyclePhase.NightBleed)
            {
                totalBleedRate += GameSettings.Instance.NightBleedRate;
            }

            // Εφαρμογή
            SteamPressure += (thermodynamicPressureRate - totalBleedRate) * deltaTime;
            SteamPressure = Math.Clamp(SteamPressure, 0.0, GameSettings.Instance.MaxPressure);

            // --- 3. Διαχείριση Ζημιάς Κελύφους ---
            if (SteamPressure > GameSettings.Instance.PressureDamageThreshold)
            {
                CasingIntegrity -= (GameSettings.Instance.CasingDamagePressureMultiplier * deltaTime);
            }

            if (coreTemp >= GameSettings.Instance.HeatDamageThreshold)
            {
                CasingIntegrity -= (GameSettings.Instance.CasingDamageHeatMultiplier * deltaTime);
            }

            CasingIntegrity = Math.Clamp(CasingIntegrity, 0.0, 100.0);
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

        public void SetCasingStress(double amount)
        {
            CasingIntegrity = amount;
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