using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using PermafrostChieftain.Simulation;

namespace PermafrostChieftain.Orchestration
{
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

        // ΝΕΑ ΣΤΟΙΧΕΙΑ: Stress & Hull Alert
        private Label _pressureTitleLabel, _coreTempLabel, _casingTitleLabel, _sootTitleLabel;
        private ProgressBar _pressureGaugeBar, _stressGaugeBar, _casingIntegrityBar, _sootFoulingBar;
        private TextureRect _hullCrackAlertIcon;
        private HBoxContainer _fuseRackContainer;

        // ΝΕΑ ΣΤΟΙΧΕΙΑ: Φάση Ρολογιού & Calculator Πόλης
        private Label _cycleClockLabel, _shiftCountdownLabel, _currentPhaseLabel;
        private Label _cityHeatPredictionLabel;
        private HSlider _targetCityTempSlider;

        private Control _dayShiftView, _nightShiftView;
        private Button _ventActuatorButton;
        private ProgressBar _workshopHazardBar;
        private Label _unassignedReserveLabel;

        // Οθόνη Εκτόνωσης (Vent Routing Modal)
        private PanelContainer _ventRoutingModal;
        private Label _routingTitleLabel;
        private HSlider _cityVentSlider, _workshopVentSlider, _drillVentSlider, _atmosphereVentSlider;
        private Label _cityVentValue, _workshopVentValue, _drillVentValue, _atmosphereVentValue, _retainVentValue;
        private Button _confirmVentButton;

        private Dictionary<JobType, JobCardUI> _jobCards = new();

        private Button _adminPanelButton;
        private PanelContainer _adminPanelModal;

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

        // Στοιχεία UI για Logs & Reports
        private Button _openLogsButton;
        private PanelContainer _logsReportsModal;
        private Button _closeLogsBtn;
        private VBoxContainer _logsVBox;
        private VBoxContainer _reportListVBox;
        private RichTextLabel _reportContentText;

        private Control _gameOverModal;
        private Label _gameOverReasonLabel;
        private Button _gameOverRestartBtn;
        private RichTextLabel _finalStatsLabel;

        // Κρατάμε ποια συνταγή crafting έχει επιλέξει ο παίκτης από το Dropdown/Modal
        private JobType _activeCraftingJob = JobType.WorkshopCraftPatch;

        public override void _Ready()
        {
            // 1. Ασφαλής ανάκτηση του Orchestrator από το Root του δέντρου
            _gameOrchestrator = GetTree().Root.GetNodeOrNull<GameOrchestrator>("TheGame");

            // 2. Δέσιμο των UI Nodes
            BindUiNodes();

            // 3. Σύνδεση με το ρολόι (Πλέον είμαστε σίγουροι ότι το Clock ΔΕΝ είναι null)
            if (_gameOrchestrator != null)
            {
                _gameOrchestrator.Clock.VentModalTimeoutExpired += () =>
                {
                    if (_ventRoutingModal != null && _ventRoutingModal.Visible)
                    {
                        _ventRoutingModal.Visible = false;
                    }
                    GD.Print("UI: Το παράθυρο εκτόνωσης έκλεισε βίαια λόγω timeout!");
                };
            }
            else
            {
                GD.PrintErr("UIOrchestrator: Δεν βρέθηκε το TheGame (GameOrchestrator) στο δέντρο!");
            }

            ConnectSimulationSignals();
            BindInputEvents();

            SetShiftView(CyclePhase.DayActive);
        }

        private void ConnectSimulationSignals()
        {
            if (_gameOrchestrator == null) return;

            // Το TelemetrySnapshot πλέον τραβάει τα πάντα με μια κλήση
            _gameOrchestrator.OnGameEvent += OnGameEventReceived;
        }

        public override void _Process(double delta)
        {
            // Αντί για event, ρωτάμε το backend κάθε frame για το UI Snapshot
            if (_gameOrchestrator != null)
            {
                OnTelemetryUpdated();
            }
        }

        private void BindUiNodes()
        {
            var stack = GetNode<VBoxContainer>("ScreenMargin/VerticalStack");

            // --- Header Panels ---
            var headerVBox = stack.GetNode<VBoxContainer>("PersistentHeaderPanel/HeaderVBox");
            _coalLabel = headerVBox.GetNode<Label>("PrimaryStockpileHBox/CoalLabel");
            _scrapLabel = headerVBox.GetNode<Label>("PrimaryStockpileHBox/ScrapLabel");
            _relayLabel = headerVBox.GetNode<Label>("PrimaryStockpileHBox/RelayLabel");
            _cityTempLabel = headerVBox.GetNode<Label>("PrimaryStockpileHBox/CityTempLabel");
            _totalPopLabel = headerVBox.GetNode<Label>("PopulationQuickBarHBox/TotalPopLabel");
            _healthyCountLabel = headerVBox.GetNode<Label>("PopulationQuickBarHBox/HealthyCountLabel");
            _sickCountLabel = headerVBox.GetNode<Label>("PopulationQuickBarHBox/SickCountLabel");

            // --- Boiler Panel ---
            var boilerVBox = stack.GetNode<VBoxContainer>("PersistentBoilerStatusPanel/BoilerVBox");
            _pressureTitleLabel = boilerVBox.GetNode<Label>("PressureHeaderHBox/PressureTitleLabel");
            _coreTempLabel = boilerVBox.GetNode<Label>("PressureHeaderHBox/CoreTempLabel");
            _fuseRackContainer = boilerVBox.GetNode<HBoxContainer>("PressureHeaderHBox/FuseRackContainer");
            _pressureGaugeBar = boilerVBox.GetNode<ProgressBar>("PressureGaugeBar");

            // ΝΕΑ: Stress & Hull Alert 
            _stressGaugeBar = boilerVBox.GetNode<ProgressBar>("StressRowHBox/StressGaugeBar");
            _casingTitleLabel = boilerVBox.GetNode<Label>("CasingRowHBox/CasingTitleLabel");
            _hullCrackAlertIcon = boilerVBox.GetNode<TextureRect>("CasingRowHBox/HullCrackAlertIcon");
            _casingIntegrityBar = boilerVBox.GetNode<ProgressBar>("CasingRowHBox/CasingIntegrityBar");

            _sootTitleLabel = boilerVBox.GetNode<Label>("SootRowHBox/SootTitleLabel");
            _sootFoulingBar = boilerVBox.GetNode<ProgressBar>("SootRowHBox/SootFoulingBar");

            // --- Chronometer Panel ---
            var chronoVBox = stack.GetNode<VBoxContainer>("PersistentChronometerPanel/ChronoVBox");
            _cycleClockLabel = chronoVBox.GetNode<Label>("MasterClockHBox/CycleClockLabel");
            _shiftCountdownLabel = chronoVBox.GetNode<Label>("MasterClockHBox/NextVentCountdownLabel");
            _currentPhaseLabel = chronoVBox.GetNode<Label>("MasterClockHBox/CurrentPhaseLabel");

            var viewport = stack.GetNode<MarginContainer>("MainViewportArea");
            _dayShiftView = viewport.GetChild<Control>(0);
            _nightShiftView = viewport.GetChild<Control>(1);

            _ventActuatorButton = _dayShiftView.GetNode<Button>("DayVBox/VentDecompressionCard/VentVBox/VentActuatorButton");
            _workshopHazardBar = _nightShiftView.GetNode<ProgressBar>("NightVBox/WorkshopCard/WorkshopHazardVBox/WorkshopHazardHBox/WorkshopHazardBar");

            var laborVBox = _dayShiftView.GetNode<VBoxContainer>("DayVBox/LaborAllocationCard/LaborVBox");
            _unassignedReserveLabel = laborVBox.GetNode<Label>("UnassignedReserveLabel");

            // --- Ανάκτηση των καρτών εργασιών (Ημέρα) - Ενημερωμένα Ονόματα ---
            BindJobCard(laborVBox, "Job_StokeFurnace", JobType.StokeFurnace);
            BindJobCard(laborVBox, "Job_CoalMining", JobType.CoalMining);
            BindJobCard(laborVBox, "Job_ScrapScavenging", JobType.ScrapScavenging);
            BindJobCard(laborVBox, "Job_MetalSmelting", JobType.MetalSmelting);
            BindJobCard(laborVBox, "Job_OperateTurbines", JobType.OperateTurbines);

            // Το dropdown/crafting χρησιμοποιεί ένα job type τη φορά
            BindJobCard(laborVBox, "Job_WorkshopMachining", JobType.WorkshopCraftPatch);

            // --- Ανάκτηση των καρτών εργασιών (Νύχτα) - Ενημερωμένα Ονόματα ---
            var nightJobsVBox = _nightShiftView.GetNode<VBoxContainer>("NightVBox/NightJobsVBox");
            BindJobCard(nightJobsVBox, "Job_NightHullRepair", JobType.NightHullRepair);
            BindJobCard(nightJobsVBox, "Job_WorkshopUpkeep", JobType.WorkshopUpkeep);
            BindJobCard(nightJobsVBox, "Job_IntensiveCare", JobType.IntensiveCare);
            // Το νυχτερινό crafting χρησιμοποιεί το Fuse (ως default, αλλάζει μέσω του UI)
            BindJobCard(nightJobsVBox, "Job_NightCrafting", JobType.WorkshopCraftFuse);
            BindJobCard(laborVBox, "Job_DaytimePatch", JobType.DaytimePatch);

            // Ομοίως για το Soot (Καπνιά)
            BindJobCard(laborVBox, "Job_CleanSoot", JobType.CleanSoot);
            // --- Vent Routing Modal ---
            var overlay = GetNode<Control>("ModalOverlayLayer/MarginContainer");
            _ventRoutingModal = overlay.GetNode<PanelContainer>("VentRoutingModal");
            _routingTitleLabel = _ventRoutingModal.GetNode<Label>("RoutingVBox/RoutingTitleLabel");
            _cityVentSlider = _ventRoutingModal.GetNode<HSlider>("RoutingVBox/CityRoutingHBox/CityVentSlider");
            _cityVentValue = _ventRoutingModal.GetNode<Label>("RoutingVBox/CityRoutingHBox/CityVentValueLabel");
            _workshopVentSlider = _ventRoutingModal.GetNode<HSlider>("RoutingVBox/WorkshopRoutingHBox/WorkshopVentSlider");
            _workshopVentValue = _ventRoutingModal.GetNode<Label>("RoutingVBox/WorkshopRoutingHBox/WorkshopVentValueLabel");
            _drillVentSlider = _ventRoutingModal.GetNode<HSlider>("RoutingVBox/DrillRoutingHBox/DrillVentSlider");
            _drillVentValue = _ventRoutingModal.GetNode<Label>("RoutingVBox/DrillRoutingHBox/DrillVentValueLabel");
            _atmosphereVentSlider = _ventRoutingModal.GetNode<HSlider>("RoutingVBox/AtmosphereRoutingHBox/AtmosphereVentSlider");
            _atmosphereVentValue = _ventRoutingModal.GetNode<Label>("RoutingVBox/AtmosphereRoutingHBox/AtmosphereVentValueLabel");
            _retainVentValue = _ventRoutingModal.GetNode<Label>("RetainedPressureValueLabel");
            _confirmVentButton = _ventRoutingModal.GetNode<Button>("RoutingVBox/ConfirmVentButton");

            // --- Modals ---
            _adminPanelButton = stack.GetNode<Button>("PersistentBottomDock/DockHBox/AdminPanelButton");
            _adminPanelModal = GetNode<PanelContainer>("ModalOverlayLayer/MarginContainer/AdminPanelModal");
            _openShopButton = stack.GetNode<Button>("PersistentBottomDock/DockHBox/OpenShopButton");
            _openInventoryButton = stack.GetNode<Button>("PersistentBottomDock/DockHBox/OpenInventoryButton");

            _workshopCraftingModal = overlay.GetNode<PanelContainer>("WorkshopCraftingModal");
            _recipeListVBox = _workshopCraftingModal.GetNode<VBoxContainer>("MainVBox/ScrollArea/RecipeListVBox");
            _recipePrefab = _recipeListVBox.GetNode<HBoxContainer>("RecipePrefab");
            _recipePrefab.Visible = false;
            _closeShopBtn = _workshopCraftingModal.GetNode<Button>("MainVBox/CloseShopBtn");

            _inventoryModal = overlay.GetNode<PanelContainer>("InventoryModal");
            _itemListVBox = _inventoryModal.GetNode<VBoxContainer>("MainVBox/ScrollArea/ItemListVBox");
            _itemPrefab = _itemListVBox.GetNode<HBoxContainer>("ItemPrefab");
            _itemPrefab.Visible = false;
            _closeInventoryBtn = _inventoryModal.GetNode<Button>("MainVBox/CloseInventoryBtn");

            _openLogsButton = stack.GetNode<Button>("PersistentBottomDock/DockHBox/OpenLogsButton");
            _logsReportsModal = overlay.GetNode<PanelContainer>("LogsReportsModal");
            _closeLogsBtn = _logsReportsModal.GetNode<Button>("MainVBox/HeaderHBox/CloseLogsBtn");
            _logsVBox = _logsReportsModal.GetNode<VBoxContainer>("MainVBox/TabContainer/Logs/LogsVBox");
            _reportListVBox = _logsReportsModal.GetNode<VBoxContainer>("MainVBox/TabContainer/Reports/ReportListScroll/ReportListVBox");
            _reportContentText = _logsReportsModal.GetNode<RichTextLabel>("MainVBox/TabContainer/Reports/ReportContentText");
            _reportContentText.BbcodeEnabled = true;

            // --- Diagnostic Terminal (City Calculator) ---
            var diagnosticTerminal = overlay.GetNode<PanelContainer>("DiagnosticTerminalModal");
            _targetCityTempSlider = diagnosticTerminal.GetNode<HSlider>("TerminalVBox/TabContainer/CityHeatingTab/HeatingCalculatorVBox/TargetTemperatureSlider");
            _cityHeatPredictionLabel = diagnosticTerminal.GetNode<Label>("TerminalVBox/TabContainer/CityHeatingTab/HeatingCalculatorVBox/PredictionAlertLabel");

            _gameOverModal = overlay.GetNode<Control>("GameOverModal");
            _gameOverReasonLabel = _gameOverModal.GetNode<Label>("GameOverVBox/ReasonLabel");
            _gameOverRestartBtn = _gameOverModal.GetNode<Button>("GameOverVBox/RestartButton");
            _finalStatsLabel = _gameOverModal.GetNode<RichTextLabel>("GameOverVBox/FinalStatsLabel");
            _gameOverModal.Visible = false;
        }

        private void BindJobCard(Node parent, string nodeName, JobType type)
        {
            var container = parent.GetNodeOrNull<PanelContainer>(nodeName);
            if (container == null) return; // Fail-safe σε περίπτωση που δεν έχεις φτιάξει ακόμα τον κόμβο

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
            _ventActuatorButton.Pressed += OnMainCenterButtonPressed;

            _cityVentSlider.ValueChanged += _ => UpdateVentRoutingValues();
            _workshopVentSlider.ValueChanged += _ => UpdateVentRoutingValues();
            _drillVentSlider.ValueChanged += _ => UpdateVentRoutingValues();
            _atmosphereVentSlider.ValueChanged += _ => UpdateVentRoutingValues();

            _confirmVentButton.Pressed += OnConfirmVentRouting;
            _adminPanelButton.Pressed += () => _adminPanelModal.Visible = true;

            // Διαχείριση City Heating Calculator
            _targetCityTempSlider.ValueChanged += (val) => _gameOrchestrator.Thermo.SetTargetCityTemperature(val);

            _openShopButton.Pressed += OpenShopModal;
            _closeShopBtn.Pressed += () => _workshopCraftingModal.Visible = false;

            _openInventoryButton.Pressed += OpenInventoryModal;
            _closeInventoryBtn.Pressed += () => _inventoryModal.Visible = false;

            _openLogsButton.Pressed += () => _logsReportsModal.Visible = true;
            _closeLogsBtn.Pressed += () => _logsReportsModal.Visible = false;

            _gameOverRestartBtn.Pressed += () => GetTree().ReloadCurrentScene();
        }

        private void OnMainCenterButtonPressed()
        {
            _gameOrchestrator.Clock.TriggerVentModal();
        }

        private void OpenShopModal()
        {
            _workshopCraftingModal.Visible = true;

            foreach (Node child in _recipeListVBox.GetChildren())
            {
                if (child != _recipePrefab) child.QueueFree();
            }

            foreach (var recipe in RecipeRegistry.Recipes)
            {
                var newItem = (HBoxContainer)_recipePrefab.Duplicate();
                newItem.Visible = true;
                _recipeListVBox.AddChild(newItem);

                newItem.GetNode<Label>("RecipeName").Text = recipe.ItemName;
                newItem.GetNode<Label>("CostLabel").Text = $"Cost: {recipe.RefinedMetalCost} Metal | {recipe.WorkRequired}s";

                var craftBtn = newItem.GetNode<Button>("CraftBtn");

                // Ορίζουμε το active job βάσει της συνταγής
                if (_activeCraftingJob == recipe.AssociatedJob)
                {
                    newItem.Modulate = new Color(0.2f, 1f, 0.2f);
                    craftBtn.Text = "ACTIVE";
                    craftBtn.Disabled = true;
                }
                else
                {
                    newItem.Modulate = new Color(1f, 1f, 1f);
                    craftBtn.Text = "SELECT";
                    craftBtn.Disabled = false;
                }

                craftBtn.Pressed += () =>
                {
                    _activeCraftingJob = recipe.AssociatedJob;

                    // Αλλάζουμε δυναμικά το Binding της κάρτας WorkshopMachining στο νέο JobType!
                    if (_jobCards.ContainsKey(JobType.WorkshopCraftPatch))
                    {
                        var card = _jobCards[JobType.WorkshopCraftPatch];
                        // Αδειάζουμε τους εργάτες από την παλιά συνταγή
                        _gameOrchestrator.Population.TryAdjustWorkers(JobType.WorkshopCraftPatch, -card.StaffingLabel.Text.ToInt());
                    }

                    OpenShopModal(); // Refresh UI
                };
            }
        }

        private void OpenInventoryModal()
        {
            _inventoryModal.Visible = true;
            RefreshInventoryList();
        }

        private void RefreshInventoryList()
        {
            if (!_inventoryModal.Visible || _gameOrchestrator.Inventory == null) return;

            foreach (Node child in _itemListVBox.GetChildren())
            {
                if (child != _itemPrefab) child.QueueFree();
            }

            foreach (var item in _gameOrchestrator.Inventory.Items)
            {
                if (item.Durability <= 0 && item.Category == ItemCategory.Equippable) continue;

                var newRow = (HBoxContainer)_itemPrefab.Duplicate();
                newRow.Visible = true;
                _itemListVBox.AddChild(newRow);

                newRow.GetNode<Label>("ItemNameLabel").Text = item.Name;

                var bar = newRow.GetNode<ProgressBar>("DurabilityBar");
                var equipBtn = newRow.GetNode<Button>("EquipBtn");

                if (item.Category == ItemCategory.Consumable)
                {
                    // Tα αναλώσιμα δεν εξοπλίζονται[cite: 8]
                    equipBtn.Visible = false;
                    bar.Visible = false;
                }
                else
                {
                    equipBtn.Visible = true;
                    bar.Visible = true;
                    bar.Value = item.Durability;

                    equipBtn.Text = item.IsEquipped ? "UNEQUIP" : "EQUIP";
                    equipBtn.Modulate = item.IsEquipped ? new Color(0.2f, 1f, 0.2f) : new Color(1f, 1f, 1f);

                    equipBtn.Pressed += () =>
                    {
                        if (item.IsEquipped) _gameOrchestrator.Inventory.UnequipItem(item.Name);
                        else _gameOrchestrator.Inventory.EquipItem(item.Name);
                        RefreshInventoryList();
                    };
                }
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

            if (total > currentPressure) return;

            _ventRoutingModal.Visible = false;
            _gameOrchestrator.ExecuteRoutingPanel(drill, city, workshop, atmosphere);
        }

        private void UpdateVentRoutingValues()
        {
            double currentPressure = _gameOrchestrator.Boiler.SteamPressure;
            double totalUsed = _cityVentSlider.Value + _workshopVentSlider.Value + _drillVentSlider.Value + _atmosphereVentSlider.Value;

            _cityVentValue.Text = $"{_cityVentSlider.Value:F0} Bar";
            _workshopVentValue.Text = $"{_workshopVentSlider.Value:F0} Bar";
            _drillVentValue.Text = $"{_drillVentSlider.Value:F0} Bar";
            _atmosphereVentValue.Text = $"{_atmosphereVentSlider.Value:F0} Bar";

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
                _retainVentValue.Modulate = new Color(0.2f, 1f, 0.2f);
                _confirmVentButton.Disabled = false;
            }
        }

        private void OnTelemetryUpdated()
        {
            var telemetry = _gameOrchestrator.GetTelemetrySnapshot();
            var b = telemetry.Boiler;
            var t = telemetry.Thermo;
            var w = telemetry.Workforce;
            var l = telemetry.Logistics;

            // --- Κεφαλίδα ---
            _coalLabel.Text = $"Coal: {l.CoalStockpile:F0} kg";
            _scrapLabel.Text = $"Scrap: {l.RawScrap:F0}";
            _relayLabel.Text = $"Relay: {l.Electricity:F1} kW";
            _cityTempLabel.Text = $"City: {t.CityTemperature:F1}°C";
            _totalPopLabel.Text = $"Pop: {w.TotalPopulation}";
            _healthyCountLabel.Text = $"H: {w.HealthyWorkers}";
            _sickCountLabel.Text = $"S: {w.SickWorkers}";

            // --- Πάνελ Λέβητα ---
            _pressureTitleLabel.Text = $"PRESSURE: {b.SteamPressure:F1} Bar";
            _pressureGaugeBar.Value = b.SteamPressure;
            _coreTempLabel.Text = $"CORE: {t.CoreTemperature:F0}°C";

            // Επίπεδο Στρες
            _stressGaugeBar.Value = b.StressLevel;

            _casingTitleLabel.Text = $"HULL: {b.CasingIntegrity:F1}%";
            _casingIntegrityBar.Value = b.CasingIntegrity;

            // Εικονίδιο ρήγματος
            if (_hullCrackAlertIcon != null) _hullCrackAlertIcon.Visible = b.IsHullCracked;

            _sootTitleLabel.Text = $"SOOT: {b.SootFraction * 100.0:F1}%";
            _sootFoulingBar.Value = b.SootFraction * 100.0;

            // Οπτικοποίηση Ασφαλειών (Fuses) από το Inventory
            int fuseCount = _gameOrchestrator.Inventory.GetConsumableCount("Boiler Fuse");
            var children = _fuseRackContainer.GetChildren();
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] is Control fuseIcon)
                {
                    fuseIcon.Modulate = (i < fuseCount) ? new Color(1, 1, 1, 1) : new Color(0.3f, 0.3f, 0.3f, 0.35f);
                }
            }

            // --- Χρονόμετρο & Φάσεις ---
            _cycleClockLabel.Text = $"TIME: {b.FormattedTimeOfDay}";
            if (_currentPhaseLabel != null) _currentPhaseLabel.Text = b.CurrentPhase.ToString();

            // Δυναμική αλλαγή του αντίστροφου χρονομέτρου ανάλογα με τη φάση
            if (b.CurrentPhase == CyclePhase.NightBleed)
            {
                double timeToDawn = GameSettings.Instance.NightDuration - _gameOrchestrator.Clock.PhaseElapsedSeconds;
                TimeSpan ts = TimeSpan.FromSeconds(timeToDawn);
                _shiftCountdownLabel.Text = $"DAWN IN: {ts.ToString(@"hh\:mm\:ss")}";
            }
            else if (b.CurrentPhase == CyclePhase.TwilightWindDown)
            {
                double timeToNight = GameSettings.Instance.TwilightDuration - _gameOrchestrator.Clock.PhaseElapsedSeconds;
                TimeSpan ts = TimeSpan.FromSeconds(timeToNight);
                _shiftCountdownLabel.Text = $"NIGHT IN: {ts.ToString(@"mm\:ss")}";
            }
            else
            {
                _shiftCountdownLabel.Text = $"NEXT VENT: {b.SecondsUntilNextVent:F0}s";
            }

            SetShiftView(b.CurrentPhase);

            // --- Δυναμικό Κεντρικό Κουμπί (Ανάφλεξη ή Εκτόνωση) ---
            if (_ventActuatorButton != null)
            {
                _ventActuatorButton.Modulate = new Color(1f, 1f, 1f);

                // Εμφανίζεται μόνο την Ημέρα και κατά τη διάρκεια της Εκτόνωσης
                _ventActuatorButton.Visible = (b.CurrentPhase == CyclePhase.VentPending ||
                                               b.CurrentPhase == CyclePhase.VentRouting ||
                                               b.CurrentPhase == CyclePhase.DayActive);

                if (b.CurrentPhase == CyclePhase.VentPending)
                {
                    _ventActuatorButton.Text = "ACTUATE DECOMPRESSION LEVER [READY]";
                    _ventActuatorButton.Disabled = false;
                }
                else
                {
                    _ventActuatorButton.Text = $"ACTUATE DECOMPRESSION LEVER [LOCKED > {b.SecondsUntilNextVent:F0}s]";
                    _ventActuatorButton.Disabled = true;
                }
            }

            // --- Υπολογιστής Θέρμανσης Πόλης ---
            if (_targetCityTempSlider != null && _cityHeatPredictionLabel != null)
            {
                _cityHeatPredictionLabel.Text = $"Steam will last for: {t.EstimatedCityHeatHours:F1} Hours";
                if (t.EstimatedCityHeatHours < 16.0 && !ClockIsDayShift(b.CurrentPhase))
                    _cityHeatPredictionLabel.Modulate = new Color(1, 0, 0); // Κόκκινο προειδοποιητικό
                else
                    _cityHeatPredictionLabel.Modulate = new Color(1, 1, 1);
            }

            // --- Κάρτες Εργασιών ---
            _unassignedReserveLabel.Text = $"Reserve Hands: {w.UnassignedReserve}";

            TimeSpan time = new TimeSpan();
            foreach (var jobData in telemetry.Jobs)
            {
                if (!_jobCards.TryGetValue(jobData.Type, out JobCardUI card)) continue;

                card.StaffingLabel.Text = $"{jobData.AssignedWorkers} Hands";
                card.PlusBtn.Disabled = w.UnassignedReserve <= 0;
                card.MinusBtn.Disabled = jobData.AssignedWorkers <= 0;

                // Δυναμική εμφάνιση/απόκρυψη της μπάρας 
                if (card.JobProgressBar != null) card.JobProgressBar.Visible = !jobData.IsContinuous;

                if (jobData.IsContinuous)
                {
                    card.JobNameLabel.Text = $"{card.BaseName} (Continuous)";
                }
                else
                {
                    if (card.JobProgressBar != null) card.JobProgressBar.Value = jobData.ProgressPercentage;

                    if (jobData.AssignedWorkers > 0)
                    {
                        double effectiveRate = Math.Pow(jobData.AssignedWorkers, 0.8);
                        double realSecondsLeft = jobData.WorkRemaining / effectiveRate;
                        time = TimeSpan.FromSeconds(realSecondsLeft);
                        string format = time.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss";
                        card.JobNameLabel.Text = $"{card.BaseName} - {time.ToString(format)}";
                    }
                    else
                    {
                        card.JobNameLabel.Text = $"{card.BaseName} - [IDLE]";
                    }
                }
            }
        }

        private bool ClockIsDayShift(CyclePhase phase)
        {
            return phase != CyclePhase.NightBleed;
        }

        private void SetShiftView(CyclePhase phase)
        {
            _dayShiftView.Visible = ClockIsDayShift(phase);
            _nightShiftView.Visible = !ClockIsDayShift(phase);

            if (phase == CyclePhase.VentRouting && !_ventRoutingModal.Visible)
            {
                double currentPressure = _gameOrchestrator.Boiler.SteamPressure;
                _routingTitleLabel.Text = $"DECOMPRESSION ROUTING - {currentPressure:F0} BAR AVAILABLE";
                _cityVentSlider.Value = 0; _workshopVentSlider.Value = 0;
                _drillVentSlider.Value = 0; _atmosphereVentSlider.Value = 0;
                _atmosphereVentSlider.MaxValue = currentPressure;
                _cityVentSlider.MaxValue = currentPressure;
                _workshopVentSlider.MaxValue = currentPressure;
                _drillVentSlider.MaxValue = currentPressure;

                UpdateVentRoutingValues();
                _ventRoutingModal.Visible = true;
            }
            else if (phase != CyclePhase.VentRouting)
            {
                _ventRoutingModal.Visible = false;
            }
        }

        private void OnGameEventReceived(string message)
        {
            Label logLabel = new Label();

            // Παίρνουμε την ώρα απευθείας από το κεντρικό ρολόι του Orchestrator
            string currentTime = _gameOrchestrator.Clock.GetFormattedTimeOfDay();

            logLabel.Text = $"[{currentTime}] {message}";
            logLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            logLabel.Modulate = new Color(0.8f, 0.8f, 0.8f);

            _logsVBox.AddChild(logLabel);
            _logsVBox.MoveChild(logLabel, 0);
        }
    }
}