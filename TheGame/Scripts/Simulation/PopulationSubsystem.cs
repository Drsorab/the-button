using System;
using System.Linq;

namespace PermafrostChieftain.Simulation
{
    public class PopulationSubsystem
    {
        public int TotalPopulation => HealthyWorkers + SickWorkers + GraveWorkers;
        public int HealthyWorkers { get; private set; } = GameSettings.Instance.StartingHealthy;
        public int SickWorkers { get; private set; } = GameSettings.Instance.StartingSick;
        public int GraveWorkers { get; private set; } = GameSettings.Instance.StartingGrave;
        public int DeadWorkers { get; private set; } = 0;

        // Εύρεση διαθέσιμων εργατών (υγιείς μείον αυτοί που έχουν ήδη ανατεθεί)
        public int UnassignedReserve => HealthyWorkers - GetTotalAssignedWorkers();

        private readonly JobManager _jobManager;
        private double _injuryCheckTimer = 0.0;
        private const double InjuryCheckInterval = 60.0; // Έλεγχος τραυματισμών κάθε 60 in-game δευτερόλεπτα

        // Γεγονός για το UI/Logs όταν κάποιος πεθάνει ή τραυματιστεί
        public event Action<string> OnCasualtyOccurred;

        public PopulationSubsystem(JobManager jobManager)
        {
            _jobManager = jobManager;
        }

        public int GetTotalAssignedWorkers()
        {
            return _jobManager.Jobs.Values.Sum(job => job.AssignedWorkers);
        }

        // Αυξομείωση εργατών σε μια συγκεκριμένη εργασία
        public bool TryAdjustWorkers(JobType type, int delta)
        {
            if (!_jobManager.Jobs.TryGetValue(type, out JobOrder job)) return false;

            if (delta > 0 && UnassignedReserve < delta) return false; // Δεν επαρκούν οι υγιείς
            if (delta < 0 && job.AssignedWorkers + delta < 0) return false; // Δεν γίνεται να είναι αρνητικοί

            job.AssignedWorkers += delta;
            return true;
        }

        public void ProcessTick(double deltaTime, BoilerSubsystem boiler, ThermodynamicsSubsystem thermo, double workshopHazard)
        {
            if (deltaTime <= 0.0) return;

            _injuryCheckTimer += deltaTime;
            if (_injuryCheckTimer >= InjuryCheckInterval)
            {
                EvaluateWorkplaceHazards(boiler, thermo, workshopHazard);
                _injuryCheckTimer = 0.0;
            }
        }

        private void EvaluateWorkplaceHazards(BoilerSubsystem boiler, ThermodynamicsSubsystem thermo, double workshopHazard)
        {
            Random rng = new Random();

            foreach (var job in _jobManager.Jobs.Values)
            {
                if (job.AssignedWorkers <= 0) continue;

                // Υπολογισμός Κινδύνου Περιβάλλοντος (X)
                double xHazard = CalculateEnvironmentHazard(job.Type, boiler, thermo, workshopHazard);

                // Υπολογισμός Κινδύνου Εργάτη (Y) - Ποινή συνωστισμού
                double crowdingRatio = ((double)job.AssignedWorkers / job.OptimalWorkers) - 1.0;
                double yHazard = 0.01 + Math.Max(0, crowdingRatio * 0.03); // Το base risk είναι 1%

                double totalHazard = xHazard + yHazard;

                // Ρίψη ζαριού για κάθε εργάτη στη συγκεκριμένη εργασία
                int currentWorkers = job.AssignedWorkers;
                for (int i = 0; i < currentWorkers; i++)
                {
                    double roll = rng.NextDouble();
                    if (roll < totalHazard)
                    {
                        ApplyInjury(job, totalHazard, rng);
                    }
                }
            }
        }

        private double CalculateEnvironmentHazard(JobType type, BoilerSubsystem boiler, ThermodynamicsSubsystem thermo, double workshopHazard)
        {
            // Επιστρέφει το X (Workplace Hazard) βάσει των συνθηκών της μηχανής
            return type switch
            {
                JobType.StokeFurnace => 0.01 + (thermo.CoreTemperature > 850 ? 0.10 : 0.0),
                JobType.MicroBleed => 0.02 + (boiler.SteamPressure > 150 ? 0.08 : 0.0),
                JobType.StructuralBracing => 0.03 + (boiler.SteamPressure > 180 ? 0.12 : 0.0),
                JobType.ScourFlues => 0.02 + (boiler.SootFraction * 0.10),
                JobType.DeepScour => 0.01 + (boiler.SootFraction * 0.05), // Ασφαλέστερο το βράδυ
                JobType.DamperThrottling => 0.01 + (thermo.CityTemperature < -60 ? 0.05 : 0.0),
                JobType.WorkshopMaintenance => 0.01, // Επίπεδο ελάχιστου κινδύνου
                JobType.IntensiveCare => 0.005,      // Ασφαλέστερη εργασίαna kelsv
                JobType.WorkshopMachining => 0.02 + workshopHazard, // Επικίνδυνο λόγω βαρέων μηχανημάτων και ρεύματος
                JobType.ManualTinkering => 0.005 + (workshopHazard * 0.5), // Πιο ασφαλές λόγω εργαλείων χειρός
                _ => 0.01
            };
        }

        private void ApplyInjury(JobOrder job, double hazardScore, Random rng)
        {
            // Αφαίρεση του εργάτη από την τρέχουσα δουλειά του
            job.AssignedWorkers--;
            HealthyWorkers--;

            // Το όριο καταστροφής (Catastrophic Threshold)
            if (hazardScore > GameSettings.Instance.HazardDeathThreshold)
            {
                // Εκθετική αύξηση της πιθανότητας για θάνατο ή βαρύ τραυματισμό (Grave)
                double deathRoll = rng.NextDouble();
                if (deathRoll < GameSettings.Instance.DeathRollProbability)
                {
                    DeadWorkers++;
                    OnCasualtyOccurred?.Invoke($"ΘΑΝΑΤΗΦΟΡΟ ΑΤΥΧΗΜΑ: Ένας εργάτης σκοτώθηκε εκτελώντας: {job.Type}");
                }
                else
                {
                    GraveWorkers++;
                    OnCasualtyOccurred?.Invoke($"ΚΡΙΣΙΜΟΣ ΤΡΑΥΜΑΤΙΣΜΟΣ: Σοβαρό ατύχημα στο τμήμα: {job.Type}");
                }
            }
            else
            {
                // Απλός τραυματισμός
                SickWorkers++;
                OnCasualtyOccurred?.Invoke($"ΤΡΑΥΜΑΤΙΣΜΟΣ: Ένας εργάτης τραυματίστηκε ελαφρά στο τμήμα: {job.Type}");
            }
        }

        // Θεραπεία ασθενών κατά τη διάρκεια της νύχτας, με boost από το Intensive Care Job
        public void ProcessOvernightHealing(bool hasIntensiveCareBoost)
        {
            Random rng = new Random();
            double healChance = hasIntensiveCareBoost ? GameSettings.Instance.IntensiveCareHealChance : GameSettings.Instance.NormalHealChance; //0.6 ,  60% με νοσηλευτές, 0.2, 20% μόνοι τους

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