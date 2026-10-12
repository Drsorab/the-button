using System;
using System.Collections.Generic;

namespace PermafrostChieftain.Simulation
{
    // Καθαρίσαμε τα παλιά Jobs και κρατήσαμε αυστηρά τον πυρήνα που συμφωνήσαμε!
    public enum JobType
    {
        StokeFurnace,       // Διακριτό: Ρίχνουν κάρβουνο
        CleanSoot,          // Διακριτό: Καθαρίζουν καπνιά
        DaytimePatch,       // Διακριτό: Επικίνδυνο μπάλωμα ρήγματος (Ημέρα)
        NightHullRepair,    // Διακριτό: Μόνιμη επισκευή αντοχής (Νύχτα)
        WorkshopCrafting,   // Διακριτό: Κατασκευή αντικειμένων
        WorkshopUpkeep,     // Διακριτό: Συντήρηση μηχανουργείου
        CoalMining,         // ΣΥΝΕΧΟΜΕΝΟ: Εξόρυξη Άνθρακα
        ScrapScavenging,    // ΣΥΝΕΧΟΜΕΝΟ: Ανασκαφή Πάγων για Παλιοσίδερα
        MetalSmelting,      // ΣΥΝΕΧΟΜΕΝΟ: Λιώσιμο Scrap σε Refined Metal
        OperateTurbines     // ΣΥΝΕΧΟΜΕΝΟ: Παραγωγή Ρεύματος (Τουρμπίνες)
    }

    public class JobOrder
    {
        public JobType Type { get; set; }
        public int AssignedWorkers { get; set; }
        public int OptimalWorkers { get; set; }
        public double WorkRemaining { get; set; } // Χρησιμοποιείται μόνο για τα Διακριτά (Discrete) Jobs

        public void SetWorkRemaining(double amount) => WorkRemaining = amount;
    }

    public class JobManager
    {
        public Dictionary<JobType, JobOrder> Jobs { get; private set; }

        // Το χρονόμετρο για το Buff του Τρυπανιού (Σε δευτερόλεπτα)
        public double DrillBuffTimer { get; private set; } = 0.0;

        public JobManager()
        {
            Jobs = new Dictionary<JobType, JobOrder>();
            // Αρχικοποίηση όλων των εργασιών (με 0 εργάτες στην αρχή)
            foreach (JobType type in Enum.GetValues(typeof(JobType)))
            {
                Jobs[type] = new JobOrder { Type = type, AssignedWorkers = 0, OptimalWorkers = 15 };
            }
        }

        // =========================================================
        // ΜΗΧΑΝΙΣΜΟΣ DRILL BUFF (Από το 108-minute Routing)
        // =========================================================
        // Όταν ο Orchestrator ρίχνει ατμό στο Drill, καλεί αυτή τη μέθοδο
        public void AddDrillBuff(double steamRouted)
        {
            if (steamRouted <= 0) return;

            // Κάθε Bar ατμού δίνει π.χ. 10 δευτερόλεπτα Buff
            double buffDuration = steamRouted * GameSettings.Instance.SteamDrillBuffDurationPerBar;
            DrillBuffTimer += buffDuration;
        }

        // =========================================================
        // ΕΚΤΕΛΕΣΗ ΣΥΝΕΧΟΜΕΝΩΝ ΕΡΓΑΣΙΩΝ (CONTINUOUS ECONOMY)
        // =========================================================
        // Επιστρέφει τον αριθμό των "Ενεργών Χυτευτών" (Active Smelters) για να 
        // το περάσει ο Orchestrator στη Θερμοδυναμική και να ρίξει τη θερμοκρασία!
        public (int activeSmelters, double thermalShock, double coalBurnedPerSec) ProcessContinuousJobs(double deltaTime, LogisticsSubsystem logistics, BoilerSubsystem boiler, PopulationSubsystem population)
        {
            if (deltaTime <= 0.0) return (0, 0.0, 0.0);

            int activeSmelters = 0;
            double totalThermalShock = 0.0;
            double totalCoalBurned = 0.0;

            // 1. Ενημέρωση του Χρονόμετρου Buff
            if (DrillBuffTimer > 0)
            {
                DrillBuffTimer -= deltaTime;
                if (DrillBuffTimer < 0) DrillBuffTimer = 0.0;
            }

            foreach (var job in Jobs.Values)
            {
                if (job.AssignedWorkers <= 0) continue;

                switch (job.Type)
                {
                    case JobType.StokeFurnace:
                        // Υπολογίζουμε πόσο κάρβουνο ζητάνε οι εργάτες
                        double requestedCoal = job.AssignedWorkers * GameSettings.Instance.StokerCoalConsumptionPerWorker * deltaTime;
                        // Καταναλώνουμε το διαθέσιμο κάρβουνο
                        totalCoalBurned += logistics.ConsumeUpToCoal(requestedCoal);
                        break;

                    case JobType.CoalMining:
                        double mineMultiplier = (DrillBuffTimer > 0) ? GameSettings.Instance.CoalMiningDrillBuffMultiplier : 1.0;
                        double coalProduced = job.AssignedWorkers * GameSettings.Instance.CoalMiningBaseRatePerWorker * mineMultiplier * deltaTime;
                        logistics.AddCoal(coalProduced);
                        break;

                    case JobType.ScrapScavenging:
                        double thawMultiplier = population.CurrentCityStage switch
                        {
                            CityLivingStage.Freezing => 0.1,
                            CityLivingStage.Cold => 0.5,
                            CityLivingStage.Livable => 1.0,
                            CityLivingStage.Warm => 1.5,
                            CityLivingStage.Comfortable => 2.0,
                            _ => 1.0
                        };
                        double scrapProduced = job.AssignedWorkers * GameSettings.Instance.ScavengeScrapBaseRatePerWorker * thawMultiplier * deltaTime;
                        logistics.AddRawScrap(scrapProduced);
                        break;

                    case JobType.MetalSmelting:
                        double requiredScrap = job.AssignedWorkers * GameSettings.Instance.SmeltingRawScrapCostPerWorker * deltaTime;
                        double availableScrap = Math.Min(requiredScrap, logistics.RawScrap);

                        if (availableScrap > 0)
                        {
                            logistics.TryConsumeRawScrap(availableScrap);
                            double ratio = availableScrap / requiredScrap;
                            double metalProduced = (job.AssignedWorkers * ratio) * GameSettings.Instance.SmeltingRefinedMetalRatePerWorker * deltaTime;
                            logistics.AddRefinedMetal(metalProduced);

                            activeSmelters = (int)(job.AssignedWorkers * ratio);
                        }
                        break;

                    case JobType.OperateTurbines:
                        double requiredSteam = job.AssignedWorkers * GameSettings.Instance.TurbineSteamCostPerWorker * deltaTime;

                        if (boiler.SteamPressure > 0)
                        {
                            double consumedSteam = Math.Min(requiredSteam, boiler.SteamPressure);
                            boiler.DeductSteam(consumedSteam);

                            // Εδώ εφαρμόζεται το Θερμικό Σοκ!
                            totalThermalShock += consumedSteam * GameSettings.Instance.TurbineThermalShockPerBar;

                            double ratio = consumedSteam / requiredSteam;
                            double electricityProduced = (job.AssignedWorkers * ratio) * GameSettings.Instance.TurbinePowerPerWorker * deltaTime;
                            logistics.AddElectricity(electricityProduced);
                        }
                        break;
                }
            }

            // Μετατρέπουμε το συνολικό κάρβουνο που κάηκε σε "ρυθμό ανά δευτερόλεπτο" για τη Θερμοδυναμική
            double coalBurnedPerSec = totalCoalBurned / deltaTime;

            // Επιστρέφουμε και τις 3 τιμές με ασφάλεια!
            return (activeSmelters, totalThermalShock, coalBurnedPerSec);
        }
    }
}