using Godot;
using System;
using System.Text.Json;

namespace PermafrostChieftain.Simulation
{
    public class GameSettings
    {
        // Το μοτίβο Singleton για εύκολη πρόσβαση από παντού
        public static GameSettings Instance { get; private set; } = new GameSettings();

        // Το μονοπάτι αποθήκευσης (το user:// λειτουργεί και σε PC και σε Android)
        private const string SavePath = "user://admin_settings.json";

        // ==========================================
        // TAB 1: TIMER
        // ==========================================
        public double ActiveCycleDuration { get; set; } = 6480.0; // Ο χρόνος μέχρι να ανοίξει το παράθυρο εκτόνωσης (108 λεπτά)
        public double VentGracePeriod { get; set; } = 20.0;       // Ο χρόνος που το κουμπί είναι διαθέσιμο (πριν σκάσει)
        public double VentModalTimeout { get; set; } = 10.0;      // Time allowed to make a choice in the modal
        public double TwilightDuration { get; set; } = 2880.0;    // 48 minutes
        public double NightDuration { get; set; } = 57600.0;      // 16 hours
        // ==========================================
        // TAB 2: CORE
        // ==========================================
        public double StartingCoreTemp { get; set; } = 550.0;
        public double StartingPressure { get; set; } = 80.0;
        public double MaxPressure { get; set; } = 200.0;
        public double BaseCoalBurnRate { get; set; } = 0.2;
        public double ViolentDischargeCasingDamage { get; set; } = 25.0; // Ζημιά αν δεν πατηθεί το κουμπί
        public double CasingDamageHeatMultiplier { get; set; } = 0.5;    // Ζημιά ανά δευτερόλεπτο αν υπερθερμανθεί
        public double CasingDamagePressureMultiplier { get; set; } = 0.2; // Ζημιά ανά δευτερόλεπτο αν υπερβεί την πίεση
        public double NightBleedRate { get; set; } = 0.05;
        public double HullCrackedLeakRate { get; set; } = 0.3; // Απώλεια πίεσης όταν υπάρχει ρήγμα
        public double SootPressureMultiplier { get; set; } = 0.5;

        // ==========================================
        // TAB 3: RESOURCES
        // ==========================================
        public double StartingCoal { get; set; } = 5000.0;
        public double StartingScrap { get; set; } = 150.0;
        public double StartingElectricity { get; set; } = 0.0;

        // ==========================================
        // TAB 4: POPULATION
        // ==========================================
        public int StartingHealthy { get; set; } = 80;
        public int StartingSick { get; set; } = 0;
        public int StartingGrave { get; set; } = 0;
        public double HazardDeathThreshold { get; set; } = 0.12; // Το όριο κινδύνου πριν αρχίσουν οι θάνατοι
        public double DeathRollProbability { get; set; } = 0.3;  // Πιθανότητα θανάτου (30%) όταν ξεπεραστεί το όριο
        public double IntensiveCareHealChance { get; set; } = 0.6;
        public double NormalHealChance { get; set; } = 0.2;

        // ==========================================
        // TAB 5: JOBS & REWARDS
        // ==========================================
        // Για συντομία στο παράδειγμα, φτιάχνουμε κλάση για τα Jobs
        public class JobConfig
        {
            public double BaseWork { get; set; }
            public int OptimalWorkers { get; set; }
        }

        // Λεξικό που κρατάει το BaseWork και OptimalWorkers για ΚΑΘΕ Job
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

        // Ειδικές μεταβλητές εργασιών
        public double StokeCoalCost { get; set; } = 150.0;
        public double StokeHeatProduced { get; set; } = 40.0;
        public double BracingScrapCost { get; set; } = 5.0;
        public double BracingRepairAmount { get; set; } = 15.0;
        public double DaySootCleanAmount { get; set; } = 0.2;
        public double NightSootCleanAmount { get; set; } = 0.5;
        //public double CraftingElectricityCost { get; set; } = 10.0;
        //public double CraftingScrapCost { get; set; } = 2.0;

        // Δομή Συνταγής για το Μηχανουργείο
        public class CraftingRecipe
        {
            public double ScrapCost { get; set; }
            public double ElectricityCost { get; set; }
            public double BaseWorkTime { get; set; } // Χρόνος σε δευτερόλεπτα
        }

        // Λεξικό με τις διαθέσιμες συνταγές
        public System.Collections.Generic.Dictionary<string, CraftingRecipe> CraftingRecipes { get; set; } = new()
        {
            { "Fuse", new CraftingRecipe { ScrapCost = 2.0, ElectricityCost = 10.0, BaseWorkTime = 1800.0 } },
            { "Hull Patch", new CraftingRecipe { ScrapCost = 8.0, ElectricityCost = 15.0, BaseWorkTime = 3600.0 } },
            { "Medical Tonic", new CraftingRecipe { ScrapCost = 3.0, ElectricityCost = 5.0, BaseWorkTime = 2700.0 } }
        };
        public double DamperScrapYield { get; set; } = 2.0;

        // MicroBleed
        public double MicroBleedPressureDeduct { get; set; } = 15.0;
        public double MicroBleedCityHeatAdd { get; set; } = 15.0;
        public double MicroBleedDrillCoalAdd { get; set; } = 20.0;
        public double MicroBleedWorkshopElecAdd { get; set; } = 10.0;

        // Ανταμοιβές (Rewards)
        public double BaseCoalSootReward { get; set; } = 10.0;
        public double SootMultiplierPerDay { get; set; } = 15.0;
        public double BaseScrapMaintReward { get; set; } = 2.0;
        public double MaintMultiplierPerDay { get; set; } = 3.0;

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
                Instance = new GameSettings(); // Επαναφορά στα defaults
                Instance.Save(); // Δημιουργία του αρχείου αμέσως
                return;
            }

            try
            {
                using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
                string jsonString = file.GetAsText();
                Instance = JsonSerializer.Deserialize<GameSettings>(jsonString) ?? new GameSettings();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Σφάλμα κατά τη φόρτωση των ρυθμίσεων. Χρήση προεπιλεγμένων τιμών. Εξαίρεση: {ex.Message}");
                Instance = new GameSettings();
            }
        }
    }
}