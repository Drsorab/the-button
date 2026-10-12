using System;

namespace PermafrostChieftain.Simulation
{
    public class ThermodynamicsSubsystem
    {
        public double CoreTemperature { get; private set; }
        public double CityTemperature { get; private set; }

        // Το δίκτυο θέρμανσης της πόλης
        public double CitySteamBuffer { get; private set; } = 0.0;
        public double TargetCityTemperature { get; private set; } = GameSettings.Instance.TempStage_Comfortable;

        public void Initialize(double startCoreTemp, double startCityTemp)
        {
            CoreTemperature = startCoreTemp;
            CityTemperature = startCityTemp;
            CitySteamBuffer = 0.0;
        }

        // Ορίζει τον στόχο της θερμοκρασίας για την πόλη (από το UI)
        public void SetTargetCityTemperature(double targetTemp)
        {
            TargetCityTemperature = targetTemp;
        }

        // Τροφοδοτεί το δίκτυο της πόλης με νέο ατμό
        public void AddSteamToCityBuffer(double steamAmount)
        {
            CitySteamBuffer += steamAmount;
        }

        // =========================================================
        // ΥΠΟΛΟΓΙΣΤΗΣ ΠΡΟΒΛΕΨΗΣ ΓΙΑ ΤΟ UI (Calculator)
        // Επιστρέφει πόσες in-game Ώρες θα αντέξει ο ατμός στον στόχο
        // =========================================================
        public double CalculateSteamDurationHours(double projectedSteamToAdd, double targetTemp)
        {
            double totalSteam = CitySteamBuffer + projectedSteamToAdd;
            double steamYield = GameSettings.Instance.SteamToCityHeatYield;

            // 1. Πόσος ατμός χρειάζεται για να φτάσουμε στη θερμοκρασία
            double steamNeededToHeatUp = 0.0;
            if (CityTemperature < targetTemp)
            {
                double tempDifference = targetTemp - CityTemperature;
                steamNeededToHeatUp = tempDifference / steamYield;
            }

            if (totalSteam <= steamNeededToHeatUp)
            {
                return 0.0; // Δεν φτάνει καν για να ζεστάνει την πόλη
            }

            double remainingSteamForUpkeep = totalSteam - steamNeededToHeatUp;

            // 2. Πόση θερμότητα χάνεται ανά δευτερόλεπτο στον Στόχο (Newton's Law)
            double targetTempDelta = targetTemp - GameSettings.Instance.AmbientWeatherTemperature;
            double heatLossPerSec = targetTempDelta * GameSettings.Instance.CityWeatherCoolingCoefficient;

            // 3. Απαίτηση ατμού ανά δευτερόλεπτο για συντήρηση
            double steamRequiredPerSec = heatLossPerSec / steamYield;

            if (steamRequiredPerSec <= 0) return 999.0; // Αν ο καιρός είναι ζεστός (σπάνιο)

            // 4. Μετατροπή δευτερολέπτων σε Ώρες (In-Game Hours)
            double durationInSeconds = remainingSteamForUpkeep / steamRequiredPerSec;
            return durationInSeconds / 3600.0;
        }

        // =========================================================
        // ΚΕΝΤΡΙΚΟΣ ΚΥΚΛΟΣ ΘΕΡΜΟΔΥΝΑΜΙΚΗΣ (Εκτελείται κάθε frame)
        // =========================================================
        public double ProcessTick(double deltaTime, double coalBurnedPerSec, int smeltingWorkers, double thermalShock)
        {
            if (deltaTime <= 0.0) return 0.0;

            // ---------------------------------------------------------
            // ΦΑΣΗ 1: ΘΕΡΜΟΔΥΝΑΜΙΚΗ ΤΟΥ ΠΥΡΗΝΑ (CORE)
            // ---------------------------------------------------------
            double energyInput = 0.0;
            if (coalBurnedPerSec > 0)
            {
                // Ακατέργαστη ενέργεια από το κάρβουνο
                double rawEnergy = coalBurnedPerSec * GameSettings.Instance.HeatGainPerKgCoal;

                // Απόδοση καμινιού βάσει τρέχουσας θερμοκρασίας (κρύο = χαμηλή απόδοση)
                double deficitRatio = CoreTemperature < GameSettings.Instance.OptimalCoreTemp
                    ? (GameSettings.Instance.OptimalCoreTemp - CoreTemperature) / GameSettings.Instance.OptimalCoreTemp
                    : 0.0;
                double furnaceEfficiency = Math.Max(0.2, 1.0 - (deficitRatio * 0.5));

                energyInput = rawEnergy * furnaceEfficiency;
            }

            // Νόμος Ψύξης Νεύτωνα: Η απώλεια είναι ανάλογη της διαφοράς θερμοκρασίας
            double temperatureDelta = CoreTemperature - GameSettings.Instance.AmbientWeatherTemperature;
            double weatherEnergyLoss = temperatureDelta * GameSettings.Instance.GeneratorWeatherCoolingCoefficient;

            // Το χυτήριο ρουφάει ενέργεια
            double smeltingEnergyLoss = smeltingWorkers * GameSettings.Instance.SmeltingHeatDrainPerWorker;

            // Καθαρή ενέργεια (Q_net)
            double netEnergy = (energyInput - weatherEnergyLoss - smeltingEnergyLoss);
            double steamGenerated = 0.0;

            // Λανθάνουσα Θερμότητα: Μετατροπή πλεονάσματος σε ατμό αν βράζει το νερό
            if (netEnergy > 0 && CoreTemperature >= GameSettings.Instance.BoilingPointThreshold)
            {
                double energyForSteam = netEnergy * GameSettings.Instance.LatentHeatSteamConversionRatio;
                netEnergy -= energyForSteam;
                steamGenerated = energyForSteam * GameSettings.Instance.BaseSteamGenerationRate * deltaTime;
            }

            // Θερμικό Σοκ: Ακαριαία αφαίρεση λόγω βίαιης αποσυμπίεσης (από Τουρμπίνες)
            CoreTemperature -= thermalShock;

            // Θερμοχωρητικότητα: Αλλαγή θερμοκρασίας βάσει της μάζας του μετάλλου
            double tempChange = (netEnergy / GameSettings.Instance.CoreThermalMass) * deltaTime;
            CoreTemperature += tempChange;

            // Δεν μπορεί να πέσει κάτω από τη θερμοκρασία του περιβάλλοντος
            CoreTemperature = Math.Max(GameSettings.Instance.AmbientWeatherTemperature, CoreTemperature);

            // ---------------------------------------------------------
            // ΦΑΣΗ 2: ΘΕΡΜΟΔΥΝΑΜΙΚΗ ΤΗΣ ΠΟΛΗΣ (CITY STEAM BUFFER)
            // ---------------------------------------------------------
            double cityTempDelta = CityTemperature - GameSettings.Instance.AmbientWeatherTemperature;
            double naturalHeatLoss = cityTempDelta * GameSettings.Instance.CityWeatherCoolingCoefficient * deltaTime;

            // Αν η πόλη είναι πιο κρύα από τον στόχο ΚΑΙ έχουμε ατμό αποθηκευμένο
            if (CityTemperature < TargetCityTemperature && CitySteamBuffer > 0)
            {
                // Απαιτούμενη θερμότητα (Αναπλήρωση απώλειας + 10% προσέγγιση στόχου ανά frame)
                double heatNeeded = naturalHeatLoss + ((TargetCityTemperature - CityTemperature) * 0.1);
                double steamNeeded = heatNeeded / GameSettings.Instance.SteamToCityHeatYield;

                double steamToConsume = Math.Min(steamNeeded, CitySteamBuffer);
                CitySteamBuffer -= steamToConsume;

                double generatedHeat = steamToConsume * GameSettings.Instance.SteamToCityHeatYield;

                CityTemperature += generatedHeat - naturalHeatLoss;
            }
            else
            {
                // Αν δεν έχουμε ατμό, η πόλη απλά παγώνει
                CityTemperature -= naturalHeatLoss;
            }

            CityTemperature = Math.Max(GameSettings.Instance.AmbientWeatherTemperature, CityTemperature);

            // Επιστρέφουμε τον ατμό που φτιάχτηκε για να τον πάρει ο Λέβητας (Boiler)
            return steamGenerated;
        }
    }
}