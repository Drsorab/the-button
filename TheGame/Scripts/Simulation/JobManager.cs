using System;
using System.Collections.Generic;

namespace PermafrostChieftain.Simulation
{
    public enum JobType
    {
        StokeFurnace,
        CleanSoot,
        DaytimePatch,
        NightHullRepair,
        WorkshopCraftPatch,
        WorkshopCraftFuse,
        WorkshopUpkeep,
        CoalMining,
        ScrapScavenging,
        MetalSmelting,
        OperateTurbines,
        IntensiveCare
    }

    public class JobOrder
    {
        public JobType Type { get; set; }
        public int AssignedWorkers { get; set; }
        public int OptimalWorkers { get; set; }
        public double WorkRemaining { get; set; }

        public void SetWorkRemaining(double amount) => WorkRemaining = amount;

        // ΝΕΟ: Υπολογισμός πραγματικής απόδοσης με "Diminishing Returns" αν ξεπεραστεί το Optimal
        public double GetEffectiveWorkers()
        {
            if (AssignedWorkers <= OptimalWorkers)
            {
                return AssignedWorkers; // 100% απόδοση μέχρι το όριο
            }

            // Για κάθε εργάτη πάνω από το βέλτιστο, η απόδοσή του μειώνεται.
            // Π.χ. αν Optimal=5 και βάλεις 10, οι πρώτοι 5 δίνουν 5, οι επόμενοι 5 δίνουν λιγότερο (π.χ. 0.5 έκαστος).
            int excessWorkers = AssignedWorkers - OptimalWorkers;
            return OptimalWorkers + Math.Pow(excessWorkers, 0.7);
        }
    }

    public class JobManager
    {
        public Dictionary<JobType, JobOrder> Jobs { get; private set; }

        // Το χρονόμετρο για το Buff του Τρυπανιού
        public double DrillBuffTimer { get; private set; } = 0.0;
        public string ActiveCraftingRecipe { get; set; } = "Metal Patch";

        public JobManager()
        {
            Jobs = new Dictionary<JobType, JobOrder>();

            // ΑΡΧΙΚΟΠΟΙΗΣΗ ΟΛΩΝ ΤΩΝ JOBS ΜΕ ΤΟ ΣΩΣΤΟ BASE WORK ΚΑΙ OPTIMAL WORKERS
            foreach (JobType type in Enum.GetValues(typeof(JobType)))
            {
                double baseWork = type switch
                {
                    JobType.CleanSoot => GameSettings.Instance.CleanSootWorkRequired,
                    JobType.DaytimePatch => GameSettings.Instance.PatchWorkRequired,
                    JobType.NightHullRepair => GameSettings.Instance.NightRepairWorkRequired,
                    JobType.WorkshopCraftPatch => GameSettings.Instance.CraftMetalPatchWorkRequired,
                    JobType.WorkshopCraftFuse => GameSettings.Instance.CraftFuseWorkRequired,
                    _ => 0.0 // Για τις συνεχόμενες εργασίες
                };

                // Ορίζουμε έναν γενικό κανόνα για Optimal Workers. (Ιδανικά, στο μέλλον θα το διαβάζει από τα Settings)
                int optimal = type switch
                {
                    JobType.StokeFurnace => 5,
                    JobType.CleanSoot => 8,
                    JobType.DaytimePatch => 10,
                    JobType.OperateTurbines => 3,
                    _ => 15 // Γενικό όριο για τις υπόλοιπες δουλειές
                };

                Jobs[type] = new JobOrder { Type = type, AssignedWorkers = 0, OptimalWorkers = optimal, WorkRemaining = baseWork };
            }
        }

        public void AddDrillBuff(double steamRouted)
        {
            if (steamRouted <= 0) return;
            double buffDuration = steamRouted * GameSettings.Instance.SteamDrillBuffDurationPerBar;
            DrillBuffTimer += buffDuration;
        }

        // =========================================================
        // 1. ΕΚΤΕΛΕΣΗ ΣΥΝΕΧΟΜΕΝΩΝ ΕΡΓΑΣΙΩΝ (CONTINUOUS ECONOMY)
        // =========================================================
        public (int activeSmelters, double thermalShock, double coalBurnedPerSec) ProcessContinuousJobs(double deltaTime, LogisticsSubsystem logistics, BoilerSubsystem boiler, PopulationSubsystem population)
        {
            if (deltaTime <= 0.0) return (0, 0.0, 0.0);

            int activeSmelters = 0;
            double totalThermalShock = 0.0;
            double totalCoalBurned = 0.0;

            if (DrillBuffTimer > 0)
            {
                DrillBuffTimer -= deltaTime;
                if (DrillBuffTimer < 0) DrillBuffTimer = 0.0;
            }

            foreach (var job in Jobs.Values)
            {
                if (job.AssignedWorkers <= 0) continue;

                // Υπολογίζουμε τους "Πραγματικούς/Αποτελεσματικούς" Εργάτες βάσει του Penalty
                double effectiveWorkers = job.GetEffectiveWorkers();

                switch (job.Type)
                {
                    case JobType.StokeFurnace:
                        double requestedCoal = effectiveWorkers * GameSettings.Instance.StokerCoalConsumptionPerWorker * deltaTime;
                        totalCoalBurned += logistics.ConsumeUpToCoal(requestedCoal);
                        break;

                    case JobType.CoalMining:
                        double mineMultiplier = (DrillBuffTimer > 0) ? GameSettings.Instance.CoalMiningDrillBuffMultiplier : 1.0;
                        double coalProduced = effectiveWorkers * GameSettings.Instance.CoalMiningBaseRatePerWorker * mineMultiplier * deltaTime;
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
                        double scrapProduced = effectiveWorkers * GameSettings.Instance.ScavengeScrapBaseRatePerWorker * thawMultiplier * deltaTime;
                        logistics.AddRawScrap(scrapProduced);
                        break;

                    case JobType.MetalSmelting:
                        double requiredScrap = effectiveWorkers * GameSettings.Instance.SmeltingRawScrapCostPerWorker * deltaTime;
                        double availableScrap = Math.Min(requiredScrap, logistics.RawScrap);

                        if (availableScrap > 0)
                        {
                            logistics.TryConsumeRawScrap(availableScrap);
                            double ratio = availableScrap / requiredScrap;
                            double metalProduced = (effectiveWorkers * ratio) * GameSettings.Instance.SmeltingRefinedMetalRatePerWorker * deltaTime;
                            logistics.AddRefinedMetal(metalProduced);

                            activeSmelters = (int)(job.AssignedWorkers * ratio); // Εδώ κρατάμε τους πραγματικούς εργάτες για τη θερμοδυναμική
                        }
                        break;

                    case JobType.OperateTurbines:
                        double requiredSteam = effectiveWorkers * GameSettings.Instance.TurbineSteamCostPerWorker * deltaTime;

                        if (boiler.SteamPressure > 0)
                        {
                            double consumedSteam = Math.Min(requiredSteam, boiler.SteamPressure);
                            boiler.DeductSteam(consumedSteam);

                            totalThermalShock += consumedSteam * GameSettings.Instance.TurbineThermalShockPerBar;

                            double ratio = consumedSteam / requiredSteam;
                            double electricityProduced = (effectiveWorkers * ratio) * GameSettings.Instance.TurbinePowerPerWorker * deltaTime;
                            logistics.AddElectricity(electricityProduced);
                        }
                        break;
                }
            }

            double coalBurnedPerSec = totalCoalBurned / deltaTime;
            return (activeSmelters, totalThermalShock, coalBurnedPerSec);
        }

        // =========================================================
        // 2. ΕΚΤΕΛΕΣΗ ΔΙΑΚΡΙΤΩΝ ΕΡΓΑΣΙΩΝ (DISCRETE JOBS / PROGRESS BARS)
        // =========================================================
        public void ProcessDiscreteJobs(double deltaTime, LogisticsSubsystem logistics, BoilerSubsystem boiler, bool isDayShift, InventorySubsystem inventory)
        {
            if (deltaTime <= 0.0) return;

            foreach (var job in Jobs.Values)
            {
                if (job.AssignedWorkers <= 0) continue;

                // Χρήση του Effective Workers για την πρόοδο της δουλειάς
                double workGenerated = job.GetEffectiveWorkers() * deltaTime;

                switch (job.Type)
                {
                    case JobType.CleanSoot:
                        if (boiler.CurrentSoot <= 0) break;

                        job.WorkRemaining -= workGenerated;
                        if (job.WorkRemaining <= 0)
                        {
                            boiler.RemoveSoot(GameSettings.Instance.SootRemovedPerClean);
                            logistics.AddCoal(GameSettings.Instance.CoalYieldFromCleaningSoot);
                            job.SetWorkRemaining(GameSettings.Instance.CleanSootWorkRequired);
                        }
                        break;

                    case JobType.DaytimePatch:
                        if (!isDayShift || !boiler.IsHullCracked) break;

                        if (job.WorkRemaining == GameSettings.Instance.PatchWorkRequired)
                        {
                            // Κατανάλωση Metal Patch από το Inventory (ΟΧΙ από τα Logistics)
                            if (!inventory.TryConsumeItem("Metal Patch")) break;
                        }

                        job.WorkRemaining -= workGenerated;
                        if (job.WorkRemaining <= 0)
                        {
                            boiler.ApplyMetalPatch();
                            job.SetWorkRemaining(GameSettings.Instance.PatchWorkRequired);
                        }
                        break;

                    case JobType.NightHullRepair:
                        if (isDayShift || boiler.CasingIntegrity >= 100.0) break;

                        if (job.WorkRemaining == GameSettings.Instance.NightRepairWorkRequired)
                        {
                            if (!logistics.TryConsumeRefinedMetal(GameSettings.Instance.NightRepairRefinedMetalCost)) break;
                        }

                        job.WorkRemaining -= workGenerated;
                        if (job.WorkRemaining <= 0)
                        {
                            boiler.RepairIntegrity(GameSettings.Instance.NightRepairIntegrityRestored);
                            job.SetWorkRemaining(GameSettings.Instance.NightRepairWorkRequired);
                        }
                        break;

                    case JobType.WorkshopCraftPatch:
                        if (job.WorkRemaining == GameSettings.Instance.CraftMetalPatchWorkRequired)
                        {
                            if (!logistics.TryConsumeRefinedMetal(GameSettings.Instance.CraftMetalPatchRefinedMetalCost)) break;
                        }

                        double patchSpeed = 1.0;
                        if (isDayShift && logistics.TryConsumeElectricity(job.GetEffectiveWorkers() * GameSettings.Instance.WorkshopElectricityConsumptionPerWorker * deltaTime))
                        {
                            patchSpeed = GameSettings.Instance.WorkshopElectricitySpeedMultiplier;
                        }

                        job.WorkRemaining -= (workGenerated * patchSpeed);

                        if (job.WorkRemaining <= 0)
                        {
                            // Προσθήκη Metal Patch στο Inventory
                            inventory.AddItem("Metal Patch", ItemCategory.Consumable);
                            job.SetWorkRemaining(GameSettings.Instance.CraftMetalPatchWorkRequired);
                        }
                        break;

                    case JobType.WorkshopCraftFuse:
                        // Διαβάζουμε πόσα Fuses έχουμε από το Inventory
                        if (inventory.GetConsumableCount("Boiler Fuse") >= GameSettings.Instance.MaxFuses) break;

                        if (job.WorkRemaining == GameSettings.Instance.CraftFuseWorkRequired)
                        {
                            if (!logistics.TryConsumeRefinedMetal(GameSettings.Instance.CraftFuseRefinedMetalCost)) break;
                        }

                        double fuseSpeed = 1.0;
                        if (isDayShift && logistics.TryConsumeElectricity(job.GetEffectiveWorkers() * GameSettings.Instance.WorkshopElectricityConsumptionPerWorker * deltaTime))
                        {
                            fuseSpeed = GameSettings.Instance.WorkshopElectricitySpeedMultiplier;
                        }

                        job.WorkRemaining -= (workGenerated * fuseSpeed);

                        if (job.WorkRemaining <= 0)
                        {
                            // Προσθήκη Fuse στο Inventory
                            inventory.AddItem("Boiler Fuse", ItemCategory.Consumable);
                            job.SetWorkRemaining(GameSettings.Instance.CraftFuseWorkRequired);
                        }
                        break;
                }
            }
        }
    }
}