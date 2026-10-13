using Godot;
using System;
using System.Collections.Generic;

namespace PermafrostChieftain.Simulation
{
    // 1. Το Godot απαιτεί το script να είναι partial και να κληρονομεί από CanvasLayer
    public partial class GameOrchestrator : CanvasLayer
    {
        private BoilerSubsystem _boiler;
        private ThermodynamicsSubsystem _thermo;
        private PopulationSubsystem _population;
        private LogisticsSubsystem _logistics;
        private JobManager _jobManager;
        private InventorySubsystem _inventory;

        public BoilerSubsystem Boiler => _boiler;
        public ThermodynamicsSubsystem Thermo => _thermo;
        public PopulationSubsystem Population => _population;
        public LogisticsSubsystem Logistics => _logistics;
        public JobManager JobManager => _jobManager;
        public InventorySubsystem Inventory => _inventory;

        public double TimeMultiplier { get; set; } = 1.0;

        public SimulationClock Clock { get; private set; }

        public event Action<string> OnGameEvent;
        public event Action<int> PhaseTransitionTriggered;
        public GameOrchestrator()
        {
            _boiler = new BoilerSubsystem();
            _thermo = new ThermodynamicsSubsystem();

            // Το JobManager πρέπει να δημιουργηθεί ΠΡΙΝ το Population
            _jobManager = new JobManager();

            // Τώρα περνάμε το JobManager με ασφάλεια στο Population
            _population = new PopulationSubsystem(_jobManager);

            _logistics = new LogisticsSubsystem();
            _inventory = new InventorySubsystem();
            Clock = new SimulationClock();
        }
        // 3. Η αρχικοποίηση γίνεται ΥΠΟΧΡΕΩΤΙΚΑ μέσω της _Ready στο Godot
        public override void _Ready()
        {
            Clock.VentGracePeriodExpired += HandleMissedVent;
            Clock.VentModalTimeoutExpired += HandleMissedVent;

            // Αφού πλέον είμαστε ο κεντρικός κόμβος (Root),
            // εκκινούμε το παιχνίδι από εδώ αυτόματα!
            InitializeGame(true, DateTime.Now);
        }
        public override void _Process(double delta)
        {
            ProcessTick(delta);
        }
        // =========================================================
        // ΑΡΧΙΚΟΠΟΙΗΣΗ & ΣΕΝΑΡΙΟ ΕΚΚΙΝΗΣΗΣ
        // =========================================================
        public void InitializeGame(bool isNewGame, DateTime currentRealTime)
        {
            _logistics.Initialize();
            _population.Initialize();
            _boiler.Initialize(100.0);
            _thermo.Initialize(GameSettings.Instance.AmbientWeatherTemperature, GameSettings.Instance.AmbientWeatherTemperature);

            if (isNewGame)
            {
                TriggerColdStartCrisis();
            }
        }

        private void TriggerColdStartCrisis()
        {
            // Αρχικό μπάλωμα στο Inventory
            _inventory.AddItem("Metal Patch", ItemCategory.Consumable);
            _boiler.RepairIntegrity(-50.0);
            _boiler.ForceHullCrack();

            // Ρύθμιση ρολογιού στο Twilight (τελευταία 48 λεπτά)
            Clock.StartAtMiddleOfDay();
            OnGameEvent?.Invoke("ΚΡΙΣΙΜΗ ΚΑΤΑΣΤΑΣΗ: Ξεχάσατε την 4η εκτόνωση και η γεννήτρια έσκασε! Έχετε λίγο χρόνο μέχρι τη Νυχτερινή Βάρδια για να κλείσετε το ρήγμα!");
        }

        // =========================================================
        // ΤΟ ΚΕΝΤΡΙΚΟ LOOP ΤΟΥ ΠΑΙΧΝΙΔΙΟΥ
        // =========================================================
        public void ProcessTick(double deltaTime)
        {
            if (deltaTime <= 0.0) return;

            double effectiveDelta = deltaTime * TimeMultiplier;

            bool wasDay = Clock.IsDayShift;
            Clock.Tick(effectiveDelta, out bool phaseChanged);

            if (phaseChanged)
            {
                HandlePhaseChange(wasDay);
            }

            // 1. ΕΚΤΕΛΕΣΗ ΟΙΚΟΝΟΜΙΑΣ (Συνεχόμενες Εργασίες)
            var (activeSmelters, thermalShock, coalBurnedPerSec) = _jobManager.ProcessContinuousJobs(effectiveDelta, _logistics, _boiler, _population);

            // 2. Νυχτερινή Συντήρηση (Μόνο το βράδυ)
            if (!Clock.IsDayShift)
            {
                double idleCoal = GameSettings.Instance.NightShiftIdleCoalConsumption * effectiveDelta;
                double actualIdleCoal = _logistics.ConsumeUpToCoal(idleCoal);
                coalBurnedPerSec += (actualIdleCoal / effectiveDelta);
            }

            // 3. ΘΕΡΜΟΔΥΝΑΜΙΚΗ & ΚΑΠΝΙΑ
            _boiler.AddSoot(coalBurnedPerSec * effectiveDelta);
            double steamGenerated = _thermo.ProcessTick(effectiveDelta, coalBurnedPerSec, activeSmelters, thermalShock, _boiler);

            // 4. ΛΕΒΗΤΑΣ
            _boiler.AddSteam(steamGenerated);
            _boiler.ProcessTick(effectiveDelta);

            // 5. ΠΛΗΘΥΣΜΟΣ & ΔΙΑΚΡΙΤΕΣ ΕΡΓΑΣΙΕΣ
            _population.ProcessTick(effectiveDelta, _thermo);
            _jobManager.ProcessDiscreteJobs(effectiveDelta, _logistics, _boiler, Clock.IsDayShift, _inventory);

            // 6. Νυχτερινή Διαρροή Ατμού
            if (!Clock.IsDayShift && _boiler.SteamPressure > 0)
            {
                double nightBleed = GameSettings.Instance.NightShiftCityBleedRatePerSec * effectiveDelta;
                double actualBleed = Math.Min(nightBleed, _boiler.SteamPressure);

                _boiler.DeductSteam(actualBleed);
                _thermo.AddSteamToCityBuffer(actualBleed);
            }

            foreach (var kvp in _jobManager.Jobs)
            {
                var job = kvp.Value;

                // Αν έχει εργάτες, αλλά ο χρόνος που απομένει είναι 0 (ή λιγότερο)
                if (job.AssignedWorkers > 0 && job.WorkRemaining <= 0)
                {
                    // Γεμίζουμε ξανά τον χρόνο (WorkRemaining) με βάση το τι δουλειά είναι!
                    job.WorkRemaining = job.Type switch
                    {
                        JobType.CleanSoot => GameSettings.Instance.CleanSootWorkRequired,
                        JobType.DaytimePatch => GameSettings.Instance.PatchWorkRequired,
                        JobType.NightHullRepair => GameSettings.Instance.NightRepairWorkRequired,
                        JobType.WorkshopCraftPatch => GameSettings.Instance.CraftMetalPatchWorkRequired,
                        JobType.WorkshopCraftFuse => GameSettings.Instance.CraftFuseWorkRequired,
                        _ => 0.0
                    };
                }
            }
        }

        private void HandlePhaseChange(bool wasDayBefore)
        {
            // Ειδοποιούμε το UI (π.χ. το ShiftDebriefController) για την αλλαγή
            PhaseTransitionTriggered?.Invoke((int)Clock.CurrentPhase);

            if (wasDayBefore && !Clock.IsDayShift)
            {
                OnGameEvent?.Invoke("Η Ημερήσια Βάρδια έληξε. Ξεκινάει η Νυχτερινή Επιβίωση.");
                bool hasIntensiveCare = _jobManager.Jobs.ContainsKey(JobType.IntensiveCare) && _jobManager.Jobs[JobType.IntensiveCare].AssignedWorkers > 0;
                _population.ProcessOvernightHealing(hasIntensiveCare);
            }
            else if (!wasDayBefore && Clock.IsDayShift)
            {
                OnGameEvent?.Invoke("Ξημέρωσε! Η Ημερήσια Βάρδια ξεκινάει.");
            }
        }

        private void HandleMissedVent()
        {
            // ΕΛΕΓΧΟΣ ΑΠΟΘΕΜΑΤΟΣ ΓΙΑ ΑΣΦΑΛΕΙΑ
            if (_inventory.TryConsumeItem("Boiler Fuse"))
            {
                double pressureToVent = _boiler.SteamPressure * GameSettings.Instance.FuseBlowPressureReleasePct;
                _boiler.DeductSteam(pressureToVent);
                _boiler.RepairIntegrity(-GameSettings.Instance.FuseBlowIntegrityDamage);

                OnGameEvent?.Invoke($"ΑΣΦΑΛΕΙΑ ΕΝΕΡΓΟΠΟΙΗΘΗΚΕ: Μια βαλβίδα έσπασε! Βίαιη εκτόνωση {pressureToVent:F1} Bar στην ατμόσφαιρα. Η γεννήτρια υπέστη ζημιά!");
            }
            else
            {
                OnGameEvent?.Invoke("ΚΙΝΔΥΝΟΣ: Χάσατε την ευκαιρία για εκτόνωση και ΔΕΝ είχατε Fuses! Η πίεση εγκλωβίστηκε!");
            }
        }

        // =========================================================
        // ΤΟ ΚΟΥΜΠΙ ROUTING
        // =========================================================
        public void ExecuteRoutingPanel(double drillPct, double cityPct, double powerPct, double keepPct)
        {
            if (Clock.CurrentPhase != CyclePhase.VentRouting && Clock.CurrentPhase != CyclePhase.VentPending)
            {
                OnGameEvent?.Invoke("ΑΡΝΗΣΗ: Το σύστημα δρομολόγησης δεν είναι έτοιμο.");
                return;
            }

            double totalPct = drillPct + cityPct + powerPct + keepPct;
            if (totalPct > 0)
            {
                drillPct /= totalPct;
                cityPct /= totalPct;
                powerPct /= totalPct;
                keepPct /= totalPct;
            }

            double startingPressure = _boiler.SteamPressure;
            double steamToConsume = startingPressure * (1.0 - keepPct);

            double steamForDrill = startingPressure * drillPct;
            double steamForCity = startingPressure * cityPct;
            double steamForPower = startingPressure * powerPct;

            double coalGenerated = steamForDrill * GameSettings.Instance.SteamToCoalYield;
            double electricityGenerated = steamForPower * GameSettings.Instance.SteamToElectricityYield;

            _boiler.DeductSteam(steamToConsume);
            _jobManager.AddDrillBuff(steamForDrill);
            _logistics.AddCoal(coalGenerated);
            _logistics.AddElectricity(electricityGenerated);
            _thermo.AddSteamToCityBuffer(steamForCity);

            Clock.AdvanceAfterVent(out bool _);

            OnGameEvent?.Invoke($"ΕΚΤΟΝΩΣΗ: Αφαιρέθηκαν {steamToConsume:F1} Bar πίεσης.");
        }

        // =========================================================
        // ΤΗΛΕΜΕΤΡΙΑ
        // =========================================================
        public GameTelemetrySnapshot GetTelemetrySnapshot()
        {
            var snapshot = new GameTelemetrySnapshot();

            snapshot.Boiler = new BoilerTelemetry
            {
                SteamPressure = _boiler.SteamPressure,
                StressLevel = _boiler.StressLevel,
                CasingIntegrity = _boiler.CasingIntegrity,
                SootFraction = _boiler.SootRatio,
                IsHullCracked = _boiler.IsHullCracked,
                CurrentPhase = Clock.CurrentPhase,
                SecondsUntilNextVent = Clock.GetSecondsUntilNextVent(),
                FormattedTimeOfDay = Clock.GetFormattedTimeOfDay(),
                AvailableFuses = _inventory.GetConsumableCount("Boiler Fuse")
            };

            snapshot.Thermo = new ThermodynamicsTelemetry
            {
                CoreTemperature = _thermo.CoreTemperature,
                CityTemperature = _thermo.CityTemperature,
                TargetCityTemperature = _thermo.TargetCityTemperature,
                CitySteamBuffer = _thermo.CitySteamBuffer,
                EstimatedCityHeatHours = _thermo.CalculateSteamDurationHours(0.0, _thermo.TargetCityTemperature)
            };

            snapshot.Workforce = new WorkforceTelemetry
            {
                TotalPopulation = _population.TotalPopulation,
                HealthyWorkers = _population.HealthyWorkers,
                SickWorkers = _population.SickWorkers,
                GraveWorkers = _population.GraveWorkers,
                DeadWorkers = _population.DeadWorkers,
                UnassignedReserve = _population.UnassignedReserve,
                CurrentCityStage = _population.CurrentCityStage
            };

            snapshot.Logistics = new LogisticsTelemetry
            {
                CoalStockpile = _logistics.CoalStockpile,
                RawScrap = _logistics.RawScrap,
                RefinedMetal = _logistics.RefinedMetal,
                Electricity = _logistics.Electricity,
                CraftedMetalPatches = _inventory.GetConsumableCount("Metal Patch")
            };

            snapshot.Jobs = new List<JobTelemetry>();
            foreach (var kvp in _jobManager.Jobs)
            {
                var job = kvp.Value;

                bool isContinuous = (job.Type == JobType.StokeFurnace ||
                                     job.Type == JobType.CoalMining ||
                                     job.Type == JobType.ScrapScavenging ||
                                     job.Type == JobType.MetalSmelting ||
                                     job.Type == JobType.OperateTurbines ||
                                     job.Type == JobType.WorkshopUpkeep ||
                                     job.Type == JobType.IntensiveCare);

                double baseWork = 0.0;
                if (!isContinuous)
                {
                    baseWork = job.Type switch
                    {
                        JobType.CleanSoot => GameSettings.Instance.CleanSootWorkRequired,
                        JobType.DaytimePatch => GameSettings.Instance.PatchWorkRequired,
                        JobType.NightHullRepair => GameSettings.Instance.NightRepairWorkRequired,
                        JobType.WorkshopCraftPatch => GameSettings.Instance.CraftMetalPatchWorkRequired,
                        JobType.WorkshopCraftFuse => GameSettings.Instance.CraftFuseWorkRequired,
                        _ => 0.0
                    };
                }

                snapshot.Jobs.Add(new JobTelemetry
                {
                    Type = job.Type,
                    AssignedWorkers = job.AssignedWorkers,
                    WorkRemaining = job.WorkRemaining,
                    BaseWorkRequired = baseWork,
                    IsContinuous = isContinuous
                });
            }

            return snapshot;
        }
    }
}