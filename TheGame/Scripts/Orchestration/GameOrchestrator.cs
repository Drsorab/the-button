using Godot;
using System;
using System.Collections.Generic;
using PermafrostChieftain.Simulation;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Threading.Channels;

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
        public LogisticsSubsystem LogisticsSub { get; private set; }
        public double TimeMultiplier { get; set; } = 1.0;
        public string CurrentCraftingRecipe { get; private set; } = "";

        public event Action<string, double, int> GameOverTriggered;
        // --- ΝΕΟ: Στατιστικά τρέχοντος Run ---
        public double RunGeneratorUptime { get; private set; } = 0.0;
        public int RunWorkersLost { get; private set; } = 0;
        public bool IsGameOver { get; private set; } = false;

        // Απλοποιημένη λογική logistics (Αποθέματα και Κίνδυνος Μηχανουργείου)
        public LogisticsTelemetry Logistics;

        // Γεγονότα (Events) για το UI
        public event Action TelemetryUpdated;
        public event Action<int> PhaseTransitionTriggered;
        public event Action<double, string, string, string> LogEventDispatched;


        // Snapshots Τηλεμετρίας για το UIOrchestrator
        public BoilerTelemetry LatestBoilerTelemetry;
        public ThermodynamicsTelemetry LatestThermodynamicTelemetry;
        public WorkforceTelemetry LatestWorkforceTelemetry;
        public List<JobTelemetry> LatestJobsTelemetry = new List<JobTelemetry>();
        public LogisticsTelemetry LatestLogisticsTelemetry;

        // Γεγονός για τη δημιουργία νέας αναφοράς βάρδιας
        public event Action<string, string> ShiftReportGenerated;

        // Στιγμιότυπα (Snapshots) για τον υπολογισμό της διαφοράς στη βάρδια
        private double _shiftStartCoal;
        private double _shiftStartScrap;
        private int _shiftStartSick;

        public InventorySubsystem Inventory { get; private set; }
        public bool IsGeneratorActive { get; private set; } = false;
        private double _autoSaveTimer = 0.0;
        private const double AutoSaveInterval = 10.0;
        public override void _EnterTree()
        {
            if (Clock == null) InitializeSimulation();
        }

        public void InitializeSimulation()
        {
            GameSettings.Load();
            PlayerStats.Load();

            PlayerStats.Instance.TotalRunsPlayed++;
            PlayerStats.Instance.Save();

            Clock = new SimulationClock();
            Clock.VentGracePeriodExpired += ExecuteForcedVent;
            Clock.VentModalTimeoutExpired += ExecuteForcedVent;

            JobManager = new JobManager();
            Thermodynamics = new ThermodynamicsSubsystem();
            Boiler = new BoilerSubsystem();
            Population = new PopulationSubsystem(JobManager);
            Inventory = new InventorySubsystem();
            LogisticsSub = new LogisticsSubsystem();

            Population.OnCasualtyOccurred += (msg) =>
            {
                RunWorkersLost++;
                PlayerStats.Instance.LifetimeWorkersLost++; // Προσθήκη στα Lifetime
                PlayerStats.Instance.Save(); // Κάνουμε άμεσο Save αφού ο θάνατος είναι σημαντικό γεγονός
                DispatchLog("CRITICAL", "MEDICAL", msg);
            };

            // Αρχικά Αποθέματα
            Logistics.CoalStockpile = GameSettings.Instance.StartingCoal;
            Logistics.ScrapStockpile = GameSettings.Instance.StartingScrap;
            Logistics.Electricity = GameSettings.Instance.StartingElectricity;
            Logistics.WorkshopHazardLevel = 0.0;

            // Αρχικοποίηση των Snapshots
            _shiftStartCoal = Logistics.CoalStockpile;
            _shiftStartScrap = Logistics.ScrapStockpile;
            _shiftStartSick = Population.SickWorkers;

            Boiler.SetCasingStress(GameSettings.Instance.StartingHullIntegrity);

            // Σύνδεση γεγονότων του Ρολογιού και των Εργασιών
            Clock.VentGracePeriodExpired += OnVentGracePeriodExpired;
            Clock.VentModalTimeoutExpired += OnVentModalTimeoutExpired;
            JobManager.OnJobCompleted += HandleJobCompleted;
            Population.OnCasualtyOccurred += (msg) => DispatchLog("CRITICAL", "MEDICAL", msg);
        }

        // Καλείται από το UI όταν ο παίκτης πατάει το κουμπί "IGNITE GENERATOR"
        public bool StartBoilerSequence()
        {
            if (IsGeneratorActive) return false;

            if (Logistics.CoalStockpile >= GameSettings.Instance.IgnitionCoalCost)
            {
                Logistics.CoalStockpile -= GameSettings.Instance.IgnitionCoalCost;
                IsGeneratorActive = true;

                // Δίνουμε μια αρχική θερμοκρασία για να μην σβήσει αμέσως στο επόμενο καρέ
                Thermodynamics.SetCoreTemperature(GameSettings.Instance.StartingCoreTemp);

                DispatchLog("INFO", "CORE", "ΑΝΑΦΛΕΞΗ: Ο κεντρικός λέβητας τέθηκε σε λειτουργία! Η παραγωγή θερμότητας ξεκίνησε.");
                return true;
            }
            else
            {
                DispatchLog("WARNING", "CORE", $"Αποτυχία ανάφλεξης: Απαιτούνται {GameSettings.Instance.IgnitionCoalCost}kg άνθρακα!");
                return false;
            }
        }

        // Καλείται αυτόματα αν ο παίκτης παραμελήσει τον λέβητα
        private void TriggerBoilerShutdown()
        {
            IsGeneratorActive = false;
            DispatchLog("CRITICAL", "CORE", "Ο ΛΕΒΗΤΑΣ ΕΣΒΗΣΕ! Η θερμοκρασία του πυρήνα έπεσε κάτω από το όριο συντήρησης.");
        }

        public override void _Process(double delta)
        {
            if (Clock == null) return;
            _autoSaveTimer += delta;
            if (_autoSaveTimer >= AutoSaveInterval)
            {
                PlayerStats.Instance.Save();
                _autoSaveTimer = 0.0;
            }
            delta *= TimeMultiplier;

            bool phaseChanged = false;

            if (Clock.Tick(delta, out phaseChanged))
            {
                // Αν ξημερώσει (DayActive) σημαίνει ότι τελείωσε η Νύχτα. 
                // Αν μπει στο VentPending/Routing, σημαίνει ότι τελείωσε η Μέρα.
                if (Clock.CurrentPhase == CyclePhase.DayActive)
                {
                    GenerateShiftReport(CyclePhase.NightBleed);
                }
                else if (Clock.CurrentPhase == CyclePhase.VentPending || Clock.CurrentPhase == CyclePhase.VentRouting)
                {
                    GenerateShiftReport(CyclePhase.DayActive);
                }

                PhaseTransitionTriggered?.Invoke((int)Clock.CurrentPhase);

                JobManager.ApplyEquipmentBuffs(JobType.ScourFlues, Inventory);
                JobManager.ApplyEquipmentBuffs(JobType.DeepScour, Inventory);
            }

            if (Clock.CurrentPhase != CyclePhase.VentRouting)
            {
                if (IsGeneratorActive)
                {
                    RunGeneratorUptime += delta;
                    PlayerStats.Instance.LifetimeGeneratorUptime += delta;
                    // --- ΚΑΤΑΝΑΛΩΣΗ ΑΝΘΡΑΚΑ & ΘΕΡΜΟΔΥΝΑΜΙΚΗ ---
                    int stokeWorkers = JobManager.Jobs[JobType.StokeFurnace].AssignedWorkers;
                    double baseCoalRate = GameSettings.Instance.StokeCoalPerWorkerPerHour;

                    var equippedShovel = Inventory.GetEquippedItem("Reinforced Shovels");
                    if (equippedShovel != null && stokeWorkers > 0)
                    {
                        baseCoalRate *= GameSettings.Instance.ShovelCoalBonusMultiplier;
                        double heatStress = Math.Max(0, Thermodynamics.CoreTemperature - GameSettings.Instance.OptimalCoreTemp);
                        double wearDamage = (GameSettings.Instance.ShovelBaseDegradationPerSec + (heatStress * GameSettings.Instance.ShovelHeatDegradationMultiplier)) * delta;
                        Inventory.DegradeEquippedItem("Reinforced Shovels", wearDamage);
                    }

                    double coalPerHour = stokeWorkers * baseCoalRate;
                    double coalBurnRatePerSec = coalPerHour / 3600.0;

                    Logistics.CoalStockpile = Math.Max(0, Logistics.CoalStockpile - (coalBurnRatePerSec * delta));

                    double newSoot = Thermodynamics.ProcessThermalTick(delta, coalBurnRatePerSec);
                    if (newSoot > 0)
                    {
                        Boiler.AddSoot(newSoot);
                    }

                    // ΕΛΕΓΧΟΣ: Μήπως πάγωσε ο πυρήνας και πρέπει να σβήσει;
                    if (Thermodynamics.CoreTemperature <= GameSettings.Instance.ShutdownTempThreshold)
                    {
                        TriggerBoilerShutdown();
                    }
                }
                else
                {
                    // Παθητική ψύξη πυρήνα όταν η γεννήτρια είναι σβηστή
                    Thermodynamics.SetCoreTemperature(Thermodynamics.CoreTemperature - (GameSettings.Instance.InactiveCoolingRate * delta));
                }

                // --- ΚΟΙΝΗ ΦΥΣΙΚΗ ΠΟΥ ΤΡΕΧΕΙ ΠΑΝΤΑ ---
                // Ενημερώνουμε την Πίεση και την Πόλη ανεξάρτητα από το αν η γεννήτρια είναι on ή off
                Boiler.ProcessTick(delta, Thermodynamics.CoreTemperature, Clock.CurrentPhase);
                Thermodynamics.ProcessCityTemperature(delta, IsGeneratorActive, Boiler.SootFraction);

                if (Thermodynamics.CityTemperature <= GameSettings.Instance.CityFreezeThreshold)
                {
                    TriggerGameOver($"Η ΠΟΛΗ ΠΑΓΩΣΕ. Η θερμοκρασία έπεσε στους {Thermodynamics.CityTemperature:F1}°C.");
                }

                // --- ΣΥΓΧΡΟΝΙΣΜΟΣ ΠΡΟΟΔΟΥ ΚΑΤΑΣΚΕΥΗΣ (Shared Progress) ---
                if (!string.IsNullOrEmpty(JobManager.ActiveCraftingRecipe))
                {
                    if (Clock.CurrentPhase == CyclePhase.DayActive || Clock.CurrentPhase == CyclePhase.TwilightWindDown)
                    {
                        double dayProgress = JobManager.Jobs[JobType.WorkshopMachining].WorkRemaining;
                        JobManager.Jobs[JobType.ManualTinkering].SetWorkRemaining(dayProgress);
                    }
                    else if (Clock.CurrentPhase == CyclePhase.NightBleed)
                    {
                        double nightProgress = JobManager.Jobs[JobType.ManualTinkering].WorkRemaining;
                        JobManager.Jobs[JobType.WorkshopMachining].SetWorkRemaining(nightProgress);
                    }
                }

                if (Clock.CurrentPhase == CyclePhase.NightBleed && Logistics.Electricity > 0)
                {
                    double actualDrain = Math.Min(Logistics.Electricity, GameSettings.Instance.NightElectricityDrainRate * delta);
                    Logistics.Electricity -= actualDrain;

                    // Προσθέτει ρεύμα στο Buffer, το οποίο αφομοιώνεται ομαλά στην πόλη μέσω της ProcessCityTemperature
                    Thermodynamics.AddCityHeatBuffer(actualDrain * GameSettings.Instance.NightHeatConversionEfficiency);

                    var equippedBattery = Inventory.GetEquippedItem("Capacitor Batteries");
                    if (equippedBattery != null)
                    {
                        double batteryDamagePerSec = GameSettings.Instance.BatteryDegradationPerCycle / GameSettings.Instance.NightDuration;
                        Inventory.DegradeEquippedItem("Capacitor Batteries", batteryDamagePerSec * delta);
                    }
                }

                JobManager.Tick(delta, Inventory);
                Population.ProcessTick(delta, Boiler, Thermodynamics, Logistics.WorkshopHazardLevel);
            }

            // Περιορισμός του Ρεύματος (Relay) βάσει Μπαταριών
            Logistics.Electricity = Math.Clamp(Logistics.Electricity, 0.0, LogisticsSub.GetMaxElectricity(Inventory));
            UpdateTelemetrySnapshots();
        }

        public void TriggerEmergencyBlowout()
        {
            if (Boiler.SteamPressure >= GameSettings.Instance.BlowoutPressureCost)
            {
                Boiler.DeductPressure(GameSettings.Instance.BlowoutPressureCost);
                Boiler.RemoveSoot(GameSettings.Instance.BlowoutSootCleared);
                Boiler.DamageCasing(GameSettings.Instance.BlowoutHullDamage);

                DispatchLog("CRITICAL", "BOILER", "ΕΚΤΑΚΤΗ ΕΚΤΟΝΩΣΗ: Οι σωληνώσεις καθαρίστηκαν βίαια. Σοβαρή ζημιά στο κέλυφος!");
            }
            else
            {
                DispatchLog("WARNING", "BOILER", $"Αποτυχία Blowout: Απαιτούνται {GameSettings.Instance.BlowoutPressureCost} Bar πίεσης.");
            }
        }

        // --- 1. Διαχείριση Εκτόνωσης (Vent Routing) ---

        // Καλείται από το Modal του UI όταν ο παίκτης κατανέμει την πίεση (108 λεπτά)
        public void ExecuteVentRouting(double cityHeat, double workshopPower, double drillPower, double atmosphereVent)
        {
            double totalVented = cityHeat + workshopPower + drillPower + atmosphereVent;
            Boiler.DeductPressure(totalVented);

            Thermodynamics.AddCityHeatBuffer(cityHeat);
            Logistics.Electricity += workshopPower * GameSettings.Instance.VentElectricityMultiplier;
            Logistics.CoalStockpile += drillPower * GameSettings.Instance.VentCoalMultiplier;
            Logistics.ScrapStockpile += drillPower * GameSettings.Instance.VentScrapMultiplier;

            double dirtyVentAmount = cityHeat + drillPower;
            if (dirtyVentAmount > 0)
            {
                double addedSoot = dirtyVentAmount * GameSettings.Instance.VentRoutingSootPenalty;
                Boiler.AddSoot(addedSoot);
                DispatchLog("WARNING", "VENT", $"Η εκτόνωση προκάλεσε συσσώρευση αιθάλης (+{addedSoot * 100.0:F1}%).");
            }

            DispatchLog("INFO", "ROUTING", $"Εκτόνωση: Πόλη={cityHeat} | Ρεύμα={workshopPower} | Drill={drillPower} | Ατμόσφαιρα={atmosphereVent}");

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
                TriggerGameOver("Ο ΛΕΒΗΤΑΣ ΑΝΑΤΙΝΑΧΤΗΚΕ ΛΟΓΩ ΥΠΕΡΠΙΕΣΗΣ.");
                Clock.AdvanceAfterVent(out bool phaseChanged);
                if (phaseChanged) PhaseTransitionTriggered?.Invoke((int)Clock.CurrentPhase);
            }
        }

        public void ExecuteForcedVent()
        {
            double totalPressure = Boiler.SteamPressure;
            Boiler.DeductPressure(totalPressure);

            var equippedFuse = Inventory.GetEquippedItem("Pressure Fuses");
            if (equippedFuse != null)
            {
                Inventory.DegradeEquippedItem("Pressure Fuses", 100.0);
                DispatchLog("CRITICAL", "VENT", "Βίαιη Εκτόνωση! Μια ασφάλεια πίεσης (Pressure Fuse) έσκασε και έσωσε το κέλυφος!");
            }
            else
            {
                Boiler.DamageCasing(GameSettings.Instance.CatastrophicHullDamage);
                DispatchLog("CRITICAL", "VENT", $"Βίαιη Εκτόνωση! Το κέλυφος υπέστη {GameSettings.Instance.CatastrophicHullDamage} ζημιά.");
            }

            Clock.AdvanceAfterVent(out bool phaseChanged);
            if (phaseChanged)
            {
                PhaseTransitionTriggered?.Invoke((int)Clock.CurrentPhase);
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

                    // ΕΠΑΝΑΦΟΡΑ: Συνεχόμενο Loop
                    JobManager.Jobs[type].SetWorkRemaining(GameSettings.Instance.JobConfigs[type].BaseWork);
                    break;

                case JobType.ScourFlues:
                case JobType.DeepScour:
                    double sootCleaned = (type == JobType.ScourFlues) ? GameSettings.Instance.DaySootCleanAmount : GameSettings.Instance.NightSootCleanAmount;
                    double coalReward = GameSettings.Instance.BaseCoalSootReward;

                    var equippedBrush = Inventory.GetEquippedItem("Wire Brushes");
                    if (equippedBrush != null)
                    {
                        coalReward *= GameSettings.Instance.BrushYieldMultiplier;
                        double damageToBrush = GameSettings.Instance.BrushDegradationPerSec * GameSettings.Instance.JobConfigs[type].BaseWork;
                        Inventory.DegradeEquippedItem("Wire Brushes", damageToBrush);
                    }

                    Boiler.RemoveSoot(sootCleaned);
                    Logistics.CoalStockpile += coalReward;

                    string scrubTime = (type == JobType.ScourFlues) ? "Ημέρα" : "Νύχτα";
                    DispatchLog("INFO", "JOBS", $"Καθαρισμός ({scrubTime}): Αφαιρέθηκε το {sootCleaned * 100.0:F0}% της αιθάλης. Βρέθηκαν {coalReward:F0}kg άνθρακα.");

                    // ΕΠΑΝΑΦΟΡΑ: Συνεχόμενο Loop
                    JobManager.Jobs[type].SetWorkRemaining(GameSettings.Instance.JobConfigs[type].BaseWork);
                    break;

                case JobType.WorkshopMaintenance:
                    double scrapYield = JobManager.CalculateWorkshopMaintenanceReward();
                    Logistics.ScrapStockpile += scrapYield;
                    Logistics.WorkshopHazardLevel = 0.0;

                    foreach (var item in Inventory.Items)
                    {
                        if (item.IsEquipped && item.Durability < 100.0)
                        {
                            item.Durability = Math.Min(100.0, item.Durability + GameSettings.Instance.NightShiftRepairRate);
                        }
                    }

                    DispatchLog("INFO", "JOBS", $"Το μηχανουργείο συντηρήθηκε. Ανακτήθηκαν {scrapYield:F0} Scrap. Εξοπλισμός επισκευάστηκε ελαφρώς.");

                    // ΕΠΑΝΑΦΟΡΑ: Συνεχόμενο Loop
                    JobManager.Jobs[type].SetWorkRemaining(GameSettings.Instance.JobConfigs[type].BaseWork);
                    break;

                case JobType.DamperThrottling:
                    Logistics.ScrapStockpile += 2.0;
                    DispatchLog("INFO", "JOBS", "Οι ρυθμιστές συντηρήθηκαν. Ανακτήθηκαν υλικά καθαρισμού (+2 Scrap).");

                    // ΕΠΑΝΑΦΟΡΑ: Συνεχόμενο Loop
                    JobManager.Jobs[type].SetWorkRemaining(GameSettings.Instance.JobConfigs[type].BaseWork);
                    break;

                case JobType.IntensiveCare:
                    Population.ProcessOvernightHealing(true);
                    DispatchLog("INFO", "JOBS", "Ολοκληρώθηκε κύκλος εντατικής θεραπείας. Κάποιοι ασθενείς αναρρώνουν.");

                    // ΕΠΑΝΑΦΟΡΑ: Συνεχόμενο Loop θεραπείας
                    JobManager.Jobs[type].SetWorkRemaining(GameSettings.Instance.JobConfigs[type].BaseWork);
                    break;

                case JobType.MicroBleed:
                    // Υπολογίζουμε πόση πίεση θέλουμε να διώξουμε
                    double intendedDeduct = GameSettings.Instance.MicroBleedPressureDeduct;

                    // Ελέγχουμε πόση πίεση ΥΠΑΡΧΕΙ πραγματικά στον λέβητα
                    double actualDeduct = Math.Min(Boiler.SteamPressure, intendedDeduct);

                    // Αφαιρούμε την πραγματική πίεση
                    Boiler.DeductPressure(actualDeduct);

                    if (JobManager.ActiveMicroBleedTarget == MicroBleedTarget.Atmosphere)
                    {
                        DispatchLog("INFO", "JOBS", $"Η μικρο-εκτόνωση ολοκληρώθηκε (-{actualDeduct:F1} Bar στην Ατμόσφαιρα).");
                    }
                    else if (JobManager.ActiveMicroBleedTarget == MicroBleedTarget.CityHeat)
                    {
                        // Η θερμότητα είναι άμεσα συνδεδεμένη με τον ατμό που εκτονώθηκε
                        double addedHeat = actualDeduct * GameSettings.Instance.PressureToHeatConversionRate;
                        Thermodynamics.AddCityHeatBuffer(addedHeat);

                        DispatchLog("INFO", "JOBS", $"Η μικρο-εκτόνωση ολοκληρώθηκε (-{actualDeduct:F1} Bar -> +{addedHeat:F1}°C στο Buffer της Πόλης).");
                    }
                    else if (JobManager.ActiveMicroBleedTarget == MicroBleedTarget.SubterraneanDrill)
                    {
                        Logistics.CoalStockpile += GameSettings.Instance.MicroBleedDrillCoalAdd;
                        DispatchLog("INFO", "JOBS", $"Η μικρο-εκτόνωση ολοκληρώθηκε (-{actualDeduct:F1} Bar -> +20kg Άνθρακας από το Drill).");
                    }
                    else if (JobManager.ActiveMicroBleedTarget == MicroBleedTarget.WorkshopPower)
                    {
                        Logistics.Electricity += GameSettings.Instance.MicroBleedWorkshopElecAdd;
                        DispatchLog("INFO", "JOBS", $"Η μικρο-εκτόνωση ολοκληρώθηκε (-{actualDeduct:F1} Bar -> +10 kW Ρεύμα).");
                    }

                    // ΕΠΑΝΑΦΟΡΑ: Συνεχόμενο Loop
                    JobManager.Jobs[type].SetWorkRemaining(GameSettings.Instance.JobConfigs[type].BaseWork);
                    break;

                case JobType.WorkshopMachining:
                case JobType.ManualTinkering:
                    if (!string.IsNullOrEmpty(JobManager.ActiveCraftingRecipe))
                    {
                        var recipe = GameSettings.Instance.CraftingRecipes[JobManager.ActiveCraftingRecipe];

                        double elecCost = (type == JobType.WorkshopMachining) ? recipe.ElectricityCost : 0.0;
                        double baseHazardIncrease = (type == JobType.WorkshopMachining) ? 0.1 : 0.05;

                        var equippedRespirator = Inventory.GetEquippedItem("Pneumatic Respirators");
                        double finalHazardIncrease = baseHazardIncrease;

                        if (equippedRespirator != null)
                        {
                            if (equippedRespirator.Durability <= GameSettings.Instance.RespiratorDangerThreshold)
                            {
                                finalHazardIncrease *= GameSettings.Instance.RespiratorBrokenHazardPenalty;
                            }
                            else
                            {
                                finalHazardIncrease *= GameSettings.Instance.RespiratorHazardReduction;
                            }

                            Inventory.DegradeEquippedItem("Pneumatic Respirators", GameSettings.Instance.RespiratorClogRatePerSec * recipe.BaseWorkTime);
                        }

                        if (Logistics.Electricity >= elecCost && Logistics.ScrapStockpile >= recipe.ScrapCost)
                        {
                            Logistics.Electricity -= elecCost;
                            Logistics.ScrapStockpile -= recipe.ScrapCost;
                            Logistics.WorkshopHazardLevel = Math.Min(1.0, Logistics.WorkshopHazardLevel + finalHazardIncrease);

                            Inventory.AddItem(JobManager.ActiveCraftingRecipe);

                            string shiftName = type == JobType.WorkshopMachining ? "Μηχανουργείο (Ημέρα)" : "Μηχανουργείο (Νύχτα)";
                            DispatchLog("INFO", "JOBS", $"{shiftName}: Κατασκευάστηκε [{JobManager.ActiveCraftingRecipe}]. (Αποθηκεύτηκε στο Inventory)");

                            JobManager.SetActiveCraftingRecipe("");
                            JobManager.Jobs[JobType.WorkshopMachining].SetWorkRemaining(0);
                            JobManager.Jobs[JobType.ManualTinkering].SetWorkRemaining(0);
                        }
                        else
                        {
                            DispatchLog("WARNING", "JOBS", $"Παύση Κατασκευής ({JobManager.ActiveCraftingRecipe}): Ανεπαρκή υλικά ή ρεύμα!");
                            double currentWork = JobManager.Jobs[type].WorkRemaining;
                            JobManager.Jobs[type].SetWorkRemaining(currentWork + 1.0); // Μικρό penalty/delay αν λείπουν υλικά
                        }
                    }
                    break;
            }
        }


        // Καλείται από το νέο UI (Shop) για να βάλει μια συνταγή στην ουρά
        public bool QueueCraftingRecipe(string recipeName)
        {
            if (!GameSettings.Instance.CraftingRecipes.TryGetValue(recipeName, out var recipe)) return false;

            JobManager.SetActiveCraftingRecipe(recipeName);
            JobManager.Jobs[JobType.WorkshopMachining].SetWorkRemaining(recipe.BaseWorkTime);
            JobManager.Jobs[JobType.ManualTinkering].SetWorkRemaining(recipe.BaseWorkTime);

            DispatchLog("INFO", "WORKSHOP", $"Η γραμμή παραγωγής άλλαξε. Νέα κατασκευή: {recipeName}.");
            return true;
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

        private void GenerateShiftReport(CyclePhase endedPhase)
        {
            string shiftName = (endedPhase == CyclePhase.DayActive || endedPhase == CyclePhase.TwilightWindDown) ? "Day Shift" : "Night Shift";
            string timeStamp = Clock.GetFormattedTimeOfDay();

            double coalDiff = Logistics.CoalStockpile - _shiftStartCoal;
            double scrapDiff = Logistics.ScrapStockpile - _shiftStartScrap;
            int sickDiff = Population.SickWorkers - _shiftStartSick;

            string reportTitle = $"[{timeStamp}] {shiftName} Report";

            string reportContent = $"[b]{shiftName} Summary[/b]\n";
            reportContent += $"Time of Generation: {timeStamp}\n\n";

            reportContent += "[b]Resource Yields:[/b]\n";
            reportContent += $"• Coal: {(coalDiff >= 0 ? "+" : "")}{coalDiff:F1} kg\n";
            reportContent += $"• Scrap: {(scrapDiff >= 0 ? "+" : "")}{scrapDiff:F1}\n\n";

            reportContent += "[b]Medical Status:[/b]\n";
            if (sickDiff > 0) reportContent += $"• WARNING: {sickDiff} workers fell sick.\n";
            else if (sickDiff < 0) reportContent += $"• SUCCESS: {Math.Abs(sickDiff)} workers recovered.\n";
            else reportContent += "• No changes in medical status.\n\n";

            reportContent += "[b]Boiler Status:[/b]\n";
            reportContent += $"• Pressure: {Boiler.SteamPressure:F1} Bar\n";
            reportContent += $"• Core Temp: {Thermodynamics.CoreTemperature:F0}°C\n";
            reportContent += $"• Soot Accumulation: {Boiler.SootFraction * 100.0:F1}%\n";

            ShiftReportGenerated?.Invoke(reportTitle, reportContent);

            _shiftStartCoal = Logistics.CoalStockpile;
            _shiftStartScrap = Logistics.ScrapStockpile;
            _shiftStartSick = Population.SickWorkers;
        }

        public void TriggerGameOver(string reason)
        {
            if (IsGameOver) return; // Για να μην κληθεί πολλές φορές

            IsGameOver = true;
            TimeMultiplier = 0.0; // Σταματάει τον χρόνο του παιχνιδιού

            DispatchLog("CRITICAL", "SYSTEM", $"GAME OVER: {reason}");

            PlayerStats.Instance.Save();

            // Ειδοποιούμε το UI να εμφανίσει την οθόνη
            GameOverTriggered?.Invoke(reason, RunGeneratorUptime, RunWorkersLost);
        }
        public override void _ExitTree()
        {
            // Κάνουμε ένα τελευταίο, ασφαλές Save πριν τερματίσει το παιχνίδι
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.Save();
            }
        }

    }
}