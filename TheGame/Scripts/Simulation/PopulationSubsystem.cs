using Godot;
using System;
using System.Linq;

namespace PermafrostChieftain.Simulation
{
    // Τα 5 στάδια διαβίωσης της πόλης
    public enum CityLivingStage
    {
        Freezing,
        Cold,
        Livable,
        Warm,
        Comfortable
    }

    public class PopulationSubsystem
    {
        public int TotalPopulation => HealthyWorkers + SickWorkers + GraveWorkers;
        public int HealthyWorkers { get; private set; }
        public int SickWorkers { get; private set; }
        public int GraveWorkers { get; private set; }
        public int DeadWorkers { get; private set; } = 0;

        public int UnassignedReserve => HealthyWorkers - GetTotalAssignedWorkers();
        public CityLivingStage CurrentCityStage { get; private set; } = CityLivingStage.Comfortable;

        private readonly JobManager _jobManager;
        private double _injuryCheckTimer = 0.0;
        private const double InjuryCheckInterval = 60.0; // Ο έλεγχος γίνεται κάθε 1 in-game λεπτό

        public event Action<string> OnCasualtyOccurred;

        public PopulationSubsystem(JobManager jobManager)
        {
            _jobManager = jobManager;
        }

        public void Initialize()
        {
            // Ανάκτηση από το TAB 7 του GameSettings (που φτιάξαμε στην αρχή)
            HealthyWorkers = GameSettings.Instance.StartingHealthy;
            SickWorkers = GameSettings.Instance.StartingSick;
            GraveWorkers = GameSettings.Instance.StartingGrave;
        }

        private string GetJobDisplayName(JobType type)
        {
            return type switch
            {
                JobType.StokeFurnace => "Στον Λέβητα (Stokers)",
                JobType.CleanSoot => "Καθαρισμός Καπνιάς",
                JobType.DaytimePatch => "Επικίνδυνο Μπάλωμα",
                JobType.NightHullRepair => "Νυχτερινή Επισκευή",
                JobType.WorkshopCraftPatch => "Μηχανουργείο (Crafting)",
                JobType.WorkshopCraftFuse => "Μηχανουργείο (Fuses)",
                JobType.WorkshopUpkeep => "Συντήρηση Μηχανουργείου",
                JobType.CoalMining => "Ορυχείο Άνθρακα",
                JobType.ScrapScavenging => "Ανασκαφή Πάγων",
                JobType.MetalSmelting => "Χυτήριο",
                JobType.OperateTurbines => "Τουρμπίνες",
                JobType.IntensiveCare => "Εντατική Θεραπεία",
                _ => type.ToString()
            };
        }

        public int GetTotalAssignedWorkers()
        {
            return _jobManager.Jobs.Values.Sum(job => job.AssignedWorkers);
        }

        public bool TryAdjustWorkers(JobType type, int delta)
        {
            if (!_jobManager.Jobs.TryGetValue(type, out JobOrder job)) return false;

            if (delta > 0 && UnassignedReserve < delta) return false;
            if (delta < 0 && job.AssignedWorkers + delta < 0) return false;

            job.AssignedWorkers += delta;
            return true;
        }

        public void ProcessTick(double deltaTime, ThermodynamicsSubsystem thermo)
        {
            if (deltaTime <= 0.0) return;

            // 1. Ενημέρωση του Σταδίου Διαβίωσης βάσει της θερμοκρασίας
            UpdateCityLivingStage(thermo.CityTemperature);

            // 2. Έλεγχος Ασθενειών/Ατυχημάτων
            _injuryCheckTimer += deltaTime;
            if (_injuryCheckTimer >= InjuryCheckInterval)
            {
                EvaluateWorkplaceHazards(thermo);
                _injuryCheckTimer = 0.0;
            }
        }

        // =========================================================
        // ΥΠΟΛΟΓΙΣΜΟΣ ΤΩΝ 5 ΣΤΑΔΙΩΝ (CITY LIVING STAGES)
        // =========================================================
        private void UpdateCityLivingStage(double cityTemp)
        {
            if (cityTemp >= GameSettings.Instance.TempStage_Comfortable)
                CurrentCityStage = CityLivingStage.Comfortable;
            else if (cityTemp >= GameSettings.Instance.TempStage_Warm)
                CurrentCityStage = CityLivingStage.Warm;
            else if (cityTemp >= GameSettings.Instance.TempStage_Livable)
                CurrentCityStage = CityLivingStage.Livable;
            else if (cityTemp >= GameSettings.Instance.TempStage_Cold)
                CurrentCityStage = CityLivingStage.Cold;
            else
                CurrentCityStage = CityLivingStage.Freezing;
        }

        // ΝΕΑ ΜΕΘΟΔΟΣ: Ελέγχει ποιες δουλειές είναι πάνω/δίπλα στον καυτό λέβητα
        // Ξεκαθαρίζουμε 100% ποιες δουλειές είναι στον καυτό πυρήνα και ποιες στο κρύο της πόλης
        private bool IsWorkingNearGenerator(JobType type)
        {
            switch (type)
            {
                case JobType.StokeFurnace:
                case JobType.CleanSoot:
                case JobType.DaytimePatch:
                case JobType.NightHullRepair:
                case JobType.OperateTurbines:
                case JobType.MetalSmelting:
                    return true; // Κοντά στη Γεννήτρια (Ζέστη / Μηχανικός Κίνδυνος)

                case JobType.CoalMining:
                case JobType.ScrapScavenging:
                case JobType.WorkshopCraftFuse:
                case JobType.WorkshopCraftPatch:
                case JobType.WorkshopUpkeep:
                    return false; // Εκτεθειμένοι στην Πόλη / Καιρό (Κρύο / Ασθένειες)

                default:
                    return false;
            }
        }

        // Σημείωση: Πρέπει πλέον να δέχεται το thermo από το ProcessTick του Orchestrator
        public void EvaluateWorkplaceHazards(ThermodynamicsSubsystem thermo)
        {
            Random rng = new Random();
            int casualtiesThisTick = 0; // Μετρητής για το Casualty Cap

            double citySicknessRate = CurrentCityStage switch
            {
                CityLivingStage.Comfortable => GameSettings.Instance.SicknessRate_Comfortable,
                CityLivingStage.Warm => GameSettings.Instance.SicknessRate_Warm,
                CityLivingStage.Livable => GameSettings.Instance.SicknessRate_Livable,
                CityLivingStage.Cold => GameSettings.Instance.SicknessRate_Cold,
                CityLivingStage.Freezing => GameSettings.Instance.SicknessRate_Freezing,
                _ => 0.0
            };

            // ΑΝΑΚΑΤΕΜΑ: Ανακατεύουμε τις δουλειές για να είναι δίκαιο το ποιος θα δεχτεί το χτύπημα
            var shuffledJobs = _jobManager.Jobs.Values.OrderBy(x => rng.Next()).ToList();

            foreach (var job in shuffledJobs)
            {
                if (job.AssignedWorkers <= 0) continue;

                bool isNearGenerator = IsWorkingNearGenerator(job.Type);
                double baseHazard = 0.0;
                bool isWeatherRelated = !isNearGenerator;

                // 1. ΔΥΝΑΜΙΚΟΣ ΚΙΝΔΥΝΟΣ ΒΑΣΕΙ ΘΕΡΜΟΚΡΑΣΙΑΣ (HEAT PROTECTION)
                if (isNearGenerator)
                {
                    double safeTemp = 80.0;
                    double heatProtection = 1.0; // Ξεκινάμε θεωρώντας τους 100% προστατευμένους

                    if (thermo.CoreTemperature < safeTemp)
                    {
                        // Αν η γεννήτρια κρυώσει πολύ (από 80 προς τα κάτω), χάνουν σταδιακά την προστασία
                        double tempRange = safeTemp - GameSettings.Instance.AmbientWeatherTemperature;
                        double currentTempAboveAmbient = thermo.CoreTemperature - GameSettings.Instance.AmbientWeatherTemperature;
                        heatProtection = Math.Clamp(currentTempAboveAmbient / tempRange, 0.0, 1.0);
                    }

                    // Εφαρμογή των κινδύνων (Με heatProtection = 1.0, το coldHazard γίνεται 0.0)
                    double coldHazard = citySicknessRate * (1.0 - heatProtection);

                    // Εγκαύματα/Τραυματισμοί υφίστανται μόνο όταν η μηχανή καίει (heatProtection > 0)
                    double burnHazard = GameSettings.Instance.GeneratorJobInjuryRate * heatProtection;

                    baseHazard = coldHazard + burnHazard;

                    if (coldHazard > burnHazard) isWeatherRelated = true;
                }
                else
                {
                    baseHazard = citySicknessRate;
                }

                // 2. Πέναλτι Συνωστισμού (Overcrowding)
                double overcrowdingPenalty = 0.0;
                if (job.AssignedWorkers > job.OptimalWorkers)
                {
                    int extraWorkers = job.AssignedWorkers - job.OptimalWorkers;
                    overcrowdingPenalty = extraWorkers * GameSettings.Instance.OvercrowdingHazardPenalty;
                }

                // 3. Τελικός Υπολογισμός
                double totalHazard = baseHazard + overcrowdingPenalty;
                double normalizedHazardPerTick = totalHazard * (InjuryCheckInterval / 60.0);

                int currentWorkers = job.AssignedWorkers;
                for (int i = 0; i < currentWorkers; i++)
                {
                    if (rng.NextDouble() < normalizedHazardPerTick)
                    {
                        ApplyInjury(job, rng, isWeatherRelated, overcrowdingPenalty > 0);

                        // ΝΕΟ: Αυξάνουμε τον μετρητή. Αν φτάσουμε το όριο, σταματάμε τον έλεγχο!
                        casualtiesThisTick++;
                        if (casualtiesThisTick >= GameSettings.Instance.MaxCasualtiesPerEvaluation)
                        {
                            return; // Βγαίνουμε εντελώς από τη μέθοδο, γλιτώνοντας τους υπόλοιπους
                        }
                    }
                }
            }
        }

        // Ελαφρώς τροποποιημένη για να δίνει και το σωστό μήνυμα αν φταίει ο συνωστισμός
        private void ApplyInjury(JobOrder job, Random rng, bool isWeatherRelated, bool isOvercrowded)
        {
            job.AssignedWorkers--;
            HealthyWorkers--;

            string jobName = GetJobDisplayName(job.Type);
            string debugPrefix = $"[DEBUG - {jobName} ({job.AssignedWorkers}/{job.OptimalWorkers})]: ";

            bool isCatastrophic = isWeatherRelated &&
                                  (CurrentCityStage == CityLivingStage.Freezing) &&
                                  (rng.NextDouble() < GameSettings.Instance.DeathRollProbability);

            if (isCatastrophic)
            {
                if (rng.NextDouble() < 0.5)
                {
                    DeadWorkers++;
                    string msg = $"ΘΑΝΑΤΗΦΟΡΟ ΨΥΧΟΣ: Ένας κάτοικος υπέκυψε στην παγωνιά! (Στάδιο: {CurrentCityStage})";
                    GD.Print(debugPrefix + msg); // Debug Console Output
                    OnCasualtyOccurred?.Invoke(msg);
                }
                else
                {
                    GraveWorkers++;
                    string msg = $"ΚΡΥΟΠΑΓΗΜΑ: Ένας εργάτης μεταφέρθηκε κλινήρης! (Στάδιο: {CurrentCityStage})";
                    GD.Print(debugPrefix + msg);
                    OnCasualtyOccurred?.Invoke(msg);
                }
            }
            else
            {
                SickWorkers++;

                if (isOvercrowded && rng.NextDouble() < 0.3)
                {
                    string msg = $"ΑΤΥΧΗΜΑ ΣΥΝΩΣΤΙΣΜΟΥ: Επικρατεί χάος στο τμήμα. Ένας εργάτης τραυματίστηκε!";
                    GD.Print(debugPrefix + msg);
                    OnCasualtyOccurred?.Invoke(msg);
                }
                else if (isWeatherRelated)
                {
                    string msg = $"ΑΣΘΕΝΕΙΑ: Ένας εργάτης αρρώστησε λόγω του κρύου.";
                    GD.Print(debugPrefix + msg);
                    OnCasualtyOccurred?.Invoke(msg);
                }
                else
                {
                    string msg = $"ΤΡΑΥΜΑΤΙΣΜΟΣ ΕΡΓΑΣΙΑΣ: Εγκαύματα/Τραυματισμός κατά τη διάρκεια εργασίας στη γεννήτρια.";
                    GD.Print(debugPrefix + msg);
                    OnCasualtyOccurred?.Invoke(msg);
                }
            }
        }

        // Καλείται τη νύχτα (ή στο τέλος της βάρδιας) για ανάρρωση
        public void ProcessOvernightHealing(bool hasIntensiveCare)
        {
            Random rng = new Random();

            // Το ποσοστό ίασης μειώνεται δραματικά αν η πόλη είναι παγωμένη
            double environmentPenalty = CurrentCityStage switch
            {
                CityLivingStage.Freezing => 0.8, // 80% μείωση στην πιθανότητα ίασης!
                CityLivingStage.Cold => 0.4,
                CityLivingStage.Comfortable => -0.2, // Bonus ίασης αν είναι ζεστά
                _ => 0.0
            };

            double healChance = hasIntensiveCare
                ? GameSettings.Instance.IntensiveCareHealChance
                : GameSettings.Instance.NormalHealChance;

            healChance = Math.Clamp(healChance - environmentPenalty, 0.05, 1.0); // Ποτέ κάτω από 5% 

            int initialSick = SickWorkers;
            for (int i = 0; i < initialSick; i++)
            {
                if (rng.NextDouble() < healChance)
                {
                    SickWorkers--;
                    HealthyWorkers++;
                }
            }
        }
    }
}