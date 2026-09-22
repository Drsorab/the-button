using Godot;
using System;
using System.Collections.Generic;
using PermafrostChieftain.Simulation;

namespace PermafrostChieftain.Orchestration
{
    public partial class GameOrchestrator : Node
    {
        // Subsystems
        public SimulationClock Clock { get; private set; }
        public JobManager JobManager { get; private set; }
        public ThermodynamicsSubsystem Thermodynamics { get; private set; }
        public BoilerSubsystem Boiler { get; private set; }
        public PopulationSubsystem Population { get; private set; }

        // Απλοποιημένη λογική logistics (Αποθέματα και Κίνδυνος Μηχανουργείου)
        public LogisticsTelemetry Logistics;

        // Γεγονότα (Events) για το UI
        public event Action TelemetryUpdated;
        public event Action<int> PhaseTransitionTriggered;
        public event Action<double, string, string, string> LogEventDispatched;
        public event Action<string, string> GameOverTriggered;

        // Snapshots Τηλεμετρίας για το UIOrchestrator
        public BoilerTelemetry LatestBoilerTelemetry;
        public ThermodynamicsTelemetry LatestThermodynamicTelemetry;
        public WorkforceTelemetry LatestWorkforceTelemetry;
        public List<JobTelemetry> LatestJobsTelemetry = new List<JobTelemetry>();
        public LogisticsTelemetry LatestLogisticsTelemetry;
        public override void _EnterTree()
        {
            if (Clock == null) InitializeSimulation();
        }

        public void InitializeSimulation()
        {
            GameSettings.Load();

            Clock = new SimulationClock();
            JobManager = new JobManager();
            Thermodynamics = new ThermodynamicsSubsystem();
            Boiler = new BoilerSubsystem();
            Population = new PopulationSubsystem(JobManager);

            // Αρχικά Αποθέματα
            Logistics.CoalStockpile = GameSettings.Instance.StartingCoal;
            Logistics.ScrapStockpile = GameSettings.Instance.StartingScrap;
            Logistics.Electricity = GameSettings.Instance.StartingElectricity;
            Logistics.WorkshopHazardLevel = 0.0;

            // Σύνδεση γεγονότων του Ρολογιού και των Εργασιών
            Clock.VentGracePeriodExpired += OnVentGracePeriodExpired;
            Clock.VentModalTimeoutExpired += OnVentModalTimeoutExpired;
            JobManager.OnJobCompleted += HandleJobCompleted;
            Population.OnCasualtyOccurred += (msg) => DispatchLog("CRITICAL", "MEDICAL", msg);
        }

        public override void _Process(double delta)
        {
            if (Clock == null) return;

            bool phaseChanged = false;

            // Το ρολόι επιστρέφει true αν αλλάξαμε φάση (π.χ. άνοιξε το Modal Εκτόνωσης)
            if (Clock.Tick(delta, out phaseChanged))
            {
                PhaseTransitionTriggered?.Invoke((int)Clock.CurrentPhase);
            }

            // Η προσομοίωση τρέχει ΜΟΝΟ αν δεν είμαστε σε κατάσταση επιλογής Routing
            if (Clock.CurrentPhase != CyclePhase.VentRouting)
            {
                Boiler.ProcessTick(delta, Thermodynamics.CoreTemperature, Clock.CurrentPhase);

                // Υπολογισμός κατανάλωσης άνθρακα
                double coalBurnRate = 0.2;
                Logistics.CoalStockpile = Math.Max(0, Logistics.CoalStockpile - (coalBurnRate * delta));

                Thermodynamics.ProcessThermalTick(delta, coalBurnRate, Boiler.SootFraction);
                if (Clock.CurrentPhase == CyclePhase.NightBleed && Logistics.Electricity > 0)
                {
                    // Τη νύχτα, το διαθέσιμο ρεύμα μετατρέπεται σιγά-σιγά σε θερμότητα για την πόλη
                    double electricityDrainRate = 1.5; // Καταναλώνει 1.5 Relay ανά δευτερόλεπτο
                    double actualDrain = Math.Min(Logistics.Electricity, electricityDrainRate * delta);
                    Logistics.Electricity -= actualDrain;
                    Thermodynamics.AddCityHeatBuffer(actualDrain * 0.8); // 80% απόδοση μετατροπής σε θερμότητα
                }
                JobManager.Tick(delta);
                Population.ProcessTick(delta, Boiler, Thermodynamics, Logistics.WorkshopHazardLevel);
            }

            UpdateTelemetrySnapshots();
        }

        // --- 1. Διαχείριση Εκτόνωσης (Vent Routing) ---

        // Καλείται από το Modal του UI όταν ο παίκτης κατανέμει την πίεση (108 λεπτά)
        public void ExecuteVentRouting(double cityHeat, double workshopPower, double drillPower, double retain)
        {
            double totalVented = cityHeat + workshopPower + drillPower;
            Boiler.DeductPressure(totalVented);

            Thermodynamics.AddCityHeatBuffer(cityHeat);

            // Η πίεση προς το workshop μετατρέπεται σε ρεύμα (Relay)
            Logistics.Electricity += workshopPower * 2.0;

            Logistics.CoalStockpile += drillPower * 15.0;
            Logistics.ScrapStockpile += drillPower * 2.0;

            DispatchLog("INFO", "ROUTING", $"Εκτόνωση: Πόλη={cityHeat} | Ρεύμα={workshopPower * 2.0} | Drill={drillPower}");

            Clock.AdvanceAfterVent(out bool phaseChanged);
            if (phaseChanged) PhaseTransitionTriggered?.Invoke((int)Clock.CurrentPhase);
        }

        private void OnVentGracePeriodExpired()
        {
            if (Boiler.AvailableFuses > 0)
            {
                Boiler.AvailableFuses--;
                DispatchLog("WARNING", "PRESSURE", "Ο χρόνος έληξε! Μια ασφάλεια κάηκε για την αυτόματη εκτόνωση.");
                // Αυτόματη ασφαλής εκτόνωση: 20 Bar στην πόλη, τίποτα αλλού
                ExecuteVentRouting(20.0, 0, 0, Math.Max(0, Boiler.SteamPressure - 20.0));
            }
            else
            {
                DispatchLog("CRITICAL", "HULL", "Αποτυχία εκτόνωσης! Το κέλυφος ράγισε!");
                Boiler.TriggerCatastrophicFailure();
                Clock.AdvanceAfterVent(out bool phaseChanged);
                if (phaseChanged) PhaseTransitionTriggered?.Invoke((int)Clock.CurrentPhase);
            }
        }

        private void OnVentModalTimeoutExpired()
        {
            DispatchLog("WARNING", "PRESSURE", "Το σύστημα δρομολόγησης έκλεισε λόγω αδράνειας. Αυτόματη εκτόνωση.");
            ExecuteVentRouting(20.0, 0, 0, Math.Max(0, Boiler.SteamPressure - 20.0));
        }

        // --- 2. Ανταμοιβές Εργασιών (Time-Scaled Rewards) ---

        private void HandleJobCompleted(JobType type)
        {
            switch (type)
            {
                case JobType.StokeFurnace:
                    // Καταναλώνει μια μεγάλη παρτίδα άνθρακα και δημιουργεί ραγδαία αύξηση θερμοκρασίας.
                    // Περισσότεροι εργάτες = Πιο γρήγορη ολοκλήρωση = Πολλαπλάσιες ρίψεις άνθρακα ανά ώρα.
                    double coalCost = GameSettings.Instance.StokeCoalCost;
                    if (Logistics.CoalStockpile >= coalCost)
                    {
                        Logistics.CoalStockpile -= coalCost;
                        Thermodynamics.ApplyDirectThermalDelta(GameSettings.Instance.StokeHeatProduced, 0); 
                        DispatchLog("INFO", "JOBS", $"Ρίψη άνθρακα: Καταναλώθηκαν {coalCost}kg, +40°C στον πυρήνα.");
                    }
                    else
                    {
                        DispatchLog("WARNING", "JOBS", "Αποτυχία τροφοδοσίας: Τα αποθέματα άνθρακα εξαντλήθηκαν!");
                    }
                    break;

                case JobType.StructuralBracing:
                    if (Logistics.ScrapStockpile >= GameSettings.Instance.BracingScrapCost)
                    {
                        Boiler.RepairCasing(GameSettings.Instance.BracingRepairAmount);
                        Logistics.ScrapStockpile -= GameSettings.Instance.BracingScrapCost;
                        DispatchLog("INFO", "JOBS", "Ολοκληρώθηκε η ενίσχυση του κελύφους (+15% Αντοχή).");
                    }
                    else
                    {
                        DispatchLog("WARNING", "JOBS", "Αποτυχία επισκευής: Δεν υπάρχει διαθέσιμο Scrap!");
                    }
                    break;

                case JobType.ScourFlues:
                case JobType.DeepScour:
                    double coalYield = JobManager.CalculateSootCleaningReward();
                    Logistics.CoalStockpile += coalYield;
                    Boiler.CleanSoot(type == JobType.DeepScour ? 0.5 : 0.2); // Το βράδυ καθαρίζει καλύτερα
                    DispatchLog("INFO", "JOBS", $"Ο καθαρισμός αιθάλης ολοκληρώθηκε. Βρέθηκαν {coalYield:F0}kg άνθρακα.");
                    break;

                case JobType.WorkshopMaintenance:
                    double scrapYield = JobManager.CalculateWorkshopMaintenanceReward();
                    Logistics.ScrapStockpile += scrapYield;
                    Logistics.WorkshopHazardLevel = 0.0; // Μηδενίζει τον κίνδυνο ατυχημάτων
                    DispatchLog("INFO", "JOBS", $"Το μηχανουργείο συντηρήθηκε. Ανακτήθηκαν {scrapYield:F0} Scrap.");
                    break;

                case JobType.DamperThrottling:
                    // Κρατάει την πίεση στο σύστημα και παράγει ένα μικρό χημικό υποπροϊόν
                    Logistics.ScrapStockpile += 2.0;
                    DispatchLog("INFO", "JOBS", "Οι ρυθμιστές συντηρήθηκαν. Ανακτήθηκαν υλικά καθαρισμού (+2 Scrap).");
                    break;

                case JobType.IntensiveCare:
                    // Προκαλεί έναν κύκλο άμεσης θεραπείας στους τραυματίες
                    Population.ProcessOvernightHealing(true);
                    DispatchLog("INFO", "JOBS", "Ολοκληρώθηκε κύκλος εντατικής θεραπείας. Κάποιοι ασθενείς αναρρώνουν.");
                    break;
                case JobType.MicroBleed:
                    Boiler.DeductPressure(GameSettings.Instance.MicroBleedPressureDeduct);
                    if (JobManager.ActiveMicroBleedTarget == MicroBleedTarget.Atmosphere)
                    {
                        DispatchLog("INFO", "JOBS", "Η μικρο-εκτόνωση ολοκληρώθηκε (-15 Bar στην Ατμόσφαιρα).");
                    }
                    else if (JobManager.ActiveMicroBleedTarget == MicroBleedTarget.CityHeat)
                    {
                        Thermodynamics.AddCityHeatBuffer(GameSettings.Instance.MicroBleedCityHeatAdd);
                        DispatchLog("INFO", "JOBS", "Η μικρο-εκτόνωση ολοκληρώθηκε (+15 Θερμότητα στην Πόλη).");
                    }
                    else if (JobManager.ActiveMicroBleedTarget == MicroBleedTarget.SubterraneanDrill)
                    {
                        Logistics.CoalStockpile += GameSettings.Instance.MicroBleedDrillCoalAdd;
                        DispatchLog("INFO", "JOBS", "Η μικρο-εκτόνωση ολοκληρώθηκε (+20kg Άνθρακας από το Drill).");
                    }
                    else if (JobManager.ActiveMicroBleedTarget == MicroBleedTarget.WorkshopPower)
                    {
                        Logistics.Electricity += GameSettings.Instance.MicroBleedWorkshopElecAdd;
                        DispatchLog("INFO", "JOBS", "Η μικρο-εκτόνωση ολοκληρώθηκε (+10 kW Ρεύμα στο Μηχανουργείο).");
                    }
                    break;

                case JobType.WorkshopMachining:
                case JobType.ManualTinkering:
                    var recipe = GameSettings.Instance.CraftingRecipes[JobManager.ActiveCraftingRecipe];

                    // Η νυχτερινή χειρωνακτική εργασία ΔΕΝ καταναλώνει ρεύμα
                    double elecCost = (type == JobType.WorkshopMachining) ? recipe.ElectricityCost : 0.0;

                    // Η ημερήσια εργασία αυξάνει τον κίνδυνο πιο πολύ (+0.1) από τη νυχτερινή (+0.05)
                    double hazardIncrease = (type == JobType.WorkshopMachining) ? 0.1 : 0.05;

                    if (Logistics.Electricity >= elecCost && Logistics.ScrapStockpile >= recipe.ScrapCost)
                    {
                        Logistics.Electricity -= elecCost;
                        Logistics.ScrapStockpile -= recipe.ScrapCost;
                        Logistics.WorkshopHazardLevel = Math.Min(1.0, Logistics.WorkshopHazardLevel + hazardIncrease);

                        // Απόδοση της ανταμοιβής βάσει του τι κατασκευάστηκε
                        if (JobManager.ActiveCraftingRecipe == "Fuse") Boiler.AvailableFuses++;
                        else if (JobManager.ActiveCraftingRecipe == "Hull Patch") Boiler.RepairCasing(10.0);
                        else if (JobManager.ActiveCraftingRecipe == "Medical Tonic") Population.ProcessOvernightHealing(true);

                        string shiftName = type == JobType.WorkshopMachining ? "Μηχανουργείο (Ημέρα)" : "Μηχανουργείο (Νύχτα)";
                        DispatchLog("INFO", "JOBS", $"{shiftName}: Κατασκευάστηκε [{JobManager.ActiveCraftingRecipe}].");
                    }
                    else
                    {
                        DispatchLog("WARNING", "JOBS", $"Παύση Κατασκευής ({JobManager.ActiveCraftingRecipe}): Ανεπαρκή υλικά ή ρεύμα!");
                    }
                    break;
            }
        }

        // --- 3. Τηλεμετρία και Καταγραφή ---

        public void DispatchLog(string severity, string category, string message)
        {
            LogEventDispatched?.Invoke(Clock?.CurrentTimeOfDaySeconds ?? 0, severity, category, message);
        }

        private void UpdateTelemetrySnapshots()
        {
            LatestBoilerTelemetry = new BoilerTelemetry
            {
                SteamPressure = Boiler.SteamPressure,
                CasingIntegrity = Boiler.CasingIntegrity,
                SootFraction = Boiler.SootFraction,
                AvailableFuses = Boiler.AvailableFuses,
                IsHullCracked = Boiler.IsHullCracked,
                CurrentPhase = Clock.CurrentPhase,
                SecondsUntilNextVent = Clock.GetSecondsUntilNextVent()
            };

            LatestThermodynamicTelemetry = new ThermodynamicsTelemetry
            {
                CoreTemperature = Thermodynamics.CoreTemperature,
                CityTemperature = Thermodynamics.CityTemperature,
                DraftStance = Thermodynamics.ActiveDraftStance
            };

            LatestWorkforceTelemetry = new WorkforceTelemetry
            {
                TotalPopulation = Population.TotalPopulation,
                HealthyWorkers = Population.HealthyWorkers,
                SickWorkers = Population.SickWorkers,
                GraveWorkers = Population.GraveWorkers,
                UnassignedReserve = Population.UnassignedReserve
            };

            // Δημιουργία snapshot για όλες τις εργασίες (για να τροφοδοτήσουμε τις μπάρες προόδου στο UI)
            LatestJobsTelemetry.Clear();
            foreach (var job in JobManager.Jobs.Values)
            {
                LatestJobsTelemetry.Add(new JobTelemetry
                {
                    Type = job.Type,
                    AssignedWorkers = job.AssignedWorkers,
                    WorkRemaining = job.WorkRemaining,
                    BaseWorkRequired = job.BaseWorkRequired
                });
            }
            LatestLogisticsTelemetry = Logistics;

            TelemetryUpdated?.Invoke();
        }
    }
}