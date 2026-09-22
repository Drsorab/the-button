using Godot;
using System;
using System.Reflection;
using PermafrostChieftain.Simulation;

namespace PermafrostChieftain.Orchestration
{
    public partial class AdminPanelController : PanelContainer
    {
        private Button _closeButton;
        private Button _saveButton;
        private HBoxContainer _prefab;

        public override void _Ready()
        {
            // Ανάκτηση των κόμβων (Διορθωμένο το HeaderHBox)
            _closeButton = GetNode<Button>("MainVBox/HeaderHBox/CloseAdminButton");
            _saveButton = GetNode<Button>("MainVBox/SaveSettingsButton");
            _prefab = GetNode<HBoxContainer>("AdminFieldPrefab");

            // Σύνδεση κουμπιών
            _closeButton.Pressed += () => Visible = false;
            _saveButton.Pressed += () =>
            {
                GameSettings.Instance.Save();
                GD.Print("Οι ρυθμίσεις αποθηκεύτηκαν επιτυχώς στο admin_settings.json");
            };

            // Γέμισμα των Tabs με τις απλές μεταβλητές
            PopulateTab("MainVBox/AdminTabContainer/Tab_Timer/ContentVBox", new string[] {
                "ActiveCycleDuration", "VentGracePeriod", "VentModalTimeout", "TwilightDuration", "NightDuration"
            });

            PopulateTab("MainVBox/AdminTabContainer/Tab_Core/ContentVBox", new string[] {
                "StartingCoreTemp", "StartingPressure", "MaxPressure", "BaseCoalBurnRate",
                "ViolentDischargeCasingDamage", "CasingDamageHeatMultiplier", "CasingDamagePressureMultiplier",
                "NightBleedRate", "HullCrackedLeakRate", "SootPressureMultiplier"
            });

            PopulateTab("MainVBox/AdminTabContainer/Tab_Resources/ContentVBox", new string[] {
                "StartingCoal", "StartingScrap", "StartingElectricity"
            });

            PopulateTab("MainVBox/AdminTabContainer/Tab_Population/ContentVBox", new string[] {
                "StartingHealthy", "StartingSick", "StartingGrave", "HazardDeathThreshold",
                "DeathRollProbability", "IntensiveCareHealChance", "NormalHealChance"
            });

            PopulateTab("MainVBox/AdminTabContainer/Tab_Jobs/ContentVBox", new string[] {
                "StokeCoalCost", "StokeHeatProduced", "BracingScrapCost", "BracingRepairAmount",
                "DaySootCleanAmount", "NightSootCleanAmount", "CraftingElectricityCost",
                "CraftingScrapCost", "DamperScrapYield", "MicroBleedPressureDeduct",
                "MicroBleedCityHeatAdd", "MicroBleedDrillCoalAdd", "MicroBleedWorkshopElecAdd",
                "BaseCoalSootReward", "SootMultiplierPerDay", "BaseScrapMaintReward", "MaintMultiplierPerDay"
            });

            // Ειδική συνάρτηση για το λεξικό (Dictionary) των JobConfigs
            PopulateJobDictionaryTab("MainVBox/AdminTabContainer/Tab_Jobs/ContentVBox");
        }

        private void PopulateTab(string containerPath, string[] propertyNames)
        {
            var container = GetNode<VBoxContainer>(containerPath);
            Type settingsType = typeof(GameSettings);

            foreach (string propName in propertyNames)
            {
                PropertyInfo propInfo = settingsType.GetProperty(propName);
                if (propInfo == null) continue;

                // Δημιουργία αντιγράφου του prefab (Κλωνοποίηση)
                var newField = (HBoxContainer)_prefab.Duplicate();
                newField.Visible = true;
                container.AddChild(newField);

                // Ενημέρωση του κειμένου
                newField.GetNode<Label>("PropertyNameLabel").Text = propName;

                // Ανάγνωση τρέχουσας τιμής και ρύθμιση του SpinBox
                var spinBox = newField.GetNode<SpinBox>("PropertyValueSpinBox");
                double currentValue = Convert.ToDouble(propInfo.GetValue(GameSettings.Instance));
                spinBox.Value = currentValue;

                // Όταν ο χρήστης αλλάζει την τιμή, ενημερώνεται αυτόματα το Singleton!
                spinBox.ValueChanged += (newValue) =>
                {
                    object convertedValue = Convert.ChangeType(newValue, propInfo.PropertyType);
                    propInfo.SetValue(GameSettings.Instance, convertedValue);
                };
            }
        }

        // Αυτή η μέθοδος διαβάζει το λεξικό με τα BaseWork και OptimalWorkers για κάθε Job
        private void PopulateJobDictionaryTab(string containerPath)
        {
            var container = GetNode<VBoxContainer>(containerPath);

            foreach (var kvp in GameSettings.Instance.JobConfigs)
            {
                // Προσθήκη ενός απλού τίτλου για διαχωρισμό (π.χ. "--- StokeFurnace ---")
                var title = new Label();
                title.Text = $"\n--- {kvp.Key} ---";
                title.Modulate = new Color(0.5f, 1f, 0.5f); // Πράσινο χρώμα για να ξεχωρίζει
                container.AddChild(title);

                // 1. Πεδίο: Base Work Required
                var workField = (HBoxContainer)_prefab.Duplicate();
                workField.Visible = true;
                container.AddChild(workField);
                workField.GetNode<Label>("PropertyNameLabel").Text = "Base Work Required";

                var workSpin = workField.GetNode<SpinBox>("PropertyValueSpinBox");
                workSpin.Value = kvp.Value.BaseWork;
                workSpin.ValueChanged += (val) => kvp.Value.BaseWork = val;

                // 2. Πεδίο: Optimal Workers
                var workersField = (HBoxContainer)_prefab.Duplicate();
                workersField.Visible = true;
                container.AddChild(workersField);
                workersField.GetNode<Label>("PropertyNameLabel").Text = "Optimal Workers";

                var workersSpin = workersField.GetNode<SpinBox>("PropertyValueSpinBox");
                workersSpin.Value = kvp.Value.OptimalWorkers;
                workersSpin.ValueChanged += (val) => kvp.Value.OptimalWorkers = (int)val;
            }
        }
    }
}