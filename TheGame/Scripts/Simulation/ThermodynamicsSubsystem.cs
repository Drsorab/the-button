using System;

namespace PermafrostChieftain.Simulation
{
    public enum DraftStance
    {
        BankedEmbers, // 0.4x burn rate (Χαμηλή συντήρηση)
        SteadyIdle,   // 1.0x burn rate (Κανονική καύση)
        ThermalSurge  // 1.8x burn rate (Επιθετική καύση - τεράστια αύξηση πίεσης)
    }

    public class ThermodynamicsSubsystem
    {

        public double CoreTemperature { get; private set; }
        public double CityTemperature { get; private set; }
        public double CurrentAmbientTemperature { get; set; }
        public double CityHeatBuffer { get; private set; } = 0.0;

        public DraftStance ActiveDraftStance { get; set; } = DraftStance.SteadyIdle;
        public double HullThermalLeakModifier { get; set; } = 1.0;

        public ThermodynamicsSubsystem()
        {
            // Αρχικοποίηση από τα settings όταν ξεκινάει το σύστημα
            CurrentAmbientTemperature = GameSettings.Instance.AmbientTemperature;
            CoreTemperature = GameSettings.Instance.StartingCoreTemp;
            CityTemperature = GameSettings.Instance.StartingCityTemp;
        }
        // Επιστρέφει την ποσότητα καπνιάς που παρήχθη αυτό το δευτερόλεπτο
        public double ProcessThermalTick(double deltaTime, double coalBurnedPerSec)
        {
            if (coalBurnedPerSec <= 0)
            {
                // Φυσική απώλεια θερμοκρασίας αν η φωτιά σβήσει
                double heatLoss = GameSettings.Instance.InactiveCoolingRate * deltaTime;

                // Εφαρμογή αδράνειας (Thermal Inertia) για να κρυώνει πιο ομαλά
                heatLoss *= GameSettings.Instance.CoreThermalInertia;

                CoreTemperature = Math.Max(CityTemperature, CoreTemperature - heatLoss);
                return 0.0;
            }

            // --- 1. ΥΠΟΛΟΓΙΣΜΟΣ ΑΠΟΔΟΣΗΣ ΚΑΜΙΝΙΟΥ (FURNACE EFFICIENCY) ---
            // Όταν το καμίνι είναι κρύο, η ενέργεια χάνεται στα μέταλλα. Όταν είναι καυτό, είναι 100% αποδοτικό.
            double furnaceEfficiency = 1.0;
            if (CoreTemperature < GameSettings.Instance.OptimalCoreTemp)
            {
                double deficitRatio = (GameSettings.Instance.OptimalCoreTemp - CoreTemperature) / GameSettings.Instance.OptimalCoreTemp;
                // Π.χ. αν είναι κρύο (deficitRatio = ~0.8), το efficiency πέφτει στο 52%
                furnaceEfficiency = 1.0 - (deficitRatio * GameSettings.Instance.ColdFurnaceEfficiencyPenalty);
                furnaceEfficiency = Math.Max(0.1, furnaceEfficiency); // Ποτέ κάτω από 10% απόδοση
            }

            // --- 2. ΠΑΡΑΓΩΓΗ & ΑΠΩΛΕΙΑ ΘΕΡΜΟΤΗΤΑΣ ---
            double rawHeatGain = coalBurnedPerSec * GameSettings.Instance.HeatGainPerKgCoal;
            double effectiveHeatGain = (rawHeatGain * furnaceEfficiency) * deltaTime;

            double heatLossWorking = (CoreTemperature - CityTemperature) * GameSettings.Instance.AmbientHeatLossRate * deltaTime;

            // --- 3. ΕΦΑΡΜΟΓΗ ΘΕΡΜΙΚΗΣ ΑΔΡΑΝΕΙΑΣ (THERMAL INERTIA) ---
            // Κάνει την άνοδο και την πτώση της θερμοκρασίας πιο "βαριά" και ρεαλιστική, αποτρέποντας απότομα άλματα
            double netTempChange = (effectiveHeatGain - heatLossWorking) * GameSettings.Instance.CoreThermalInertia;

            CoreTemperature = Math.Max(CityTemperature, CoreTemperature + netTempChange);

            // --- 4. ΥΠΟΛΟΓΙΣΜΟΣ ΚΑΠΝΙΑΣ (SMOLDERING PENALTY) ---
            double generatedSoot = GameSettings.Instance.BaseSootGenerationRate * deltaTime;

            if (CoreTemperature < GameSettings.Instance.OptimalCoreTemp)
            {
                double temperatureDeficit = (GameSettings.Instance.OptimalCoreTemp - CoreTemperature) / GameSettings.Instance.OptimalCoreTemp;
                double smolderingMultiplier = 1.0 + (temperatureDeficit * GameSettings.Instance.SmolderingSootPenalty);

                generatedSoot *= smolderingMultiplier;
            }

            return generatedSoot;
        }

        // Υπολογίζει δυναμικά τη θερμοκρασία της πόλης με βάση τη φυσική
        public void ProcessCityTemperature(double deltaTime, bool isGeneratorActive, double sootFraction)
        {
            if (deltaTime <= 0.0) return;

            // 1. Δύναμη Ψύξης: Απώλεια θερμότητας προς το περιβάλλον
            double ambientDifferential = CityTemperature - CurrentAmbientTemperature;
            double coolingRate = ambientDifferential * GameSettings.Instance.CityCoolingCoefficient;

            // 2. Δύναμη Θέρμανσης: Ατμός από τη Γεννήτρια
            double heatingRate = 0.0;

            // Για να θερμανθεί η πόλη, η γεννήτρια πρέπει να λειτουργεί ΚΑΙ να βράζει νερό
            if (isGeneratorActive && CoreTemperature >= GameSettings.Instance.MinimumHeatingThreshold)
            {
                double coreDifferential = Math.Max(0.0, CoreTemperature - CityTemperature);
                heatingRate = coreDifferential * GameSettings.Instance.CityHeatingCoefficient;

                // Η αιθάλη (Soot) λειτουργεί ως μονωτικό, μειώνοντας την απόδοση μεταφοράς θερμότητας
                double insulationFactor = 1.0 - (sootFraction * GameSettings.Instance.SootInsulationMultiplier);
                heatingRate *= Math.Max(0.1, insulationFactor); // Αποτροπή αρνητικής θέρμανσης
            }

            // 3. Εφαρμογή του τελικού αποτελέσματος (Net Change)
            double netChange = (heatingRate - coolingRate) * deltaTime;
            CityTemperature += netChange;

            // Η πόλη δεν μπορεί να γίνει πιο κρύα από τον εξωτερικό καιρό
            CityTemperature = Math.Max(CurrentAmbientTemperature, CityTemperature);

            if (CityHeatBuffer > 0)
            {
                double transferRate = (CityHeatBuffer * 0.05) + GameSettings.Instance.BufferTransferRate;
                double actualTransfer = Math.Min(CityHeatBuffer, transferRate);

                CityTemperature += actualTransfer;
                CityHeatBuffer -= actualTransfer;
            }
        }


        // Θέτει την ακριβή θερμοκρασία του πυρήνα με όριο την ελάχιστη εξωτερική
        public void SetCoreTemperature(double temp)
        {
            CoreTemperature = Math.Max(CurrentAmbientTemperature, temp);
        }

        // Θέτει την ακριβή θερμοκρασία της πόλης με όριο την *τρέχουσα* εξωτερική
        public void SetCityTemperature(double temp)
        {
            CityTemperature = Math.Max(CurrentAmbientTemperature, temp);
        }
        // Καλείται όταν ο παίκτης επιλέγει να στείλει πίεση/ατμό στο City Heating κατά το Vent
        public void AddCityHeatBuffer(double addedHeat)
        {
            CityHeatBuffer += addedHeat;
        }

        // Άμεση αλλαγή θερμοκρασίας (για emergency events)
        public void ApplyDirectThermalDelta(double coreDelta, double cityDelta)
        {
            CoreTemperature = Math.Clamp(CoreTemperature + coreDelta, CurrentAmbientTemperature, 1200.0);
            CityHeatBuffer += cityDelta;
        }
    }
}