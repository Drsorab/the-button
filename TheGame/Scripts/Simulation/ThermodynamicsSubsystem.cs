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

        public double CoreTemperature { get; private set; } = GameSettings.Instance.StartingCoreTemp;
        public double CityTemperature { get; private set; } = GameSettings.Instance.AmbientTemperature;
        public DraftStance ActiveDraftStance { get; set; } = DraftStance.SteadyIdle;

        // Συντελεστής που μειώνει την απόδοση αν το κέλυφος ραγίσει λόγω πίεσης
        public double HullThermalLeakModifier { get; set; } = 1.0;

        // Συσσωρευμένη θερμότητα από την πόλη που χάνεται σταδιακά (The Thermal Blanket)
        public double CityHeatBuffer { get; private set; } = 0.0;

        public double CurrentAmbientTemperature { get; set; }

        public ThermodynamicsSubsystem()
        {
            // Αρχικοποίηση από τα settings όταν ξεκινάει το σύστημα
            CurrentAmbientTemperature = GameSettings.Instance.AmbientTemperature;
        }
        // Επιστρέφει την ποσότητα καπνιάς που παρήχθη αυτό το δευτερόλεπτο
        public double ProcessThermalTick(double deltaTime, double coalBurnedPerSec)
        {
            if (coalBurnedPerSec <= 0)
            {
                // Φυσική απώλεια θερμοκρασίας αν η φωτιά σβήσει
                double heatLoss = (CoreTemperature - CityTemperature) * (GameSettings.Instance.AmbientHeatLossRate * 2.5) * deltaTime;
                CoreTemperature = Math.Max(CityTemperature, CoreTemperature - heatLoss);
                return 0.0;
            }

            // Υπολογισμός Θερμοκρασίας με τις μεταβλητές των Settings
            double heatGain = coalBurnedPerSec * GameSettings.Instance.HeatGainPerKgCoal;
            double heatLossWorking = (CoreTemperature - CityTemperature) * GameSettings.Instance.AmbientHeatLossRate * deltaTime;

            CoreTemperature = Math.Max(CityTemperature, CoreTemperature + (heatGain * deltaTime) - heatLossWorking);

            // Υπολογισμός Smoldering Penalty (Καπνιά)
            double generatedSoot = GameSettings.Instance.BaseSootGenerationRate * deltaTime;

            if (CoreTemperature < GameSettings.Instance.OptimalCoreTemp)
            {
                double temperatureDeficit = (GameSettings.Instance.OptimalCoreTemp - CoreTemperature) / GameSettings.Instance.OptimalCoreTemp;
                double smolderingMultiplier = 1.0 + (temperatureDeficit * GameSettings.Instance.SmolderingSootPenalty);

                generatedSoot *= smolderingMultiplier;
            }

            return generatedSoot;
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