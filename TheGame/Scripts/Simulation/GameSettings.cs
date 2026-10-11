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
        // Reasoning: Timers remain unchanged as they dictate the fundamental game loop structure.

        // ==========================================
        // TAB 2: CORE
        // ==========================================
        public double StartingCoreTemp { get; set; } = 150.0;
        public double StartingPressure { get; set; } = 0.0;
        public double OptimalCoreTemp { get; set; } = 750.0;
        public double BoilingPointThreshold { get; set; } = 250.0;
        public double MaxPressure { get; set; } = 200.0;
        public double BasePressureGenerationRate { get; set; } = 0.015;
        public double HeatGainPerKgCoal { get; set; } = 85.0;
        public double AmbientHeatLossRate { get; set; } = 0.0025;
        public double NightBleedRate { get; set; } = 0.003;

        // Πόση ενέργεια του άνθρακα χάνεται για να ζεσταθούν τα κρύα μέταλλα (60% απώλεια όταν είναι κρύο)
        public double ColdFurnaceEfficiencyPenalty { get; set; } = 0.6;

        // Πόσο "βαρύ" είναι το σύστημα. Μικρότερο νούμερο = πιο αργές/ομαλές αλλαγές θερμοκρασίας
        public double CoreThermalInertia { get; set; } = 0.3;

        // Reasoning: 
        // Starting Values: Following the tutorial blowout, the generator is cold (200°C) and depressurized (35 Bar). 
        // OptimalCoreTemp (750°C): Raised from 550°C to require 12-15 workers assigned to StokeFurnace to maintain it against the AmbientHeatLossRate.
        // BasePressureGenerationRate (0.012): The simulation showed that with a 40-worker economy, players can only spare about 5 workers for DayScour. At 0.012, if soot hits 50%, pressure reaches 160 Bar at the 108-minute mark, requiring active Vent Routing but avoiding a forced blowout.

        // ==========================================
        // TAB 3: HULL (Casing Damage)
        // ==========================================
        public double StartingHullIntegrity { get; set; } = 70.0; // Assume you add this variable
        public double PressureDamageThreshold { get; set; } = 180.0;
        public double HeatDamageThreshold { get; set; } = 950.0;
        public double CasingDamageHeatMultiplier { get; set; } = 0.05;
        public double CasingDamagePressureMultiplier { get; set; } = 0.08;
        // Πόση ζημιά (Integrity) παθαίνει το κέλυφος όταν δημιουργηθεί ρήγμα
        public double CatastrophicHullDamage { get; set; } = 25.0;

        // Πολλαπλασιαστής για το πόσο γρήγορα κλιμακώνεται η διαρροή πίεσης ανά δευτερόλεπτο
        public double HullCrackPressureLeakMultiplier { get; set; } = 1.5;

        // Πόση αντοχή ΠΡΕΠΕΙ να επισκευαστεί (μετά το ρήγμα) για να θεωρηθεί "σφραγισμένο"
        // Το 15.0 αντιστοιχεί ακριβώς σε μία ολοκληρωμένη εργασία "Structural Bracing"
        public double IntegrityRepairRequiredToSeal { get; set; } = 15.0;


        // ==========================================
        // TAB 4: SOOT (Αιθάλη)
        // ==========================================
        public double BaseSootGenerationRate { get; set; } = 0.00012;
        public double SmolderingSootPenalty { get; set; } = 5.0;
        public double SootInsulationMultiplier { get; set; } = 0.6;
        public double MaxSootPressureMultiplier { get; set; } = 2.2;
        public double BlowoutPressureCost { get; set; } = 150.0;
        public double BlowoutHullDamage { get; set; } = 15.0;
        public double BlowoutSootCleared { get; set; } = 0.50;
        public double DaySootCleanAmount { get; set; } = 0.15;
        public double NightSootCleanAmount { get; set; } = 0.35;
        // Reasoning: 
        // SootInsulationMultiplier (0.7): If soot reaches 100%, coal yields 30% less heat. With only 40 workers, the player cannot afford to put 20 on StokeFurnace to compensate. They must clean the flues.
        // Clean Amounts (0.15 / 0.35): Reduced from 0.2/0.5. The simulation indicated that at 0.5, NightScour completely negated the soot mechanic. Lowering it forces players to dedicate workers to this job over multiple shifts.

        // ==========================================
        // TAB 5: VENT (Εκτόνωση & Routing)
        // ==========================================
        public double VentElectricityMultiplier { get; set; } = 1.5;
        public double VentCoalMultiplier { get; set; } = 10.0;
        public double VentScrapMultiplier { get; set; } = 1.5;
        public double VentRoutingSootPenalty { get; set; } = 0.08;
        public double MicroBleedPressureDeduct { get; set; } = 15.0;
        public double MicroBleedCityHeatAdd { get; set; } = 15.0;
        public double MicroBleedDrillCoalAdd { get; set; } = 15.0;
        public double MicroBleedWorkshopElecAdd { get; set; } = 10.0;

        // Στο TAB 5: VENT (Εκτόνωση & Routing)
        // Αντί για σταθερό νούμερο, τώρα η πίεση πολλαπλασιάζεται με αυτό τον συντελεστή
        public double PressureToHeatConversionRate { get; set; } = 0.8; // π.χ. 15 Bar ατμού * 0.8 = 12°C ζέστης στο Buffer

        // Στο TAB 2: ΘΕΡΜΟΔΥΝΑΜΙΚΗ ΤΗΣ ΠΟΛΗΣ
        // Καθορίζει πόσο γρήγορα αποδεσμεύεται η ζέστη από το Buffer στην πόλη (Ομαλή μετάβαση)
        public double BufferTransferRate { get; set; } = 0.25; // 0.25°C ανά δευτερόλεπτο

        // Reasoning: Reduced the yields slightly. The simulation showed that with 40 workers, Vent Routing provided too much scrap, entirely bypassing the need for `WorkshopMaintenance`.

        // ==========================================
        // TAB 6: RESOURCES
        // ==========================================
        public double StartingCoal { get; set; } = 800.0;
        public double StartingScrap { get; set; } = 0.0;
        public double StartingElectricity { get; set; } = 0.0;
        public double NightElectricityDrainRate { get; set; } = 0.02;
        public double NightHeatConversionEfficiency { get; set; } = 0.8;
        // Reasoning: 
        // StartingCoal (800.0): The player pays 150 for Ignition, leaving 650. 10 Stokers burn ~180/hr. 650 supports roughly 3.5 in-game hours of operation, forcing the player to prioritize coal acquisition (`ScourFlues` yield or Venting) by mid-shift.

        // ==========================================
        // TAB 7: POPULATION & SURVIVAL
        // ==========================================
        // Τα υπάρχοντα που ήδη έχεις:
        public int StartingHealthy { get; set; } = 40;
        public int StartingSick { get; set; } = 0;
        public int StartingGrave { get; set; } = 0;
        public double HazardDeathThreshold { get; set; } = 0.15;
        public double DeathRollProbability { get; set; } = 0.4;
        public double IntensiveCareHealChance { get; set; } = 0.75;
        public double NormalHealChance { get; set; } = 0.15;

        // --- ΝΕΟ: Thresholds Πόλης (City Temperature Hazards) ---
        public double CityChillyThreshold { get; set; } = 10.0;     // Κάτω από 10°C, η πόλη είναι ψυχρή
        public double CityFreezingThreshold { get; set; } = 0.0;    // Κάτω από 0°C, η πόλη παγώνει
        public double CityDeadlyThreshold { get; set; } = -10.0;    // Κάτω από -10°C, ακραίος κίνδυνος

        // --- ΝΕΟ: Πιθανότητες Αρρώστιας λόγω Κρύου (City Cold Penalties) ---
        public double ChillyHazardPenalty { get; set; } = 0.05;     // 5% επιπλέον κίνδυνος ανά εργάτη
        public double FreezingHazardPenalty { get; set; } = 0.15;   // 15% επιπλέον κίνδυνος
        public double DeadlyHazardPenalty { get; set; } = 0.30;     // 30% επιπλέον κίνδυνος

        // (Αν θέλεις να κρατήσεις και την υπερθέρμανση που βάλαμε πριν)
        public double CityOverheatThreshold { get; set; } = 25.0;
        public double OverheatHazardPenalty { get; set; } = 0.15;
        // Reasoning: 
        // 40 Workers: The core constraint. 
        // HazardDeathThreshold (0.15): Raised slightly to give a margin of error. If `WorkshopMachining` increases hazard by 0.1, a second unmitigated machining job pushes it over the edge, causing deaths.


        // Φυσικοί Συντελεστές (Physics Coefficients)
        public double ThermalPressureCoefficient { get; set; } = 000035; // Πόση πίεση παράγεται ανά βαθμό Κελσίου πάνω από το σημείο βρασμού
        public double CondensationRateCoefficient { get; set; } = 0.01; // Πόσο γρήγορα πέφτει η πίεση όταν υγροποιείται ο ατμός κάτω από το σημείο βρασμού
        public double NaturalHullBleedRate { get; set; } = 0.002; // Σταθερή διαρροή βαλβίδων ανεξαρτήτως θερμοκρασίας



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
            { JobType.ScourFlues, new JobConfig { BaseWork = 4500.0, OptimalWorkers = 10 } },
            { JobType.DamperThrottling, new JobConfig { BaseWork = 1200.0, OptimalWorkers = 5 } },
            { JobType.DeepScour, new JobConfig { BaseWork = 6000.0, OptimalWorkers = 10 } },
            { JobType.WorkshopMaintenance, new JobConfig { BaseWork = 4500.0, OptimalWorkers = 8 } },
            { JobType.IntensiveCare, new JobConfig { BaseWork = 7200.0, OptimalWorkers = 5 } },
            { JobType.WorkshopMachining, new JobConfig { BaseWork = 2400.0, OptimalWorkers = 8 } },
            { JobType.ManualTinkering, new JobConfig { BaseWork = 5400.0, OptimalWorkers = 5 } }
        };

        public double StokeCoalPerWorkerPerHour { get; set; } = 18.0;
        public double BracingScrapCost { get; set; } = 10.0;
        public double BracingRepairAmount { get; set; } = 20.0;
        public double DamperScrapYield { get; set; } = 3.0;
        public double BaseCoalSootReward { get; set; } = 25.0;
        public double SootMultiplierPerDay { get; set; } = 12.0;
        public double BaseScrapMaintReward { get; set; } = 4.0;
        public double MaintMultiplierPerDay { get; set; } = 2.0;

        // Reasoning: 
        // BaseWork Times: Increased `ScourFlues` (4500), `DeepScour` (6000), and `WorkshopMachining` (2400). In the simulations, lower work times allowed players to complete 4-5 jobs per shift, resulting in zero tension. These higher values force hard choices: you cannot brace the hull, clean the flues, and craft an item in a single shift with only 40 workers.
        // StokeCoalPerWorkerPerHour (18.0): Adjusted down from 21.0 to prevent instant coal starvation with 15 stokers.
        // BaseScrapMaintReward (4.0): A single maintenance run provides 4 scrap. Bracing costs 10. The player must run maintenance 3 times to afford a hull repair.

        // ==========================================
        // TAB 9: EQUIPMENT & INVENTORY
        // ==========================================
        public double ShovelCoalBonusMultiplier { get; set; } = 1.30;
        public double ShovelHeatDegradationMultiplier { get; set; } = 0.08;
        public double ShovelBaseDegradationPerSec { get; set; } = 0.15;
        public double BatteryCapacityBonus { get; set; } = 50.0;
        public double BatteryDegradationPerCycle { get; set; } = 5.0;
        public double RespiratorHazardReduction { get; set; } = 0.6;
        public double RespiratorClogRatePerSec { get; set; } = 0.20;
        public double RespiratorDangerThreshold { get; set; } = 20.0;
        public double RespiratorBrokenHazardPenalty { get; set; } = 2.0;
        public double BrushWorkReductionMultiplier { get; set; } = 0.6;
        public double BrushYieldMultiplier { get; set; } = 1.5;
        public double BrushDegradationPerSec { get; set; } = 1.5;
        public double NightShiftRepairRate { get; set; } = 10.0;
        // Reasoning: 
        // RespiratorHazardReduction (0.6): Weakened from 0.8. A respirator no longer makes machining perfectly safe; it merely buys time.
        // Degradation Rates: Increased across the board (Shovel: 0.15, Brush: 1.5). The simulation indicated that if items lasted 3-4 shifts, crafting became irrelevant. Items must break quickly to force the player back into the scrap economy.

        // ==========================================
        // ΓΕΝΝΗΤΡΙΑ & ΑΝΑΦΛΕΞΗ (IGNITION)
        // ==========================================
        public double IgnitionCoalCost { get; set; } = 150.0;
        public double ShutdownTempThreshold { get; set; } = 100.0;
        public double InactiveCoolingRate { get; set; } = 0.25;


        // ==========================================
        // ΘΕΡΜΟΔΥΝΑΜΙΚΗ & ΠΕΡΙΒΑΛΛΟΝ
        // ==========================================
        public double AmbientTemperature { get; set; } = -30.0;
        // Reasoning: -30°C provides a survivable baseline for Day 1, giving the 40-worker crew a fighting chance before the escalations drop it to -40°C or below.

        // ==========================================
        // ΘΕΡΜΟΔΥΝΑΜΙΚΗ ΤΗΣ ΠΟΛΗΣ (CITY HEATING PHYSICS)
        // ==========================================
        public double StartingCityTemp { get; set; } = -10.0; // Η πόλη ξεκινάει με λίγη ζέστη από την προηγούμενη βάρδια
        public double CityHeatingCoefficient { get; set; } = 0.0001;
        public double CityCoolingCoefficient { get; set; } = 0.0016;
        public double MinimumHeatingThreshold { get; set; } = 250.0;
        public double CityFreezeThreshold { get; set; } = -20.0; // Game Over




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
            { "Reinforced Shovels", new CraftingRecipe { ScrapCost = 12.0, ElectricityCost = 0.0, BaseWorkTime = 4200.0 } },
            { "Capacitor Batteries", new CraftingRecipe { ScrapCost = 15.0, ElectricityCost = 30.0, BaseWorkTime = 6000.0 } },
            { "Pressure Fuses", new CraftingRecipe { ScrapCost = 8.0, ElectricityCost = 15.0, BaseWorkTime = 2400.0 } },
            { "Pneumatic Respirators", new CraftingRecipe { ScrapCost = 18.0, ElectricityCost = 10.0, BaseWorkTime = 5400.0 } },
            { "Wire Brushes", new CraftingRecipe { ScrapCost = 10.0, ElectricityCost = 0.0, BaseWorkTime = 3000.0 } }
        };
        // Reasoning: 
        // Scrap Costs: Rebalanced against the new `BaseScrapMaintReward` (4.0). A Shovel (12 Scrap) requires 3 successful `WorkshopMaintenance` runs. This creates a multi-shift planning requirement.
        // BaseWorkTimes: Increased significantly. `Capacitor Batteries` (6000s) now take nearly a full 108-minute shift for an 8-worker crew. The player must commit heavily to the workshop to acquire advanced gear.
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