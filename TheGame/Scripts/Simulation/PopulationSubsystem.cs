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
        private const double InjuryCheckInterval = 60.0; // Έλεγχος τραυματισμών/ασθενειών κάθε 60 δευτερόλεπτα

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

            if (delta > 0 && UnassignedReserve < delta) return false;
            if (delta < 0 && job.AssignedWorkers + delta < 0) return false;

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

            // 1. Υπολογισμός Κινδύνου Καιρού της Πόλης (Ζ)
            double weatherHazard = CalculateCityWeatherHazard(thermo.CityTemperature);

            foreach (var job in _jobManager.Jobs.Values)
            {
                if (job.AssignedWorkers <= 0) continue;

                // 2. Υπολογισμός Κινδύνου Εργασίας/Μηχανής (X)
                double jobHazard = CalculateJobHazard(job.Type, boiler, thermo, workshopHazard);

                // 3. Υπολογισμός Κινδύνου Συνωστισμού (Y)
                double crowdingRatio = ((double)job.AssignedWorkers / job.OptimalWorkers) - 1.0;
                double crowdingHazard = 0.01 + Math.Max(0, crowdingRatio * 0.03);

                // ΣΥΝΟΛΙΚΟΣ ΚΙΝΔΥΝΟΣ ΑΝΑ ΩΡΑ = Εργασία + Συνωστισμός + Καιρός Πόλης
                double totalHazardPerHour = jobHazard + crowdingHazard + weatherHazard;

                // ΝΕΟ: Κανονικοποίηση (Normalization) του κινδύνου.
                // Οι πιθανότητες στα Settings (π.χ. 30%) αφορούν 1 ΟΛΟΚΛΗΡΗ ΩΡΑ (3600 in-game δευτερόλεπτα).
                // Επειδή τρέχουμε τον έλεγχο κάθε 60 δευτερόλεπτα, προσαρμόζουμε την πιθανότητα!
                // Έτσι, ένα 30% ανά ώρα γίνεται δίκαιο ~0.5% ανά λεπτό.
                double normalizedHazardPerTick = totalHazardPerHour * (InjuryCheckInterval / 3600.0);

                // Ρίψη ζαριού για κάθε εργάτη
                int currentWorkers = job.AssignedWorkers;
                for (int i = 0; i < currentWorkers; i++)
                {
                    double roll = rng.NextDouble();
                    if (roll < normalizedHazardPerTick)
                    {
                        // Αν το ζάρι "πιάσει", περνάμε το Base Hazard (totalHazardPerHour) για να δούμε 
                        // αν ο κίνδυνος ήταν αρκετά μεγάλος για να προκαλέσει θάνατο
                        ApplyInjury(job, totalHazardPerHour, rng, weatherHazard);
                    }
                }
            }
        }

        // ΝΕΑ ΜΕΘΟΔΟΣ: Δυναμικός υπολογισμός βάσει των Thresholds του GameSettings
        private double CalculateCityWeatherHazard(double cityTemp)
        {
            if (cityTemp > GameSettings.Instance.CityOverheatThreshold)
                return GameSettings.Instance.OverheatHazardPenalty; // Θερμοπληξία

            if (cityTemp < GameSettings.Instance.CityDeadlyThreshold)
                return GameSettings.Instance.DeadlyHazardPenalty;   // -10°C (Ακραίο Ψύχος)

            if (cityTemp < GameSettings.Instance.CityFreezingThreshold)
                return GameSettings.Instance.FreezingHazardPenalty; // 0°C (Παγετός)

            if (cityTemp < GameSettings.Instance.CityChillyThreshold)
                return GameSettings.Instance.ChillyHazardPenalty;   // 10°C (Κρύο)

            return 0.0; // Ιδανική Θερμοκρασία (Safe Zone)
        }

        private double CalculateJobHazard(JobType type, BoilerSubsystem boiler, ThermodynamicsSubsystem thermo, double workshopHazard)
        {
            // Αφαιρέσαμε το CityTemperature από εδώ, γιατί το μετράμε πλέον καθολικά (Global)
            return type switch
            {
                JobType.StokeFurnace => 0.01 + (thermo.CoreTemperature > 850 ? 0.10 : 0.0),
                JobType.MicroBleed => 0.02 + (boiler.SteamPressure > 150 ? 0.08 : 0.0),
                JobType.StructuralBracing => 0.03 + (boiler.SteamPressure > 180 ? 0.12 : 0.0),
                JobType.ScourFlues => 0.02 + (boiler.SootFraction * 0.10),
                JobType.DeepScour => 0.01 + (boiler.SootFraction * 0.05),
                JobType.DamperThrottling => 0.01,
                JobType.WorkshopMaintenance => 0.01,
                JobType.IntensiveCare => 0.005,
                JobType.WorkshopMachining => 0.02 + workshopHazard,
                JobType.ManualTinkering => 0.005 + (workshopHazard * 0.5),
                _ => 0.01
            };
        }

        private void ApplyInjury(JobOrder job, double totalHazardScore, Random rng, double weatherHazard)
        {
            // Αφαίρεση του εργάτη από την τρέχουσα δουλειά του
            job.AssignedWorkers--;
            HealthyWorkers--;

            // ΝΕΟ: Το ότι ο κίνδυνος είναι υψηλός, δεν σημαίνει ακαριαίο θάνατο. 
            // Δίνει απλά μια πιθανότητα το ατύχημα/κρύο να είναι ΣΟΒΑΡΟ αντί για απλή ασθένεια.
            bool isCatastrophic = false;

            if (totalHazardScore > GameSettings.Instance.HazardDeathThreshold)
            {
                // Αν περάσει το όριο (π.χ. 0.15), ρίχνουμε ζάρι για να δούμε αν είναι θανατηφόρο/κρίσιμο.
                // Χρησιμοποιούμε το DeathRollProbability (π.χ. 40% πιθανότητα να είναι όντως καταστροφικό)
                if (rng.NextDouble() < GameSettings.Instance.DeathRollProbability)
                {
                    isCatastrophic = true;
                }
            }

            if (isCatastrophic)
            {
                // Είναι σοβαρό! Τώρα ρίχνουμε για το αν θα πεθάνει ή θα μπει στην εντατική (Grave)
                if (rng.NextDouble() < GameSettings.Instance.DeathRollProbability)
                {
                    DeadWorkers++;
                    if (weatherHazard >= GameSettings.Instance.DeadlyHazardPenalty)
                        OnCasualtyOccurred?.Invoke($"ΘΑΝΑΤΗΦΟΡΟ ΨΥΧΟΣ: Ένας εργάτης υπέκυψε στο κρύο (Τμήμα: {job.Type}).");
                    else
                        OnCasualtyOccurred?.Invoke($"ΘΑΝΑΤΗΦΟΡΟ ΑΤΥΧΗΜΑ: Ένας εργάτης σκοτώθηκε (Τμήμα: {job.Type}).");
                }
                else
                {
                    GraveWorkers++;
                    OnCasualtyOccurred?.Invoke($"ΚΡΙΣΙΜΟΣ ΤΡΑΥΜΑΤΙΣΜΟΣ/ΝΟΣΗΛΕΙΑ: Σοβαρό περιστατικό στο τμήμα: {job.Type}");
                }
            }
            else
            {
                // Το ατύχημα / ασθένεια ήταν ελαφρύ. Πάει στους Sick (Ασθενείς) και θα φανεί στο UI!
                SickWorkers++;

                if (weatherHazard > 0 && rng.NextDouble() > 0.5)
                    OnCasualtyOccurred?.Invoke($"ΑΣΘΕΝΕΙΑ: Ένας εργάτης αρρώστησε λόγω του κρύου (Τμήμα: {job.Type}).");
                else
                    OnCasualtyOccurred?.Invoke($"ΤΡΑΥΜΑΤΙΣΜΟΣ: Ένας εργάτης τραυματίστηκε ελαφρά (Τμήμα: {job.Type}).");
            }
        }

        public void ProcessOvernightHealing(bool hasIntensiveCareBoost)
        {
            Random rng = new Random();
            double healChance = hasIntensiveCareBoost ? GameSettings.Instance.IntensiveCareHealChance : GameSettings.Instance.NormalHealChance;

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