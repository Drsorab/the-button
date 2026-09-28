using Godot;
using System;
using System.Text.Json;

namespace PermafrostChieftain.Simulation
{
    public class GameSettings
    {
        // Το μοτίβο Singleton
        public static GameSettings Instance { get; private set; } = new GameSettings();
        private const string SavePath = "user://admin_settings.json";

        // ==========================================
        // TAB 1: TIMER
        // ==========================================
        public double ActiveCycleDuration { get; set; } = 6480.0;
        public double TwilightDuration { get; set; } = 2880.0;
        public double NightDuration { get; set; } = 57600.0;
        public double VentGracePeriod { get; set; } = 20.0;
        public double VentModalTimeout { get; set; } = 10.0;

        // ==========================================
        // TAB 2: CORE
        // ==========================================
        public double StartingCoreTemp { get; set; } = 550.0;
        public double StartingPressure { get; set; } = 80.0;
        public double OptimalCoreTemp { get; set; } = 550.0;
        public double BoilingPointThreshold { get; set; } = 250.0;
        public double MaxPressure { get; set; } = 200.0;
        public double BasePressureGenerationRate { get; set; } = 0.5;
        public double HeatGainPerKgCoal { get; set; } = 40.0;
        public double AmbientHeatLossRate { get; set; } = 0.002;
        public double NightBleedRate { get; set; } = 0.05;

        // ==========================================
        // TAB 3: HULL (Casing Damage)
        // ==========================================
        public double PressureDamageThreshold { get; set; } = 180.0;
        public double HeatDamageThreshold { get; set; } = 850.0;
        public double CasingDamageHeatMultiplier { get; set; } = 0.5;
        public double CasingDamagePressureMultiplier { get; set; } = 0.2;
        public double ViolentDischargeCasingDamage { get; set; } = 25.0;
        public double HullCrackedLeakRate { get; set; } = 0.3;

        // ==========================================
        // TAB 4: SOOT (Αιθάλη)
        // ==========================================
        public double BaseSootGenerationRate { get; set; } = 0.0001;
        public double SmolderingSootPenalty { get; set; } = 5.0;
        public double SootInsulationMultiplier { get; set; } = 0.8;
        public double BlowoutPressureCost { get; set; } = 150.0;
        public double BlowoutHullDamage { get; set; } = 15.0;
        public double BlowoutSootCleared { get; set; } = 0.40;
        public double DaySootCleanAmount { get; set; } = 0.2;
        public double NightSootCleanAmount { get; set; } = 0.5;

        // ==========================================
        // TAB 5: VENT (Εκτόνωση & Routing)
        // ==========================================
        public double VentElectricityMultiplier { get; set; } = 2.0;
        public double VentCoalMultiplier { get; set; } = 15.0;
        public double VentScrapMultiplier { get; set; } = 2.0;
        public double VentRoutingSootPenalty { get; set; } = 0.05;
        public double MicroBleedPressureDeduct { get; set; } = 15.0;
        public double MicroBleedCityHeatAdd { get; set; } = 15.0;
        public double MicroBleedDrillCoalAdd { get; set; } = 20.0;
        public double MicroBleedWorkshopElecAdd { get; set; } = 10.0;

        // ==========================================
        // TAB 6: RESOURCES
        // ==========================================
        public double StartingCoal { get; set; } = 5000.0;
        public double StartingScrap { get; set; } = 150.0;
        public double StartingElectricity { get; set; } = 0.0;
        public double NightElectricityDrainRate { get; set; } = 1.5;
        public double NightHeatConversionEfficiency { get; set; } = 0.8;

        // ==========================================
        // TAB 7: POPULATION
        // ==========================================
        public int StartingHealthy { get; set; } = 80;
        public int StartingSick { get; set; } = 0;
        public int StartingGrave { get; set; } = 0;
        public double HazardDeathThreshold { get; set; } = 0.12;
        public double DeathRollProbability { get; set; } = 0.3;
        public double IntensiveCareHealChance { get; set; } = 0.6;
        public double NormalHealChance { get; set; } = 0.2;

        // ==========================================
        // TAB 8: JOBS & REWARDS
        // ==========================================
        public class JobConfig
        {
            public double BaseWork { get; set; }
            public int OptimalWorkers { get; set; }
        }

        public System.Collections.Generic.Dictionary<JobType, JobConfig> JobConfigs { get; set; } = new()
        {
            { JobType.StokeFurnace, new JobConfig { BaseWork = 1800.0, OptimalWorkers = 15 } },
            { JobType.MicroBleed, new JobConfig { BaseWork = 900.0, OptimalWorkers = 5 } },
            { JobType.StructuralBracing, new JobConfig { BaseWork = 2700.0, OptimalWorkers = 10 } },
            { JobType.ScourFlues, new JobConfig { BaseWork = 3600.0, OptimalWorkers = 10 } },
            { JobType.DamperThrottling, new JobConfig { BaseWork = 1200.0, OptimalWorkers = 5 } },
            { JobType.DeepScour, new JobConfig { BaseWork = 5400.0, OptimalWorkers = 10 } },
            { JobType.WorkshopMaintenance, new JobConfig { BaseWork = 3600.0, OptimalWorkers = 8 } },
            { JobType.IntensiveCare, new JobConfig { BaseWork = 7200.0, OptimalWorkers = 5 } },
            { JobType.WorkshopMachining, new JobConfig { BaseWork = 1800.0, OptimalWorkers = 10 } },
            { JobType.ManualTinkering, new JobConfig { BaseWork = 4500.0, OptimalWorkers = 5 } }
        };

        public double StokeCoalPerWorkerPerHour { get; set; } = 21.0;
        public double BracingScrapCost { get; set; } = 5.0;
        public double BracingRepairAmount { get; set; } = 15.0;
        public double DamperScrapYield { get; set; } = 2.0;
        public double BaseCoalSootReward { get; set; } = 10.0;
        public double SootMultiplierPerDay { get; set; } = 15.0;
        public double BaseScrapMaintReward { get; set; } = 2.0;
        public double MaintMultiplierPerDay { get; set; } = 3.0;

        // ==========================================
        // TAB 9: EQUIPMENT & INVENTORY
        // ==========================================
        // Shovels (Φτυάρια)
        public double ShovelCoalBonusMultiplier { get; set; } = 1.25; // +25% περισσότερο κάρβουνο ανά εργάτη
        public double ShovelHeatDegradationMultiplier { get; set; } = 0.05; // Ζημιά ανά βαθμό Κελσίου πάνω από το Optimal
        public double ShovelBaseDegradationPerSec { get; set; } = 0.1; // Βασική φθορά όσο χρησιμοποιούνται

        // Batteries (Μπαταρίες)
        public double BatteryCapacityBonus { get; set; } = 50.0; // Πόσο αυξάνει το μέγιστο Relay storage
        public double BatteryDegradationPerCycle { get; set; } = 2.0; // Φθορά ανά πλήρη κύκλο φόρτισης/αποφόρτισης

        // Respirators (Αναπνευστήρες)
        public double RespiratorHazardReduction { get; set; } = 0.8; // Μειώνει το Hazard κατά 80% (πολλαπλασιαστής 0.2)
        public double RespiratorClogRatePerSec { get; set; } = 0.15; // Πόσο γρήγορα βουλώνουν
        public double RespiratorDangerThreshold { get; set; } = 20.0; // Αν η αντοχή πέσει κάτω από 20%, αυξάνουν το Hazard!
        public double RespiratorBrokenHazardPenalty { get; set; } = 1.5; // Πόσο χειρότερο κάνουν το Hazard όταν βουλώσουν

        // Wire Brushes (Βούρτσες)
        public double BrushWorkReductionMultiplier { get; set; } = 0.5; // Μειώνει τον χρόνο καθαρισμού στο μισό
        public double BrushYieldMultiplier { get; set; } = 2.0; // Διπλασιάζει τα υλικά που βρίσκεις
        public double BrushDegradationPerSec { get; set; } = 1.0; // Λιώνουν πολύ γρήγορα (Μεγάλη φθορά)

        // Συντήρηση (Workshop Maintenance)
        public double NightShiftRepairRate { get; set; } = 5.0; // Πόση αντοχή ανακτούν τα αντικείμενα κατά τη συντήρηση

        // ==========================================
        // RECIPES
        // ==========================================
        public class CraftingRecipe
        {
            public double ScrapCost { get; set; }
            public double ElectricityCost { get; set; }
            public double BaseWorkTime { get; set; }
        }

        public System.Collections.Generic.Dictionary<string, CraftingRecipe> CraftingRecipes { get; set; } = new()
        {
            { "Reinforced Shovels", new CraftingRecipe { ScrapCost = 15.0, ElectricityCost = 0.0, BaseWorkTime = 3600.0 } },
            { "Capacitor Batteries", new CraftingRecipe { ScrapCost = 10.0, ElectricityCost = 25.0, BaseWorkTime = 5400.0 } },
            { "Pressure Fuses", new CraftingRecipe { ScrapCost = 5.0, ElectricityCost = 10.0, BaseWorkTime = 1800.0 } },
            { "Pneumatic Respirators", new CraftingRecipe { ScrapCost = 20.0, ElectricityCost = 5.0, BaseWorkTime = 4500.0 } },
            { "Wire Brushes", new CraftingRecipe { ScrapCost = 8.0, ElectricityCost = 0.0, BaseWorkTime = 2700.0 } }
        };
        // ==========================================
        // ΜΕΘΟΔΟΙ ΑΠΟΘΗΚΕΥΣΗΣ / ΦΟΡΤΩΣΗΣ
        // ==========================================
        public void Save()
        {
            try
            {
                string jsonString = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
                file.StoreString(jsonString);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Σφάλμα κατά την αποθήκευση των ρυθμίσεων: {ex.Message}");
            }
        }

        public static void Load()
        {
            if (!FileAccess.FileExists(SavePath))
            {
                GD.Print("Δεν βρέθηκε αρχείο ρυθμίσεων. Χρήση προεπιλεγμένων τιμών.");
                Instance = new GameSettings();
                Instance.Save();
                return;
            }

            try
            {
                using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
                string jsonString = file.GetAsText();
                Instance = JsonSerializer.Deserialize<GameSettings>(jsonString) ?? new GameSettings();

                // --- ΝΕΟΣ ΕΛΕΓΧΟΣ: Επαναφορά των Συνταγών αν το JSON είναι παλιό ή άδειο ---
                if (Instance.CraftingRecipes == null || Instance.CraftingRecipes.Count == 0 || !Instance.CraftingRecipes.ContainsKey("Reinforced Shovels"))
                {
                    Instance.CraftingRecipes = new System.Collections.Generic.Dictionary<string, CraftingRecipe>
                    {
                        { "Reinforced Shovels", new CraftingRecipe { ScrapCost = 15.0, ElectricityCost = 0.0, BaseWorkTime = 3600.0 } },
                        { "Capacitor Batteries", new CraftingRecipe { ScrapCost = 10.0, ElectricityCost = 25.0, BaseWorkTime = 5400.0 } },
                        { "Pressure Fuses", new CraftingRecipe { ScrapCost = 5.0, ElectricityCost = 10.0, BaseWorkTime = 1800.0 } },
                        { "Pneumatic Respirators", new CraftingRecipe { ScrapCost = 20.0, ElectricityCost = 5.0, BaseWorkTime = 4500.0 } },
                        { "Wire Brushes", new CraftingRecipe { ScrapCost = 8.0, ElectricityCost = 0.0, BaseWorkTime = 2700.0 } }
                    };

                    // Αποθήκευση ώστε το αρχείο JSON στο δίσκο να ενημερωθεί με τις νέες συνταγές
                    Instance.Save();
                    GD.Print("Το αρχείο ρυθμίσεων ενημερώθηκε επιτυχώς με τις νέες συνταγές κατασκευής.");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Σφάλμα κατά τη φόρτωση των ρυθμίσεων. Χρήση προεπιλεγμένων τιμών. Εξαίρεση: {ex.Message}");
                Instance = new GameSettings();
            }
        }
    }
}