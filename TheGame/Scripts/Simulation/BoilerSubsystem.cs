using System;

namespace PermafrostChieftain.Simulation
{
    public class BoilerSubsystem
    {
        public double SteamPressure { get; private set; } = GameSettings.Instance.StartingPressure;
        public double CasingIntegrity { get; private set; } = 100.0;
        public double SootFraction { get; private set; } = 0.0;
        // Κρατάει το ποσό της επισκευής που λείπει για να κλείσει η τρύπα
        private double _pendingRepairToSeal = 0.0;

        // Κρατάει τον χρόνο που παραμένει ανοιχτό το ρήγμα (για να αυξάνεται η διαρροή)
        private double _crackDuration = 0.0;
        public int AvailableFuses { get; set; } = 3;

        // Κατάσταση βλάβης αν δεν πατηθεί το κουμπί και δεν υπάρχουν ασφάλειες
        public bool IsHullCracked { get; private set; } = false;

        public void ProcessTick(double deltaTime, double coreTemp, CyclePhase currentPhase)
        {
            if (deltaTime <= 0.0) return;

            // --- 1. Υπολογισμός Θερμοδυναμικής Πίεσης ---
            // Η αιθάλη (soot) δρα ως μόνωση
            double insulationFactor = 1.0 - (SootFraction * GameSettings.Instance.SootInsulationMultiplier);
            double effectiveWaterTemp = coreTemp * Math.Max(0.1, insulationFactor);

            double heatDelta = effectiveWaterTemp - GameSettings.Instance.BoilingPointThreshold;
            double thermodynamicPressureRate = 0.0;

            if (heatDelta > 0)
            {
                // NORMALIZED HEAT RATIO:
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

            // --- 2. Υπολογισμός Απώλειας Πίεσης (Bleed) ---
            double totalBleedRate = GameSettings.Instance.NaturalHullBleedRate;

            if (currentPhase == CyclePhase.NightBleed)
            {
                totalBleedRate += GameSettings.Instance.NightBleedRate;
            }

            // Εφαρμογή φυσιολογικής πίεσης / συμπύκνωσης / σταθερού bleed
            SteamPressure += (thermodynamicPressureRate - totalBleedRate) * deltaTime;

            // --- ΝΕΟ: ΔΥΝΑΜΙΚΗ ΔΙΑΡΡΟΗ ΡΗΓΜΑΤΟΣ (CRACK LEAK) ---
            // Αντικαθιστά το παλιό, σταθερό HullCrackedLeakRate
            if (IsHullCracked)
            {
                _crackDuration += deltaTime;

                // Η διαρροή ξεκινάει σιγά-σιγά και επιταχύνεται. 
                // Το 0.1 είναι ο ρυθμός βάσης, επί τον χρόνο (σε δευτερόλεπτα), επί τον multiplier από τα Settings.
                double leakRate = (_crackDuration * 0.1) * GameSettings.Instance.HullCrackPressureLeakMultiplier;

                // Αφαιρούμε άμεσα την πίεση (ανεξάρτητα από τον κανονικό βρασμό)
                SteamPressure -= (leakRate * deltaTime);
            }

            SteamPressure = Math.Clamp(SteamPressure, 0.0, GameSettings.Instance.MaxPressure);

            // --- 3. Διαχείριση Ζημιάς Κελύφους (Degradation) ---
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
            // Αυξάνουμε τη συνολική αντοχή, αλλά δεν ξεπερνάμε το 100%
            CasingIntegrity = Math.Clamp(CasingIntegrity + amount, 0.0, 100.0);

            // Αν το κέλυφος είναι ραγισμένο, αφαιρούμε την επισκευή από την "οφειλή"
            if (IsHullCracked)
            {
                _pendingRepairToSeal -= amount;

                // Αν ρίξαμε αρκετό Scrap για να καλύψουμε την τρύπα
                if (_pendingRepairToSeal <= 0.0)
                {
                    IsHullCracked = false;
                    _pendingRepairToSeal = 0.0;
                    _crackDuration = 0.0; // Το ρήγμα έκλεισε, μηδενίζουμε τον χρόνο διαρροής
                }
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

            // Το κέλυφος παθαίνει τη ζημιά που ορίσαμε στα Settings
            CasingIntegrity = Math.Max(0.0, CasingIntegrity - GameSettings.Instance.CatastrophicHullDamage);

            // Απαιτείται τόση επισκευή όση βάλαμε στα Settings για να σφραγίσει ξανά
            _pendingRepairToSeal = GameSettings.Instance.IntegrityRepairRequiredToSeal;

            // Μηδενίζουμε τον χρόνο ρήγματος για να ξεκινήσει η κλιμακούμενη διαρροή
            _crackDuration = 0.0;

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