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
        public LogisticsSubsystem LogisticsSub { get; private set; }
        public double TimeMultiplier { get; set; } = 1.0;
        public string CurrentCraftingRecipe { get; private set; } = "";
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

        public override void _EnterTree()
        {
            if (Clock == null) InitializeSimulation();
        }

        public void InitializeSimulation()
        {
            GameSettings.Load();

            Clock = new SimulationClock();

            Clock.VentGracePeriodExpired += ExecuteForcedVent;
            Clock.VentModalTimeoutExpired += ExecuteForcedVent;

            JobManager = new JobManager();
            Thermodynamics = new ThermodynamicsSubsystem();
            Boiler = new BoilerSubsystem();
            Population = new PopulationSubsystem(JobManager);
            Inventory = new InventorySubsystem();
            LogisticsSub = new LogisticsSubsystem();
            // Αρχικά Αποθέματα
            Logistics.CoalStockpile = GameSettings.Instance.StartingCoal;
            Logistics.ScrapStockpile = GameSettings.Instance.StartingScrap;
            Logistics.Electricity = GameSettings.Instance.StartingElectricity;
            Logistics.WorkshopHazardLevel = 0.0;

            // ΝΕΟ: Αρχικοποίηση των Snapshots
            _shiftStartCoal = Logistics.CoalStockpile;
            _shiftStartScrap = Logistics.ScrapStockpile;
            _shiftStartSick = Population.SickWorkers;

            Boiler.SetCasingStress (GameSettings.Instance.StartingHullIntegrity);
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
                Thermodynamics.SetCoreTemperature(GameSettings.Instance.ShutdownTempThreshold + 50.0);

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
                // Αν η γεννήτρια δουλεύει κανονικά
                if (IsGeneratorActive)
                {
                    Boiler.ProcessTick(delta, Thermodynamics.CoreTemperature, Clock.CurrentPhase);

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
                    // Η ΓΕΝΝΗΤΡΙΑ ΕΙΝΑΙ ΣΒΗΣΤΗ: Ραγδαία πτώση θερμοκρασιών και πίεσης

                    // Διαβάζουμε την τρέχουσα εξωτερική θερμοκρασία από τη Θερμοδυναμική
                    double ambientTemp = Thermodynamics.CurrentAmbientTemperature;

                    // Ο πυρήνας παγώνει
                    Thermodynamics.SetCoreTemperature(Thermodynamics.CoreTemperature - (GameSettings.Instance.InactiveCoolingRate * delta));

                    // Η πόλη χάνει τη ζέστη της δραματικά
                    Thermodynamics.SetCityTemperature(Thermodynamics.CityTemperature - ((GameSettings.Instance.InactiveCoolingRate * 0.5) * delta));

                    // Η πίεση χάνεται σιγά σιγά επειδή δεν υπάρχει βρασμός
                    //Boiler.DeductPressure(Boiler.SteamPressure * 0.05 * delta);
                    Boiler.ProcessTick(delta, Thermodynamics.CoreTemperature, Clock.CurrentPhase);
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
                // Καταναλώνει την πίεση (Πάει χαμένη στην ατμόσφαιρα βίαια)
                Boiler.DeductPressure(GameSettings.Instance.BlowoutPressureCost);

                // Καθαρίζει ένα μεγάλο ποσοστό καπνιάς
                Boiler.RemoveSoot(GameSettings.Instance.BlowoutSootCleared);

                // Τιμωρία: Τεράστια ζημιά στο κέλυφος (Hull Damage)
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
            // Αφαιρούμε ΜΟΝΟ όση πίεση επιλέχθηκε να φύγει από τον λέβητα. 
            // Το υπόλοιπο παραμένει αυτόματα (Retain).
            double totalVented = cityHeat + workshopPower + drillPower + atmosphereVent;
            Boiler.DeductPressure(totalVented);

            Thermodynamics.AddCityHeatBuffer(cityHeat);
            Logistics.Electricity += workshopPower * GameSettings.Instance.VentElectricityMultiplier;
            Logistics.CoalStockpile += drillPower * GameSettings.Instance.VentCoalMultiplier;
            Logistics.ScrapStockpile += drillPower * GameSettings.Instance.VentScrapMultiplier;

            // Η εκτόνωση στην πόλη και το τρυπάνι δημιουργεί καπνιά
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
                Clock.AdvanceAfterVent(out bool phaseChanged);
                if (phaseChanged) PhaseTransitionTriggered?.Invoke((int)Clock.CurrentPhase);
            }
        }

        public void ExecuteForcedVent()
        {
            double totalPressure = Boiler.SteamPressure;
            Boiler.DeductPressure(totalPressure);

            // Έλεγχος Εξοπλισμού: Ασφάλειες Πίεσης (Pressure Fuses)
            var equippedFuse = Inventory.GetEquippedItem("Pressure Fuses");
            if (equippedFuse != null)
            {
                // Η ασφάλεια σώζει το κέλυφος αλλά καταστρέφεται (100% ζημιά)
                Inventory.DegradeEquippedItem("Pressure Fuses", 100.0);
                DispatchLog("CRITICAL", "VENT", "Βίαιη Εκτόνωση! Μια ασφάλεια πίεσης (Pressure Fuse) έσκασε και έσωσε το κέλυφος!");
            }
            else
            {
                // Εφαρμογή της ποινής στο κέλυφος κανονικά
                Boiler.DamageCasing(GameSettings.Instance.ViolentDischargeCasingDamage);
                DispatchLog("CRITICAL", "VENT", $"Βίαιη Εκτόνωση! Το κέλυφος υπέστη {GameSettings.Instance.ViolentDischargeCasingDamage} ζημιά.");
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
                    break;

                case JobType.ScourFlues:
                case JobType.DeepScour:
                    double sootCleaned = (type == JobType.ScourFlues) ? GameSettings.Instance.DaySootCleanAmount : GameSettings.Instance.NightSootCleanAmount;

                    // Το βασικό coal reward μπορεί να κλιμακώνεται με τις μέρες, αλλά εδώ κρατάμε τη βάση 
                    // συν το multiplier αν έχει βούρτσα.
                    double coalReward = GameSettings.Instance.BaseCoalSootReward;

                    // Έλεγχος Εξοπλισμού: Βούρτσες (Wire Brushes)
                    var equippedBrush = Inventory.GetEquippedItem("Wire Brushes");
                    if (equippedBrush != null)
                    {
                        // Διπλασιασμός ανταμοιβής σε κάρβουνο!
                        coalReward *= GameSettings.Instance.BrushYieldMultiplier;

                        // Η βούρτσα λιώνει. Αντί να τραβάμε το baseWork, βάζουμε μια σταθερή ζημιά (π.χ. 33% ανά χρήση) 
                        // για να μην εξαρτάται από τον χρόνο που έκαναν οι εργάτες να τελειώσουν.
                        // (Το έχουμε στο Settings: BrushDegradationPerSec. Αν το έχεις βάλει 1.0, μπορούμε να το πολλαπλασιάσουμε με το BaseWork)
                        double damageToBrush = GameSettings.Instance.BrushDegradationPerSec * GameSettings.Instance.JobConfigs[type].BaseWork;
                        Inventory.DegradeEquippedItem("Wire Brushes", damageToBrush);
                    }

                    Boiler.RemoveSoot(sootCleaned);
                    Logistics.CoalStockpile += coalReward;

                    string scrubTime = (type == JobType.ScourFlues) ? "Ημέρα" : "Νύχτα";
                    DispatchLog("INFO", "JOBS", $"Καθαρισμός ({scrubTime}): Αφαιρέθηκε το {sootCleaned * 100.0:F0}% της αιθάλης. Βρέθηκαν {coalReward:F0}kg άνθρακα.");
                    break;

                case JobType.WorkshopMaintenance:
                    double scrapYield = JobManager.CalculateWorkshopMaintenanceReward();
                    Logistics.ScrapStockpile += scrapYield;
                    Logistics.WorkshopHazardLevel = 0.0; // Μηδενίζει τον κίνδυνο ατυχημάτων

                    foreach (var item in Inventory.Items)
                    {
                        if (item.IsEquipped && item.Durability < 100.0)
                        {
                            item.Durability = Math.Min(100.0, item.Durability + GameSettings.Instance.NightShiftRepairRate);
                        }
                    }

                    DispatchLog("INFO", "JOBS", $"Το μηχανουργείο συντηρήθηκε. Ανακτήθηκαν {scrapYield:F0} Scrap. Εξοπλισμός επισκευάστηκε ελαφρώς.");
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
                    if (!string.IsNullOrEmpty(JobManager.ActiveCraftingRecipe))
                    {
                        var recipe = GameSettings.Instance.CraftingRecipes[JobManager.ActiveCraftingRecipe];

                        double elecCost = (type == JobType.WorkshopMachining) ? recipe.ElectricityCost : 0.0;
                        double baseHazardIncrease = (type == JobType.WorkshopMachining) ? 0.1 : 0.05;

                        // Έλεγχος Εξοπλισμού: Αναπνευστήρες (Pneumatic Respirators)
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
                            JobManager.Jobs[type].SetWorkRemaining(currentWork + 1.0);
                        }
                    }
                    break;
            }
        }


        // Καλείται από το νέο UI (Shop) για να βάλει μια συνταγή στην ουρά
        public bool QueueCraftingRecipe(string recipeName)
        {
            if (!GameSettings.Instance.CraftingRecipes.TryGetValue(recipeName, out var recipe)) return false;

            // Αφαιρέσαμε τον έλεγχο (!string.IsNullOrEmpty) που υπήρχε εδώ, 
            // για να μπορεί ο παίκτης να αλλάξει συνταγή στη μέση της κατασκευής!

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

        private void GenerateShiftReport(CyclePhase endedPhase)
        {
            string shiftName = (endedPhase == CyclePhase.DayActive || endedPhase == CyclePhase.TwilightWindDown) ? "Day Shift" : "Night Shift";
            string timeStamp = Clock.GetFormattedTimeOfDay();

            double coalDiff = Logistics.CoalStockpile - _shiftStartCoal;
            double scrapDiff = Logistics.ScrapStockpile - _shiftStartScrap;
            int sickDiff = Population.SickWorkers - _shiftStartSick;

            string reportTitle = $"[{timeStamp}] {shiftName} Report";

            // Χρήση Markdown-like μορφοποίησης για το RichTextLabel
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

            // Αποστολή του event στο UI
            ShiftReportGenerated?.Invoke(reportTitle, reportContent);

            // Ενημέρωση των Snapshots για την επόμενη βάρδια
            _shiftStartCoal = Logistics.CoalStockpile;
            _shiftStartScrap = Logistics.ScrapStockpile;
            _shiftStartSick = Population.SickWorkers;
        }
    }
}