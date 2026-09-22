using System;
using System.Collections.Generic;

namespace PermafrostChieftain.Simulation
{
    public enum JobType
    {
        StokeFurnace,
        MicroBleed,
        StructuralBracing,
        ScourFlues,
        DamperThrottling,
        DeepScour,
        WorkshopMaintenance,
        IntensiveCare,
        WorkshopMachining, 
        ManualTinkering    
    }

    public enum MicroBleedTarget
    {
        Atmosphere,      // Ασφαλής εκτόνωση
        CityHeat,        // Προσθήκη μικρής θερμότητας
        SubterraneanDrill, // Μικρή παραγωγή άνθρακα
        WorkshopPower      // Προσθήκη ρεύματος (Relay) στο μηχανουργείο
    }

    public class JobOrder
    {
        // Συνολικός όγκος εργασίας (σε δευτερόλεπτα, απαιτούμενος για 1 εργάτη)
        public double BaseWorkRequired { get; private set; }

        // Εργασία που απομένει μέχρι την ολοκλήρωση
        public double WorkRemaining { get; private set; }

        // Εργάτες που έχουν ανατεθεί στη συγκεκριμένη εργασία
        public int AssignedWorkers { get; set; } = 0;

        // Ιδανικός αριθμός εργατών (χρησιμοποιείται για τον υπολογισμό κινδύνου αργότερα)
        public int OptimalWorkers { get; private set; }

        public JobType Type { get; private set; }

        public JobOrder(JobType type, double baseWorkRequired, int optimalWorkers)
        {
            Type = type;
            BaseWorkRequired = baseWorkRequired;
            WorkRemaining = baseWorkRequired;
            OptimalWorkers = optimalWorkers;
        }

        // Εκτελεί την πρόοδο της εργασίας για το τρέχον καρέ. Επιστρέφει true αν ολοκληρώθηκε.
        public bool ProcessTick(double deltaTime)
        {
            if (AssignedWorkers <= 0 || WorkRemaining <= 0) return false;

            // Εκθετική απόδοση: Οι 10 εργάτες δεν είναι ακριβώς 10 φορές πιο γρήγοροι από 1.
            double effectiveWorkRate = Math.Pow(AssignedWorkers, 0.8);

            WorkRemaining -= effectiveWorkRate * deltaTime;

            if (WorkRemaining <= 0)
            {
                WorkRemaining = 0;
                return true; // Η εργασία μόλις ολοκληρώθηκε
            }

            return false;
        }

        // Επαναφορά της εργασίας για να μπορεί να εκτελεστεί ξανά
        public void ResetJob()
        {
            WorkRemaining = BaseWorkRequired;
            // Σημείωση: Δεν μηδενίζουμε τους εργάτες. Συνεχίζουν την επόμενη "παρτίδα" αυτόματα.
        }

        // Αλλάζει τον συνολικό χρόνο εργασίας, διατηρώντας το ποσοστό προόδου
        public void SetBaseWork(double newBaseWork)
        {
            double progressRatio = BaseWorkRequired > 0 ? (BaseWorkRequired - WorkRemaining) / BaseWorkRequired : 0.0;
            BaseWorkRequired = newBaseWork;
            WorkRemaining = BaseWorkRequired * (1.0 - progressRatio);
        }
        // Επιστρέφει το ποσοστό ολοκλήρωσης (0.0 έως 1.0)
        public double GetProgressRatio()
        {
            if (BaseWorkRequired <= 0) return 0.0;
            return (BaseWorkRequired - WorkRemaining) / BaseWorkRequired;
        }

        // Ορίζει το ποσοστό ολοκλήρωσης (χρήσιμο για συγχρονισμό βαρδιών)
        public void SetProgressRatio(double ratio)
        {
            WorkRemaining = BaseWorkRequired * (1.0 - Math.Clamp(ratio, 0.0, 1.0));
        }
    }

    public class JobManager
    {
        public Dictionary<JobType, JobOrder> Jobs { get; private set; } = new();

        // Μετρητές ημερών για τις κλιμακούμενες ανταμοιβές (Time-Scaled Rewards)
        public int DaysSinceLastFlueClean { get; private set; } = 0;
        public int DaysSinceLastWorkshopMaint { get; private set; } = 0;

        // Γεγονός που ειδοποιεί το GameOrchestrator ότι μια συγκεκριμένη εργασία τελείωσε
        public event Action<JobType> OnJobCompleted;
        public MicroBleedTarget ActiveMicroBleedTarget { get; set; } = MicroBleedTarget.Atmosphere;

        public string ActiveCraftingRecipe { get; private set; } = "Fuse";

        public JobManager()
        {
            foreach (var kvp in GameSettings.Instance.JobConfigs)
            {
                Jobs.Add(kvp.Key, new JobOrder(kvp.Key, kvp.Value.BaseWork, kvp.Value.OptimalWorkers));
            }
            SetActiveCraftingRecipe("Fuse");
            //// Αρχικοποίηση των εργασιών με τους βασικούς χρόνους (σε δευτερόλεπτα) και το βέλτιστο προσωπικό
            //// Π.χ. 1800.0 = 30 λεπτά αν δουλεύει 1 άτομο. Αν δουλεύουν 15 άτομα, γίνεται σε ~3.5 λεπτά.
            //Jobs.Add(JobType.StokeFurnace, new JobOrder(JobType.StokeFurnace, 1800.0, 15));
            //Jobs.Add(JobType.MicroBleed, new JobOrder(JobType.MicroBleed, 900.0, 5));
            //Jobs.Add(JobType.StructuralBracing, new JobOrder(JobType.StructuralBracing, 2700.0, 10));
            //Jobs.Add(JobType.ScourFlues, new JobOrder(JobType.ScourFlues, 3600.0, 10));

            //Jobs.Add(JobType.DamperThrottling, new JobOrder(JobType.DamperThrottling, 1200.0, 5));
            //Jobs.Add(JobType.DeepScour, new JobOrder(JobType.DeepScour, 5400.0, 10));
            //Jobs.Add(JobType.WorkshopMaintenance, new JobOrder(JobType.WorkshopMaintenance, 3600.0, 8));
            //Jobs.Add(JobType.IntensiveCare, new JobOrder(JobType.IntensiveCare, 7200.0, 5));

            //// Το ημερήσιο Machining είναι πολύ γρήγορο (30 λεπτά βάση)
            //Jobs.Add(JobType.WorkshopMachining, new JobOrder(JobType.WorkshopMachining, 1800.0, 10));

            //// Το νυχτερινό Tinkering παίρνει υπερδιπλάσιο χρόνο (75 λεπτά βάση) αφού δεν έχει ρεύμα
            //Jobs.Add(JobType.ManualTinkering, new JobOrder(JobType.ManualTinkering, 4500.0, 5));
        }

        public void Tick(double deltaTime)
        {
            foreach (var kvp in Jobs)
            {
                JobOrder currentJob = kvp.Value;
                if (currentJob.ProcessTick(deltaTime))
                {
                    if (currentJob.Type == JobType.ScourFlues || currentJob.Type == JobType.DeepScour)
                    {
                        DaysSinceLastFlueClean = 0;
                    }
                    else if (currentJob.Type == JobType.WorkshopMaintenance)
                    {
                        DaysSinceLastWorkshopMaint = 0;
                    }

                    OnJobCompleted?.Invoke(currentJob.Type);

                    // Αν τελείωσε κατασκευή (μέρα ή νύχτα), μηδενίζουμε και τις δύο βάρδιες!
                    if (currentJob.Type == JobType.WorkshopMachining || currentJob.Type == JobType.ManualTinkering)
                    {
                        Jobs[JobType.WorkshopMachining].ResetJob();
                        Jobs[JobType.ManualTinkering].ResetJob();
                    }
                    else
                    {
                        currentJob.ResetJob();
                    }
                }
            }

            // Συγχρονισμός της προόδου κατασκευής μεταξύ Ημέρας και Νύχτας
            var dayJob = Jobs[JobType.WorkshopMachining];
            var nightJob = Jobs[JobType.ManualTinkering];

            if (dayJob.AssignedWorkers > 0)
            {
                // Αν δουλεύουν τη μέρα, η νυχτερινή βάρδια "αντιγράφει" το ποσοστό προόδου τους
                nightJob.SetProgressRatio(dayJob.GetProgressRatio());
            }
            else if (nightJob.AssignedWorkers > 0)
            {
                // Αν δουλεύουν τη νύχτα, η ημερήσια βάρδια ενημερώνεται αντίστοιχα για το επόμενο πρωί
                dayJob.SetProgressRatio(nightJob.GetProgressRatio());
            }
        }

        // Καλείται από το κεντρικό ρολόι όταν ξημερώνει (Morning Whistle)
        public void IncrementDailyCounters()
        {
            DaysSinceLastFlueClean++;
            DaysSinceLastWorkshopMaint++;
        }

        // Υπολογίζει πόσο κάρβουνο (Coal Dust) παίρνει ο παίκτης όταν καθαρίζει τις καμινάδες
        public double CalculateSootCleaningReward()
        {
            // Υπολογισμός άνθρακα με βάση τις ρυθμίσεις
            return GameSettings.Instance.BaseCoalSootReward + (DaysSinceLastFlueClean * GameSettings.Instance.SootMultiplierPerDay);
        }

        public double CalculateWorkshopMaintenanceReward()
        {
            // Υπολογισμός μετάλλου (scrap) με βάση τις ρυθμίσεις
            return GameSettings.Instance.BaseScrapMaintReward + (DaysSinceLastWorkshopMaint * GameSettings.Instance.MaintMultiplierPerDay);
        }

        // Καλείται από το UI όταν ο παίκτης αλλάζει συνταγή από το Dropdown
        public void SetActiveCraftingRecipe(string recipeName)
        {
            if (GameSettings.Instance.CraftingRecipes.TryGetValue(recipeName, out var recipe))
            {
                ActiveCraftingRecipe = recipeName;

                // Ενημέρωση του χρόνου για την Ημερήσια κατασκευή
                if (Jobs.TryGetValue(JobType.WorkshopMachining, out var dayJob))
                {
                    dayJob.SetBaseWork(recipe.BaseWorkTime);
                }

                // Ενημέρωση του χρόνου για τη Νυχτερινή κατασκευή (π.χ. παίρνει διπλάσιο χρόνο χωρίς ρεύμα)
                if (Jobs.TryGetValue(JobType.ManualTinkering, out var nightJob))
                {
                    nightJob.SetBaseWork(recipe.BaseWorkTime * 2.0);
                }
            }
        }
    }
}