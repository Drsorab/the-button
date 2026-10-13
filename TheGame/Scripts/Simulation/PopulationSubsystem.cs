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
                EvaluateWorkplaceHazards();
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

        private void EvaluateWorkplaceHazards()
        {
            Random rng = new Random();

            // Βασική πιθανότητα ασθένειας από το κρύο
            double citySicknessRate = CurrentCityStage switch
            {
                CityLivingStage.Comfortable => GameSettings.Instance.SicknessRate_Comfortable,
                CityLivingStage.Warm => GameSettings.Instance.SicknessRate_Warm,
                CityLivingStage.Livable => GameSettings.Instance.SicknessRate_Livable,
                CityLivingStage.Cold => GameSettings.Instance.SicknessRate_Cold,
                CityLivingStage.Freezing => GameSettings.Instance.SicknessRate_Freezing,
                _ => 0.0
            };

            // ΝΕΟ: Υπολογισμός του Safety Bonus από το Workshop Upkeep
            int upkeepWorkers = _jobManager.Jobs.ContainsKey(JobType.WorkshopUpkeep) ? _jobManager.Jobs[JobType.WorkshopUpkeep].AssignedWorkers : 0;
            double safetyBonus = Math.Min(GameSettings.Instance.MaxUpkeepSafetyBonus, upkeepWorkers * GameSettings.Instance.UpkeepSafetyBonusPerWorker);

            // Μειώνουμε τον κίνδυνο βάσει του Bonus
            double mitigatedHazard = citySicknessRate * (1.0 - safetyBonus);
            double normalizedHazardPerTick = mitigatedHazard * (InjuryCheckInterval / 60.0);

            foreach (var job in _jobManager.Jobs.Values)
            {
                if (job.AssignedWorkers <= 0) continue;

                int currentWorkers = job.AssignedWorkers;
                for (int i = 0; i < currentWorkers; i++)
                {
                    if (rng.NextDouble() < normalizedHazardPerTick)
                    {
                        ApplyInjury(job, rng, isWeatherRelated: true);
                    }
                }
            }
        }

        private void ApplyInjury(JobOrder job, Random rng, bool isWeatherRelated)
        {
            job.AssignedWorkers--;
            HealthyWorkers--;

            // Αν η πόλη είναι στο Freezing (Ακραία Παγωνιά), οι αρρώστιες γίνονται θανατηφόρες!
            bool isCatastrophic = (CurrentCityStage == CityLivingStage.Freezing) && (rng.NextDouble() < GameSettings.Instance.DeathRollProbability);

            if (isCatastrophic)
            {
                if (rng.NextDouble() < 0.5) // 50/50 να πεθάνει ή να μπει στην εντατική
                {
                    DeadWorkers++;
                    OnCasualtyOccurred?.Invoke($"ΘΑΝΑΤΗΦΟΡΟ ΨΥΧΟΣ: Ένας κάτοικος υπέκυψε στην παγωνιά! (Στάδιο: {CurrentCityStage})");
                }
                else
                {
                    GraveWorkers++;
                    OnCasualtyOccurred?.Invoke($"ΚΡΥΟΠΑΓΗΜΑ: Ένας εργάτης μεταφέρθηκε κλινήρης! (Στάδιο: {CurrentCityStage})");
                }
            }
            else
            {
                SickWorkers++;
                if (isWeatherRelated)
                    OnCasualtyOccurred?.Invoke($"ΑΣΘΕΝΕΙΑ: Ένας εργάτης αρρώστησε λόγω του κρύου.");
                else
                    OnCasualtyOccurred?.Invoke($"ΤΡΑΥΜΑΤΙΣΜΟΣ: Ένας εργάτης τραυματίστηκε ελαφρά.");
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