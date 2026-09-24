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
        private HSlider _cityVentSlider, _workshopVentSlider, _drillVentSlider;
        private Label _cityVentValue, _workshopVentValue, _drillVentValue, _retainVentValue;
        private Button _confirmVentButton;

        // Λεξικό για γρήγορη πρόσβαση στα UI στοιχεία των εργασιών
        private Dictionary<JobType, JobCardUI> _jobCards = new();

        private OptionButton _microBleedTargetDropdown;
        private Button _adminPanelButton;
        private PanelContainer _adminPanelModal;
        public override void _Ready()
        {
            _gameOrchestrator = GetNode<GameOrchestrator>("../");

            if (_gameOrchestrator != null && _gameOrchestrator.Clock == null)
            {
                _gameOrchestrator.InitializeSimulation();
            }

            ConnectSimulationSignals();
            BindUiNodes();
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
            var routingVBox = _ventRoutingModal.GetNode<VBoxContainer>("RoutingVBox");

            _routingTitleLabel = routingVBox.GetNode<Label>("RoutingTitleLabel");
            _cityVentSlider = routingVBox.GetNode<HSlider>("CityRoutingHBox/CityVentSlider");
            _cityVentValue = routingVBox.GetNode<Label>("CityRoutingHBox/CityVentValueLabel");

            _workshopVentSlider = routingVBox.GetNode<HSlider>("WorkshopRoutingHBox/WorkshopVentSlider");
            _workshopVentValue = routingVBox.GetNode<Label>("WorkshopRoutingHBox/WorkshopVentValueLabel");

            _drillVentSlider = routingVBox.GetNode<HSlider>("DrillRoutingHBox/DrillVentSlider");
            _drillVentValue = routingVBox.GetNode<Label>("DrillRoutingHBox/DrillVentValueLabel");

            _retainVentValue = routingVBox.GetNode<Label>("RetainRoutingHBox/RetainVentValueLabel");
            _confirmVentButton = routingVBox.GetNode<Button>("ConfirmVentButton");

            // Το κουμπί από το κάτω μενού
            _adminPanelButton = stack.GetNode<Button>("PersistentBottomDock/DockHBox/AdminPanelButton");
            // Το modal (που τώρα έχει και το δικό του script)
            _adminPanelModal = GetNode<PanelContainer>("ModalOverlayLayer/MarginContainer/AdminPanelModal");
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

            _confirmVentButton.Pressed += OnConfirmVentRouting;
            _adminPanelButton.Pressed += () => _adminPanelModal.Visible = true;
            _microBleedTargetDropdown.ItemSelected += (index) =>
            {
                _gameOrchestrator.JobManager.ActiveMicroBleedTarget = (MicroBleedTarget)index;
            };
        }

        private void OnConfirmVentRouting()
        {
            double city = _cityVentSlider.Value;
            double workshop = _workshopVentSlider.Value;
            double drill = _drillVentSlider.Value;

            double total = city + workshop + drill;
            double currentPressure = _gameOrchestrator.Boiler.SteamPressure;

            // Αμυντικός έλεγχος μήπως τα sliders υπερβαίνουν τη διαθέσιμη πίεση
            if (total > currentPressure) return;

            double retain = currentPressure - total;

            // Κλείσιμο του modal και αποστολή των δεδομένων στο Orchestrator
            _ventRoutingModal.Visible = false;
            _gameOrchestrator.ExecuteVentRouting(city, workshop, drill, retain);
        }

        private void UpdateVentRoutingValues()
        {
            double currentPressure = _gameOrchestrator.Boiler.SteamPressure;
            double totalUsed = _cityVentSlider.Value + _workshopVentSlider.Value + _drillVentSlider.Value;

            // Ενημέρωση των ετικετών (Labels) με τις τρέχουσες επιλογές
            _cityVentValue.Text = $"{_cityVentSlider.Value} Bar";
            _workshopVentValue.Text = $"{_workshopVentSlider.Value} Bar";
            _drillVentValue.Text = $"{_drillVentSlider.Value} Bar";

            double retained = Math.Max(0, currentPressure - totalUsed);
            _retainVentValue.Text = $"{retained:F1} Bar";

            // Απενεργοποίηση του κουμπιού επιβεβαίωσης αν η ζήτηση ξεπερνά το διαθέσιμο ρεύμα (πίεση)
            _confirmVentButton.Disabled = totalUsed > currentPressure;
            if (totalUsed > currentPressure)
            {
                _retainVentValue.Text = "INSUFFICIENT PRESSURE";
                _retainVentValue.Modulate = new Color(1, 0, 0);
            }
            else
            {
                _retainVentValue.Modulate = new Color(1, 1, 1);
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
                if (_jobCards.TryGetValue(jobData.Type, out JobCardUI card))
                {
                    card.JobProgressBar.Value = jobData.ProgressPercentage;
                    card.StaffingLabel.Text = $"{jobData.AssignedWorkers} Hands";

                    string timeStatus = "[ΠΑΥΣΗ]"; 
                    if (jobData.AssignedWorkers > 0)
                    {
                        // Διαιρούμε τον υπολειπόμενο χρόνο με την εκθετική απόδοση των εργατών
                        double effectiveRate = Math.Pow(jobData.AssignedWorkers, 0.8);
                        double realSecondsLeft = jobData.WorkRemaining / effectiveRate;

                        // Μετατροπή δευτερολέπτων σε μορφή Λεπτά:Δευτερόλεπτα
                        time = TimeSpan.FromSeconds(realSecondsLeft);
                        if (time.TotalHours >= 1)
                            timeStatus = $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
                        else
                            timeStatus = $"{time.Minutes:D2}:{time.Seconds:D2}";
                    }

                    // Ενημέρωση του τίτλου (π.χ. "Workshop Machining - 02:45")
                    card.JobNameLabel.Text = $"{card.BaseName} - {timeStatus}";

                    card.PlusBtn.Disabled = w.UnassignedReserve <= 0;
                    card.MinusBtn.Disabled = jobData.AssignedWorkers <= 0;
                }
            }
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