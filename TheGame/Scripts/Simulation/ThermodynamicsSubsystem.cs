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
        // Ενημερωμένη ενεργειακή πυκνότητα για θετικό ισοζύγιο
        private const double CoalEnergyDensity = 320000.0;
        private const double BaseThermalLoss = 27450.0;
        private const double SpecificHeatCore = 450000.0;
        private const double AmbientTempBase = -40.0;

        public double CoreTemperature { get; private set; } = GameSettings.Instance.StartingCoreTemp;
        public double CityTemperature { get; private set; } = -40.0;
        public DraftStance ActiveDraftStance { get; set; } = DraftStance.SteadyIdle;

        // Συντελεστής που μειώνει την απόδοση αν το κέλυφος ραγίσει λόγω πίεσης
        public double HullThermalLeakModifier { get; set; } = 1.0;

        // Συσσωρευμένη θερμότητα από την πόλη που χάνεται σταδιακά (The Thermal Blanket)
        public double CityHeatBuffer { get; private set; } = 0.0;

        public void ProcessThermalTick(double deltaTime, double coalBurnedRate, double sootLevel)
        {
            if (deltaTime <= 0.0) return;

            // Υπολογισμός πολλαπλασιαστή καύσης (Η αιθάλη κάνει τη φωτιά να καίει πιο ζεστά και ανεξέλεγκτα)
            double stanceMultiplier = ActiveDraftStance switch
            {
                DraftStance.BankedEmbers => 0.4,
                DraftStance.SteadyIdle => 1.0,
                DraftStance.ThermalSurge => 1.8,
                _ => 1.0
            };

            // H αιθάλη (soot) αυξάνει τη θερμοκρασία και την κατανάλωση έως και 30%
            double sootMultiplier = 1.0 + (sootLevel * 0.3);

            double energyGenerated = coalBurnedRate * CoalEnergyDensity * stanceMultiplier * sootMultiplier;

            // Το ρήγμα στο κέλυφος (Hull Leak) προκαλεί τεράστια θερμική απώλεια
            double thermalLoss = BaseThermalLoss * HullThermalLeakModifier;
            double netEnergy = energyGenerated - thermalLoss;

            CoreTemperature += (netEnergy / SpecificHeatCore) * deltaTime;

            // Σκληρά όρια θερμοκρασίας πυρήνα
            CoreTemperature = Math.Clamp(CoreTemperature, AmbientTempBase, 1200.0);

            // Μείωση του θερμικού "αποθέματος" της πόλης (Thermal Blanket) στο κρύο
            if (CityHeatBuffer > 0)
            {
                // Το απόθεμα μειώνεται σταθερά. 
                double heatLossRate = 1.5; // Χάνει 1.5 βαθμούς ανά δευτερόλεπτο (προσαρμόζεται για balance)
                CityHeatBuffer -= heatLossRate * deltaTime;
                if (CityHeatBuffer < 0) CityHeatBuffer = 0;
            }

            // Η θερμοκρασία της πόλης βασίζεται στο περιβάλλον + το τρέχον απόθεμα
            CityTemperature = AmbientTempBase + CityHeatBuffer;
        }

        // Καλείται όταν ο παίκτης επιλέγει να στείλει πίεση/ατμό στο City Heating κατά το Vent
        public void AddCityHeatBuffer(double addedHeat)
        {
            CityHeatBuffer += addedHeat;
        }

        // Άμεση αλλαγή θερμοκρασίας (για emergency events)
        public void ApplyDirectThermalDelta(double coreDelta, double cityDelta)
        {
            CoreTemperature = Math.Clamp(CoreTemperature + coreDelta, AmbientTempBase, 1200.0);
            CityHeatBuffer += cityDelta;
        }
    }
}