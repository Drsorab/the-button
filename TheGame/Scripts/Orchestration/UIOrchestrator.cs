using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PermafrostChieftain.Simulation;

namespace PermafrostChieftain.Orchestration
{
    // Βοηθητική κλάση για την ομαδοποίηση των στοιχείων UI κάθε κάρτας εργασίας (Job Card)
    public class JobCardUI
    {
        public string BaseName;
        public Label JobNameLabel;
        public Label HazardLabel;
        public ProgressBar JobProgressBar;
        public Button MinusBtn;
        public Label StaffingLabel;
        public Button PlusBtn;
    }

    public partial class UIOrchestrator : Control
    {
        private GameOrchestrator _gameOrchestrator;

        // Κεφαλίδα και τηλεμετρία
        private Label _coalLabel, _scrapLabel, _relayLabel, _cityTempLabel;
        private Label _totalPopLabel, _healthyCountLabel, _sickCountLabel, _gravelySickCountLabel;

        private Label _pressureTitleLabel, _coreTempLabel, _casingTitleLabel, _sootTitleLabel;
        private ProgressBar _pressureGaugeBar, _casingIntegrityBar, _sootFoulingBar;
        private HBoxContainer _fuseRackContainer;

        private Label _cycleClockLabel, _shiftCountdownLabel;
        private Control _dayShiftView, _nightShiftView;
        private Button _ventActuatorButton;
        private ProgressBar _workshopHazardBar;
        private Label _unassignedReserveLabel;

        // Νέα στοιχεία: Οθόνη Εκτόνωσης (Vent Routing Modal)
        private PanelContainer _ventRoutingModal;
        private Label _routingTitleLabel;
        private HSlider _cityVentSlider, _workshopVentSlider, _drillVentSlider, _atmosphereVentSlider;
        private Label _cityVentValue, _workshopVentValue, _drillVentValue, _atmosphereVentValue, _retainVentValue;
        private Button _confirmVentButton;

        // Λεξικό για γρήγορη πρόσβαση στα UI στοιχεία των εργασιών
        private Dictionary<JobType, JobCardUI> _jobCards = new();

        private OptionButton _microBleedTargetDropdown;
        private Button _adminPanelButton;
        private PanelContainer _adminPanelModal;
        private Button _blowoutButton;

        // Στοιχεία UI για Κατασκευή (Shop)
        private Button _openShopButton;
        private PanelContainer _workshopCraftingModal;
        private VBoxContainer _recipeListVBox;
        private HBoxContainer _recipePrefab;
        private Button _closeShopBtn;

        // Στοιχεία UI για Εξοπλισμό (Inventory)
        private Button _openInventoryButton;
        private PanelContainer _inventoryModal;
        private VBoxContainer _itemListVBox;
        private HBoxContainer _itemPrefab;
        private Button _closeInventoryBtn;
        public override void _Ready()
        {
            _gameOrchestrator = GetNode<GameOrchestrator>("../");

            if (_gameOrchestrator != null && _gameOrchestrator.Clock == null)
            {
                _gameOrchestrator.InitializeSimulation();
            }

            // 1. ΠΡΩΤΑ κάνουμε την ανάκτηση των Nodes ώστε να μην είναι null.
            BindUiNodes();

            // 2. Σύνδεση των UI Timeouts με τα ρολόγια του συστήματος
            _gameOrchestrator.Clock.VentModalTimeoutExpired += () =>
            {
                // Το παράθυρο εκτόνωσης κλείνει βίαια αν λήξει ο χρόνος του Modal
                if (_ventRoutingModal != null && _ventRoutingModal.Visible)
                {
                    _ventRoutingModal.Visible = false;
                }

                GD.Print("UI: Το παράθυρο εκτόνωσης έκλεισε βίαια λόγω timeout!");
            };

            // Το κουμπί εκτόνωσης (Vent Actuator) πρέπει να κρύβεται/αλλάζει κατάσταση όταν αλλάζει η φάση 
            _gameOrchestrator.PhaseTransitionTriggered += (phaseIndex) =>
            {
                CyclePhase newPhase = (CyclePhase)phaseIndex;

                // Το κουμπί ορατότητα και κατάσταση ενεργοποίησης ελέγχεται κυρίως από τη φάση
                if (_ventActuatorButton != null)
                {
                    _ventActuatorButton.Visible = (newPhase == CyclePhase.VentPending || newPhase == CyclePhase.VentRouting || newPhase == CyclePhase.DayActive);
                    // Η ενεργοποίηση του κουμπιού (να πατιέται) ρυθμίζεται ήδη στην OnTelemetryUpdated
                }
            };

            // 3. Σύνδεση των υπόλοιπων γεγονότων
            ConnectSimulationSignals();
            BindInputEvents();

            SetShiftView(CyclePhase.DayActive);
        }

        private void ConnectSimulationSignals()
        {
            if (_gameOrchestrator == null) return;
            _gameOrchestrator.TelemetryUpdated += OnTelemetryUpdated;
            _gameOrchestrator.PhaseTransitionTriggered += OnPhaseTransitionTriggered;
        }

        private void BindUiNodes()
        {
            var stack = GetNode<VBoxContainer>("ScreenMargin/VerticalStack");

            // --- Header & Boiler Panels ---
            var headerVBox = stack.GetNode<VBoxContainer>("PersistentHeaderPanel/HeaderVBox");
            _coalLabel = headerVBox.GetNode<Label>("PrimaryStockpileHBox/CoalLabel");
            _scrapLabel = headerVBox.GetNode<Label>("PrimaryStockpileHBox/ScrapLabel");
            _cityTempLabel = headerVBox.GetNode<Label>("PrimaryStockpileHBox/CityTempLabel");
            _totalPopLabel = headerVBox.GetNode<Label>("PopulationQuickBarHBox/TotalPopLabel");
            _healthyCountLabel = headerVBox.GetNode<Label>("PopulationQuickBarHBox/HealthyCountLabel");
            _sickCountLabel = headerVBox.GetNode<Label>("PopulationQuickBarHBox/SickCountLabel");

            var boilerVBox = stack.GetNode<VBoxContainer>("PersistentBoilerStatusPanel/BoilerVBox");
            _pressureTitleLabel = boilerVBox.GetNode<Label>("PressureHeaderHBox/PressureTitleLabel");
            _coreTempLabel = boilerVBox.GetNode<Label>("PressureHeaderHBox/CoreTempLabel");
            _fuseRackContainer = boilerVBox.GetNode<HBoxContainer>("PressureHeaderHBox/FuseRackContainer");
            _pressureGaugeBar = boilerVBox.GetNode<ProgressBar>("PressureGaugeBar");

            _casingTitleLabel = boilerVBox.GetNode<Label>("CasingRowHBox/CasingTitleLabel");
            _casingIntegrityBar = boilerVBox.GetNode<ProgressBar>("CasingRowHBox/CasingIntegrityBar");
            _sootTitleLabel = boilerVBox.GetNode<Label>("SootRowHBox/SootTitleLabel");
            _sootFoulingBar = boilerVBox.GetNode<ProgressBar>("SootRowHBox/SootFoulingBar");
            _blowoutButton = boilerVBox.GetNode<Button>("SootRowHBox/BlowoutButton");

            var chronoVBox = stack.GetNode<VBoxContainer>("PersistentChronometerPanel/ChronoVBox");
            _cycleClockLabel = chronoVBox.GetNode<Label>("MasterClockHBox/CycleClockLabel");
            _shiftCountdownLabel = chronoVBox.GetNode<Label>("MasterClockHBox/ShiftCountdownLabel");

            var viewport = stack.GetNode<MarginContainer>("MainViewportArea");
            _dayShiftView = viewport.GetChild<Control>(0);
            _nightShiftView = viewport.GetChild<Control>(1);

            // --- Κουμπί Εκτόνωσης & Hazard Μηχανουργείου ---
            _ventActuatorButton = _dayShiftView.GetNode<Button>("DayVBox/VentDecompressionCard/VentVBox/VentActuatorButton");
            _workshopHazardBar = _nightShiftView.GetNode<ProgressBar>("NightVBox/WorkshopCard/WorkshopHazardVBox/WorkshopHazardHBox/WorkshopHazardBar");

            var laborVBox = _dayShiftView.GetNode<VBoxContainer>("DayVBox/LaborAllocationCard/LaborVBox");
            _unassignedReserveLabel = laborVBox.GetNode<Label>("UnassignedReserveLabel");

            _relayLabel = headerVBox.GetNode<Label>("PrimaryStockpileHBox/RelayLabel");
            _microBleedTargetDropdown = laborVBox.GetNode<OptionButton>("Job_MicroBleed/JobVBox/TargetDropdown");
            // --- Ανάκτηση των καρτών εργασιών (Ημέρα) ---
            BindJobCard(laborVBox, "Job_StokeFurnace", JobType.StokeFurnace);
            BindJobCard(laborVBox, "Job_MicroBleed", JobType.MicroBleed);
            BindJobCard(laborVBox, "Job_HullBracing", JobType.StructuralBracing);
            BindJobCard(laborVBox, "Job_ScourFlues", JobType.ScourFlues);

            // --- Ανάκτηση των καρτών εργασιών (Νύχτα) ---
            var nightJobsVBox = _nightShiftView.GetNode<VBoxContainer>("NightVBox/NightJobsVBox");
            BindJobCard(nightJobsVBox, "Job_DamperThrottling", JobType.DamperThrottling);
            BindJobCard(nightJobsVBox, "Job_DeepScour", JobType.DeepScour);
            BindJobCard(nightJobsVBox, "Job_WorkshopMaint", JobType.WorkshopMaintenance);
            BindJobCard(nightJobsVBox, "Job_IntensiveCare", JobType.IntensiveCare);

            BindJobCard(laborVBox, "Job_WorkshopMachining", JobType.WorkshopMachining);
            BindJobCard(nightJobsVBox, "Job_ManualTinkering", JobType.ManualTinkering);
            // --- Ανάκτηση του Vent Routing Modal ---
            var overlay = GetNode<Control>("ModalOverlayLayer/MarginContainer");
            _ventRoutingModal = overlay.GetNode<PanelContainer>("VentRoutingModal");

            // Τα στοιχεία είναι απευθείας παιδιά, όπως φαίνεται στο δέντρο
            _cityVentSlider = _ventRoutingModal.GetNode<HSlider>("RoutingVBox/CityRoutingHBox/CityVentSlider");
            _cityVentValue = _ventRoutingModal.GetNode<Label>("RoutingVBox/CityRoutingHBox/CityVentValueLabel");

            _workshopVentSlider = _ventRoutingModal.GetNode<HSlider>("RoutingVBox/WorkshopRoutingHBox/WorkshopVentSlider");
            _workshopVentValue = _ventRoutingModal.GetNode<Label>("RoutingVBox/WorkshopRoutingHBox/WorkshopVentValueLabel");

            _drillVentSlider = _ventRoutingModal.GetNode<HSlider>("RoutingVBox/DrillRoutingHBox/DrillVentSlider");
            _drillVentValue = _ventRoutingModal.GetNode<Label>("RoutingVBox/DrillRoutingHBox/DrillVentValueLabel");

            // Η εκτόνωση στην ατμόσφαιρα
            _atmosphereVentSlider = _ventRoutingModal.GetNode<HSlider>("RoutingVBox/AtmosphereRoutingHBox/AtmosphereVentSlider");
            _atmosphereVentValue = _ventRoutingModal.GetNode<Label>("RoutingVBox/AtmosphereRoutingHBox/AtmosphereVentValueLabel");

            // Το label που θα δείχνει την εναπομείνασα πίεση
            _retainVentValue = _ventRoutingModal.GetNode<Label>("RetainedPressureValueLabel");
            _confirmVentButton = _ventRoutingModal.GetNode<Button>("RoutingVBox/ConfirmVentButton");

            // Το κουμπί από το κάτω μενού
            _adminPanelButton = stack.GetNode<Button>("PersistentBottomDock/DockHBox/AdminPanelButton");
            // Το modal (που τώρα έχει και το δικό του script)
            _adminPanelModal = GetNode<PanelContainer>("ModalOverlayLayer/MarginContainer/AdminPanelModal");

            // (Κάτω από το _adminPanelButton = ...)
            _openShopButton = stack.GetNode<Button>("PersistentBottomDock/DockHBox/OpenShopButton");
            _openInventoryButton = stack.GetNode<Button>("PersistentBottomDock/DockHBox/OpenInventoryButton");

            // Shop Modal
            _workshopCraftingModal = overlay.GetNode<PanelContainer>("WorkshopCraftingModal");
            _recipeListVBox = _workshopCraftingModal.GetNode<VBoxContainer>("MainVBox/ScrollArea/RecipeListVBox");
            _recipePrefab = _recipeListVBox.GetNode<HBoxContainer>("RecipePrefab");
            _closeShopBtn = _workshopCraftingModal.GetNode<Button>("MainVBox/CloseShopBtn");

            // Inventory Modal
            _inventoryModal = overlay.GetNode<PanelContainer>("InventoryModal");
            _itemListVBox = _inventoryModal.GetNode<VBoxContainer>("MainVBox/ScrollArea/ItemListVBox");
            _itemPrefab = _itemListVBox.GetNode<HBoxContainer>("ItemPrefab");
            _closeInventoryBtn = _inventoryModal.GetNode<Button>("MainVBox/CloseInventoryBtn");
        }

        // Βοηθητική μέθοδος για την εύκολη ανάκτηση των κόμβων κάθε εργασίας
        private void BindJobCard(Node parent, string nodeName, JobType type)
        {
            var container = parent.GetNode<PanelContainer>(nodeName);
            var vbox = container.GetNode<VBoxContainer>("JobVBox");

            var nameLabel = vbox.GetNode<Label>("HeaderHBox/JobNameLabel"); 

            var card = new JobCardUI
            {
                BaseName = nameLabel.Text, 
                JobNameLabel = nameLabel,
                HazardLabel = vbox.GetNode<Label>("HeaderHBox/HazardLabel"),
                JobProgressBar = vbox.GetNode<ProgressBar>("JobProgressBar"),
                MinusBtn = vbox.GetNode<Button>("StaffingHBox/MinusBtn"),
                StaffingLabel = vbox.GetNode<Label>("StaffingHBox/StaffingLabel"),
                PlusBtn = vbox.GetNode<Button>("StaffingHBox/PlusBtn")
            };

            card.PlusBtn.Pressed += () => _gameOrchestrator.Population.TryAdjustWorkers(type, 1);
            card.MinusBtn.Pressed += () => _gameOrchestrator.Population.TryAdjustWorkers(type, -1);

            _jobCards[type] = card;
        }

        private void BindInputEvents()
        {
            // Το κουμπί εκτόνωσης πλέον ανοίγει το Modal (αν ο χρόνος έχει μηδενιστεί)
            _ventActuatorButton.Pressed += () => _gameOrchestrator.Clock.TriggerVentModal();

            // Σύνδεση των sliders για ανανέωση των κειμένων κατά την κίνηση
            _cityVentSlider.ValueChanged += _ => UpdateVentRoutingValues();
            _workshopVentSlider.ValueChanged += _ => UpdateVentRoutingValues();
            _drillVentSlider.ValueChanged += _ => UpdateVentRoutingValues();
            _atmosphereVentSlider.ValueChanged += _ => UpdateVentRoutingValues(); 
            _blowoutButton.Pressed += () => _gameOrchestrator.TriggerEmergencyBlowout();
            _confirmVentButton.Pressed += OnConfirmVentRouting;
            _adminPanelButton.Pressed += () => _adminPanelModal.Visible = true;
            _microBleedTargetDropdown.ItemSelected += (index) =>
            {
                _gameOrchestrator.JobManager.ActiveMicroBleedTarget = (MicroBleedTarget)index;
            };

            _openShopButton.Pressed += OpenShopModal;
            _closeShopBtn.Pressed += () => _workshopCraftingModal.Visible = false;

            _openInventoryButton.Pressed += OpenInventoryModal;
            _closeInventoryBtn.Pressed += () => _inventoryModal.Visible = false;
        }

        private void OpenShopModal()
        {
            _workshopCraftingModal.Visible = true;

            // Καθαρισμός παλιών εγγραφών (εκτός από το Prefab)
            foreach (Node child in _recipeListVBox.GetChildren())
            {
                if (child != _recipePrefab) child.QueueFree();
            }

            // Δημιουργία λίστας με βάση τις συνταγές στα Settings
            foreach (var kvp in GameSettings.Instance.CraftingRecipes)
            {
                string recipeName = kvp.Key;
                var recipe = kvp.Value;

                var newItem = (HBoxContainer)_recipePrefab.Duplicate();
                newItem.Visible = true;
                _recipeListVBox.AddChild(newItem);

                newItem.GetNode<Label>("RecipeName").Text = recipeName;
                newItem.GetNode<Label>("CostLabel").Text = $"Cost: {recipe.ScrapCost} Scrap | {recipe.ElectricityCost} kW | {(recipe.BaseWorkTime / 60):F0}m";

                var craftBtn = newItem.GetNode<Button>("CraftBtn");

                // --- ΝΕΟ: Έλεγχος & Χρωματισμός της ενεργής συνταγής ---
                if (_gameOrchestrator.JobManager.ActiveCraftingRecipe == recipeName)
                {
                    newItem.Modulate = new Color(0.2f, 1f, 0.2f); // Πράσινο χρώμα για τη γραμμή που κατασκευάζεται
                    craftBtn.Text = "CRAFTING...";
                    craftBtn.Disabled = true; // Δεν χρειάζεται να το ξαναπατήσει
                }
                else
                {
                    newItem.Modulate = new Color(1f, 1f, 1f); // Λευκό (κανονικό) χρώμα για τα υπόλοιπα
                    craftBtn.Text = "CRAFT";
                    craftBtn.Disabled = false;
                }

                craftBtn.Pressed += () =>
                {
                    bool success = _gameOrchestrator.QueueCraftingRecipe(recipeName);
                    if (success)
                    {
                        // ΣΗΜΑΝΤΙΚΟ: Δεν κλείνουμε το modal. Το ξανακαλούμε για να εφαρμόσει αμέσως τα νέα χρώματα!
                        OpenShopModal();
                    }
                };
            }
        }

        private void OpenInventoryModal()
        {
            _inventoryModal.Visible = true;
            RefreshInventoryList();
        }

        // Αυτή η μέθοδος ανανεώνει τη λίστα του Inventory. 
        // Την έχουμε ξεχωριστά ώστε να μπορούμε να την καλούμε και από το OnTelemetryUpdated αν θέλουμε live μπάρες.
        private void RefreshInventoryList()
        {
            if (!_inventoryModal.Visible) return;

            // Καθαρισμός παλιών εγγραφών (εκτός από το Prefab)
            foreach (Node child in _itemListVBox.GetChildren())
            {
                if (child != _itemPrefab) child.QueueFree();
            }

            foreach (var item in _gameOrchestrator.Inventory.Items)
            {
                // Αν έσπασε, δεν το δείχνουμε (ή μπορούμε να το δείχνουμε κόκκινο)
                if (item.Durability <= 0) continue;

                var newRow = (HBoxContainer)_itemPrefab.Duplicate();
                newRow.Visible = true;
                _itemListVBox.AddChild(newRow);

                newRow.GetNode<Label>("ItemNameLabel").Text = item.Name;

                var bar = newRow.GetNode<ProgressBar>("DurabilityBar");
                bar.Value = item.Durability;

                var equipBtn = newRow.GetNode<Button>("EquipBtn");
                equipBtn.Text = item.IsEquipped ? "UNEQUIP" : "EQUIP";
                equipBtn.Modulate = item.IsEquipped ? new Color(0.2f, 1f, 0.2f) : new Color(1f, 1f, 1f);

                equipBtn.Pressed += () =>
                {
                    if (item.IsEquipped)
                    {
                        _gameOrchestrator.Inventory.UnequipItem(item.Name);
                    }
                    else
                    {
                        _gameOrchestrator.Inventory.EquipItem(item.Name);
                    }
                    // Ανανέωση του UI αμέσως μετά το πάτημα
                    RefreshInventoryList();
                };
            }
        }

        private void OnConfirmVentRouting()
        {
            double city = _cityVentSlider.Value;
            double workshop = _workshopVentSlider.Value;
            double drill = _drillVentSlider.Value;
            double atmosphere = _atmosphereVentSlider.Value;

            double total = city + workshop + drill + atmosphere;
            double currentPressure = _gameOrchestrator.Boiler.SteamPressure;

            // Αμυντικός έλεγχος μήπως τα sliders υπερβαίνουν τη διαθέσιμη πίεση
            if (total > currentPressure) return;

            // Το 5ο στοιχείο (retain) υπολογίζεται αυτόματα από τον Orchestrator, 
            // στέλνουμε τα 4 που διάλεξε ενεργά ο παίκτης.
            _ventRoutingModal.Visible = false;
            _gameOrchestrator.ExecuteVentRouting(city, workshop, drill, atmosphere);
        }

        private void UpdateVentRoutingValues()
        {
            double currentPressure = _gameOrchestrator.Boiler.SteamPressure;
            double totalUsed = _cityVentSlider.Value + _workshopVentSlider.Value + _drillVentSlider.Value + _atmosphereVentSlider.Value;

            // Ενημέρωση των ετικετών (Labels) με τις τρέχουσες επιλογές
            _cityVentValue.Text = $"{_cityVentSlider.Value:F0} Bar";
            _workshopVentValue.Text = $"{_workshopVentSlider.Value:F0} Bar";
            _drillVentValue.Text = $"{_drillVentSlider.Value:F0} Bar";
            _atmosphereVentValue.Text = $"{_atmosphereVentSlider.Value:F0} Bar";

            // Υπολογισμός και εμφάνιση της πίεσης που θα παραμείνει στον λέβητα
            double retained = Math.Max(0, currentPressure - totalUsed);

            if (totalUsed > currentPressure)
            {
                _retainVentValue.Text = "INSUFFICIENT PRESSURE";
                _retainVentValue.Modulate = new Color(1, 0, 0);
                _confirmVentButton.Disabled = true;
            }
            else
            {
                _retainVentValue.Text = $"Retained Pressure: {retained:F1} Bar";
                _retainVentValue.Modulate = new Color(0.2f, 1f, 0.2f); // Πράσινο για να φαίνεται ασφαλές
                _confirmVentButton.Disabled = false;
            }
        }

        private void OnTelemetryUpdated()
        {
            var b = _gameOrchestrator.LatestBoilerTelemetry;
            var t = _gameOrchestrator.LatestThermodynamicTelemetry;
            var w = _gameOrchestrator.LatestWorkforceTelemetry;
            var l = _gameOrchestrator.LatestLogisticsTelemetry;

            // --- Ενημέρωση Κεφαλίδας (Header) ---
            _coalLabel.Text = $"Coal: {l.CoalStockpile:F0} kg";
            _scrapLabel.Text = $"Scrap: {l.ScrapStockpile:F0}";
            _relayLabel.Text = $"Relay: {l.Electricity:F1} kW";
            _cityTempLabel.Text = $"City: {t.CityTemperature:F1}°C";
            _totalPopLabel.Text = $"Pop: {w.TotalPopulation}";
            _healthyCountLabel.Text = $"H: {w.HealthyWorkers}";
            _sickCountLabel.Text = $"S: {w.SickWorkers}";

            // --- Ενημέρωση Λέβητα (Boiler) ---
            _pressureTitleLabel.Text = $"PRESSURE: {b.SteamPressure:F1} / 200 Bar";
            _pressureGaugeBar.Value = b.SteamPressure;
            _coreTempLabel.Text = $"CORE: {t.CoreTemperature:F0}°C";

            _casingTitleLabel.Text = $"HULL: {b.CasingIntegrity:F1}%";
            _casingIntegrityBar.Value = b.CasingIntegrity;
            _sootTitleLabel.Text = $"SOOT: {b.SootFraction * 100.0:F1}%";
            _sootFoulingBar.Value = b.SootFraction * 100.0;
            _blowoutButton.Disabled = b.SteamPressure < GameSettings.Instance.BlowoutPressureCost;
            // Ενημέρωση ενδείξεων των Ασφαλειών (Fuses)
            var children = _fuseRackContainer.GetChildren();
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Control fuseIcon)
                {
                    fuseIcon.Modulate = (i < b.AvailableFuses) ? new Color(1, 1, 1, 1) : new Color(0.3f, 0.3f, 0.3f, 0.35f);
                }
            }

            // --- Ενημέρωση Ρολογιού & Κουμπιού Εκτόνωσης ---
            _cycleClockLabel.Text = $"TIME: {_gameOrchestrator.Clock.GetFormattedTimeOfDay()}";
            _shiftCountdownLabel.Text = $"NEXT VENT: {b.SecondsUntilNextVent:F0}s";

            // Το κουμπί ενεργοποιείται μόνο στη φάση VentPending
            _ventActuatorButton.Disabled = b.CurrentPhase != CyclePhase.VentPending;
            if (b.CurrentPhase == CyclePhase.VentPending)
            {
                _ventActuatorButton.Text = "ACTUATE DECOMPRESSION LEVER [READY]";
                _ventActuatorButton.Modulate = new Color(0.2f, 1f, 0.2f); // Πράσινο για να τραβήξει την προσοχή
            }
            else
            {
                _ventActuatorButton.Text = $"ACTUATE DECOMPRESSION LEVER [LOCKED > {b.SecondsUntilNextVent:F0}s]";
                _ventActuatorButton.Modulate = new Color(1f, 1f, 1f);
            }

            // --- Ενημέρωση Καρτών Εργασιών (Job Cards) ---
            _unassignedReserveLabel.Text = $"Reserve Hands: {w.UnassignedReserve}";
            _workshopHazardBar.Value = l.WorkshopHazardLevel * 100.0;
            TimeSpan time = new TimeSpan();
            foreach (var jobData in _gameOrchestrator.LatestJobsTelemetry)
            {
                // 1. Έλεγχος γρήγορης εξόδου (Early exit) για αποφυγή βαθιών nested if
                if (!_jobCards.TryGetValue(jobData.Type, out JobCardUI card)) continue;

                // 2. Ενημέρωση των κουμπιών και των εργατών (Ισχύει για ΟΛΕΣ τις εργασίες)
                card.StaffingLabel.Text = $"{jobData.AssignedWorkers} Hands";
                card.PlusBtn.Disabled = w.UnassignedReserve <= 0;
                card.MinusBtn.Disabled = jobData.AssignedWorkers <= 0;

                if (jobData.Type == JobType.StokeFurnace)
                {
                    // 3. Ειδικός χειρισμός μόνο για τον φούρνο
                    double coalPerHour = jobData.AssignedWorkers * GameSettings.Instance.StokeCoalPerWorkerPerHour;
                    card.JobNameLabel.Text = $"{card.BaseName} - Κατανάλωση: {coalPerHour:F1} kg/h";
                    card.JobProgressBar.Value = 100.0;
                }
                else
                {
                    // 4. Χειρισμός για όλες τις υπόλοιπες εργασίες
                    card.JobProgressBar.Value = jobData.ProgressPercentage;

                    if (jobData.AssignedWorkers > 0)
                    {
                        double effectiveRate = Math.Pow(jobData.AssignedWorkers, 0.8);
                        double realSecondsLeft = jobData.WorkRemaining / effectiveRate;

                        // 5. Βελτιστοποιημένη μορφοποίηση χρόνου
                        time = TimeSpan.FromSeconds(realSecondsLeft);
                        string format = time.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss";

                        // Αν είναι μηχανουργείο, δείχνουμε ΚΑΙ το όνομα της συνταγής δίπλα στο όνομα της δουλειάς
                        if (jobData.Type == JobType.WorkshopMachining || jobData.Type == JobType.ManualTinkering)
                        {
                            string recipeName = _gameOrchestrator.JobManager.ActiveCraftingRecipe;
                            if (!string.IsNullOrEmpty(recipeName))
                            {
                                card.JobNameLabel.Text = $"{card.BaseName} [{recipeName}] - {time.ToString(format)}";
                            }
                            else
                            {
                                card.JobNameLabel.Text = $"{card.BaseName} - [IDLE]";
                            }
                        }
                        else
                        {
                            card.JobNameLabel.Text = $"{card.BaseName} - {time.ToString(format)}";
                        }
                    }
                    else
                    {
                        // Αν δεν υπάρχουν εργάτες
                        if (jobData.Type == JobType.WorkshopMachining || jobData.Type == JobType.ManualTinkering)
                        {
                            string recipeName = _gameOrchestrator.JobManager.ActiveCraftingRecipe;
                            if (!string.IsNullOrEmpty(recipeName))
                            {
                                card.JobNameLabel.Text = $"{card.BaseName} [{recipeName}] - [ΠΑΥΣΗ/ΑΝΑΜΟΝΗ ΠΡΟΣΩΠΙΚΟΥ]";
                            }
                            else
                            {
                                card.JobNameLabel.Text = $"{card.BaseName} - [IDLE]";
                            }
                        }
                        else
                        {
                            card.JobNameLabel.Text = $"{card.BaseName} - [ΠΑΥΣΗ]";
                        }
                    }
                }
            }
            RefreshInventoryList();
        }

        private void OnPhaseTransitionTriggered(int newPhase)
        {
            SetShiftView((CyclePhase)newPhase);
        }

        private void SetShiftView(CyclePhase phase)
        {
            // Ημέρα: Εμφάνιση του DayShiftView
            _dayShiftView.Visible = (phase == CyclePhase.DayActive || phase == CyclePhase.VentPending || phase == CyclePhase.TwilightWindDown);

            // Νύχτα: Εμφάνιση του NightShiftView
            _nightShiftView.Visible = (phase == CyclePhase.NightBleed);

            // Modal Εκτόνωσης
            if (phase == CyclePhase.VentRouting)
            {
                double currentPressure = _gameOrchestrator.Boiler.SteamPressure;
                _routingTitleLabel.Text = $"DECOMPRESSION ROUTING - {currentPressure:F0} BAR AVAILABLE";

                // Επαναφορά των sliders στο 0
                _cityVentSlider.Value = 0;
                _workshopVentSlider.Value = 0;
                _drillVentSlider.Value = 0;
                _atmosphereVentSlider.Value = 0;
                _atmosphereVentSlider.MaxValue = currentPressure;
                // Ορισμός της μέγιστης επιτρεπτής τιμής κάθε slider (δεν μπορεί να ξεπερνά τη συνολική πίεση)
                _cityVentSlider.MaxValue = currentPressure;
                _workshopVentSlider.MaxValue = currentPressure;
                _drillVentSlider.MaxValue = currentPressure;

                UpdateVentRoutingValues();
                _ventRoutingModal.Visible = true;
            }
            else
            {
                _ventRoutingModal.Visible = false;
            }
        }
    }
}