using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;
using PermafrostChieftain.Simulation;

namespace PermafrostChieftain.Orchestration
{
    public partial class AdminPanelController : PanelContainer
    {
        private Button _closeButton;
        private Button _saveButton;
        private HBoxContainer _prefab;
        private Button _speedUpBtn;
        private Button _resetTimeBtn;
        private GameOrchestrator _gameOrchestrator;

        public override void _Ready()
        {
            _gameOrchestrator = GetTree().Root.GetNode<GameOrchestrator>("TheGame");
            _closeButton = GetNode<Button>("MainVBox/HeaderHBox/CloseAdminButton");
            _saveButton = GetNode<Button>("MainVBox/SaveSettingsButton");
            _prefab = GetNode<HBoxContainer>("AdminFieldPrefab");
            _speedUpBtn = GetNode<Button>("MainVBox/HeaderHBox/SpeedUpBtn");
            _resetTimeBtn = GetNode<Button>("MainVBox/HeaderHBox/ResetTimeBtn");

            _speedUpBtn.Pressed += () => {
                if (_gameOrchestrator != null)
                {
                    _gameOrchestrator.TimeMultiplier *= 2.0;
                    GD.Print($"Χρόνος επιταχύνθηκε: x{_gameOrchestrator.TimeMultiplier}");
                }
            };

            _resetTimeBtn.Pressed += () => {
                if (_gameOrchestrator != null)
                {
                    _gameOrchestrator.TimeMultiplier = 1.0;
                    GD.Print("Χρόνος επανήλθε στο x1");
                }
            };

            _closeButton.Pressed += () => Visible = false;
            _saveButton.Pressed += () =>
            {
                GameSettings.Instance.Save();
                GD.Print("Οι ρυθμίσεις αποθηκεύτηκαν επιτυχώς στο admin_settings.json");
            };

            // Γέμισμα του Tab 1: Timer
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Timer/ContentVBox", new Dictionary<string, string[]> {
                { "MASTER CLOCK", new[] { "ActiveCycleDuration", "TwilightDuration", "NightDuration" } },
                { "UI & MODALS", new[] { "VentGracePeriod", "VentModalTimeout" } }
            });

            // Γέμισμα του Tab 2: Core
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Core/ContentVBox", new Dictionary<string, string[]> {
                { "STARTING CONDITIONS", new[] { "StartingCoreTemp", "StartingPressure", "IgnitionCoalCost",
                        "ShutdownTempThreshold",
                        "InactiveCoolingRate" , "AmbientTemperature" } },
                { "THERMODYNAMICS", new[] { "OptimalCoreTemp", "BoilingPointThreshold", "HeatGainPerKgCoal", "AmbientHeatLossRate" } },
                { "PRESSURE DYNAMICS", new[] { "MaxPressure", "BasePressureGenerationRate", "NightBleedRate" } }
            });

            // Γέμισμα του Tab 3: Hull
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Hull/ContentVBox", new Dictionary<string, string[]> {
                { "DAMAGE THRESHOLDS", new[] { "PressureDamageThreshold", "HeatDamageThreshold" } },
                { "DAMAGE RATES", new[] { "CasingDamageHeatMultiplier", "CasingDamagePressureMultiplier", "ViolentDischargeCasingDamage", "HullCrackedLeakRate", "StartingHullIntegrity" } }
            });

            // Γέμισμα του Tab 4: Soot
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Soot/ContentVBox", new Dictionary<string, string[]> {
                { "GENERATION & INSULATION", new[] { "BaseSootGenerationRate", "SmolderingSootPenalty", "SootInsulationMultiplier" } },
                { "BLOWOUT MECHANIC", new[] { "BlowoutPressureCost", "BlowoutHullDamage", "BlowoutSootCleared" } },
                { "JOB CLEANING", new[] { "DaySootCleanAmount", "NightSootCleanAmount" } }
            });

            // Γέμισμα του Tab 5: Vent
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Vent/ContentVBox", new Dictionary<string, string[]> {
                { "ROUTING MULTIPLIERS", new[] { "VentElectricityMultiplier", "VentCoalMultiplier", "VentScrapMultiplier", "VentRoutingSootPenalty" } },
                { "MICRO-BLEED", new[] { "MicroBleedPressureDeduct", "MicroBleedCityHeatAdd", "MicroBleedDrillCoalAdd", "MicroBleedWorkshopElecAdd" } }
            });

            // Γέμισμα του Tab 6: Resources
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Resources/ContentVBox", new Dictionary<string, string[]> {
                { "STARTING STOCKPILES", new[] { "StartingCoal", "StartingScrap", "StartingElectricity" } },
                { "NIGHT SHIFT LOGISTICS", new[] { "NightElectricityDrainRate", "NightHeatConversionEfficiency" } }
            });

            // Γέμισμα του Tab 7: Population
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Population/ContentVBox", new Dictionary<string, string[]> {
                { "STARTING POPULATION", new[] { "StartingHealthy", "StartingSick", "StartingGrave" } },
                { "HEALTH & HAZARDS", new[] { "HazardDeathThreshold", "DeathRollProbability", "IntensiveCareHealChance", "NormalHealChance" } }
            });

            // Γέμισμα του Tab 8: Jobs
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Jobs/ContentVBox", new Dictionary<string, string[]> {
                { "FURNACE & HULL", new[] { "StokeCoalPerWorkerPerHour", "BracingScrapCost", "BracingRepairAmount" } },
                { "REWARDS & SCAVENGING", new[] { "DamperScrapYield", "BaseCoalSootReward", "SootMultiplierPerDay", "BaseScrapMaintReward", "MaintMultiplierPerDay" } }
            });

            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Equipment/ContentVBox", new Dictionary<string, string[]> {
                { "SHOVELS (FURNACE)", new[] { "ShovelCoalBonusMultiplier", "ShovelHeatDegradationMultiplier", "ShovelBaseDegradationPerSec" } },
                { "BATTERIES (RELAY)", new[] { "BatteryCapacityBonus", "BatteryDegradationPerCycle" } },
                { "RESPIRATORS (HAZARD)", new[] { "RespiratorHazardReduction", "RespiratorClogRatePerSec", "RespiratorDangerThreshold", "RespiratorBrokenHazardPenalty" } },
                { "WIRE BRUSHES (SCOUR)", new[] { "BrushWorkReductionMultiplier", "BrushYieldMultiplier", "BrushDegradationPerSec" } },
                { "NIGHT REPAIR", new[] { "NightShiftRepairRate" } }
            });

            PopulateJobDictionaryTab("MainVBox/AdminTabContainer/Tab_Jobs/ContentVBox");
        }

        // ΝΕΑ ΣΥΝΑΡΤΗΣΗ: Υποστηρίζει ομάδες (Groups), επικεφαλίδες (Headers) και αποστάσεις (Spacers)
        private void PopulateTabWithGroups(string containerPath, Dictionary<string, string[]> groups)
        {
            var container = GetNode<VBoxContainer>(containerPath);
            Type settingsType = typeof(GameSettings);

            foreach (var group in groups)
            {
                // Προσθήκη κενού χώρου πριν από κάθε νέα ομάδα (εκτός από την πρώτη)
                if (container.GetChildCount() > 0)
                {
                    var spacer = new Control { CustomMinimumSize = new Vector2(0, 15) };
                    container.AddChild(spacer);
                }

                // Προσθήκη Τίτλου Ομάδας
                var title = new Label();
                title.Text = $"--- {group.Key} ---";
                title.Modulate = new Color(0.4f, 0.8f, 1f); // Απαλό γαλάζιο χρώμα για τον τίτλο
                container.AddChild(title);

                // Γέμισμα των μεταβλητών της ομάδας
                foreach (string propName in group.Value)
                {
                    PropertyInfo propInfo = settingsType.GetProperty(propName);
                    if (propInfo == null) continue;

                    var newField = (HBoxContainer)_prefab.Duplicate();
                    newField.Visible = true;
                    container.AddChild(newField);

                    newField.GetNode<Label>("PropertyNameLabel").Text = propName;

                    var spinBox = newField.GetNode<SpinBox>("PropertyValueSpinBox");
                    double currentValue = Convert.ToDouble(propInfo.GetValue(GameSettings.Instance));
                    spinBox.Value = currentValue;

                    spinBox.ValueChanged += (newValue) =>
                    {
                        object convertedValue = Convert.ChangeType(newValue, propInfo.PropertyType);
                        propInfo.SetValue(GameSettings.Instance, convertedValue);
                    };
                }
            }
        }

        private void PopulateJobDictionaryTab(string containerPath)
        {
            var container = GetNode<VBoxContainer>(containerPath);

            var spacer = new Control { CustomMinimumSize = new Vector2(0, 25) };
            container.AddChild(spacer);

            var mainTitle = new Label { Text = "=== JOB BASE WORK & OPTIMAL WORKERS ===", Modulate = new Color(1f, 0.8f, 0.2f) };
            container.AddChild(mainTitle);

            foreach (var kvp in GameSettings.Instance.JobConfigs)
            {
                var title = new Label { Text = $"\n--- {kvp.Key} ---", Modulate = new Color(0.5f, 1f, 0.5f) };
                container.AddChild(title);

                var workField = (HBoxContainer)_prefab.Duplicate();
                workField.Visible = true;
                container.AddChild(workField);
                workField.GetNode<Label>("PropertyNameLabel").Text = "Base Work Required";
                var workSpin = workField.GetNode<SpinBox>("PropertyValueSpinBox");
                workSpin.Value = kvp.Value.BaseWork;
                workSpin.ValueChanged += (val) => kvp.Value.BaseWork = val;

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