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
                // Αν έχεις υλοποιήσει το Save() στο GameSettings
                // GameSettings.Instance.Save(); 
                GD.Print("Οι ρυθμίσεις εφαρμόστηκαν (Runtime)!");
            };

            // ========================================================
            // ΑΝΤΙΣΤΟΙΧΙΣΗ ΜΕΤΑΒΛΗΤΩΝ ΜΕ ΤΑ TABS ΤΟΥ GODOT
            // ========================================================

            // Tab 1: Timer (Ημέρα, Νύχτα & Κύκλοι)
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Timer/ContentVBox", new Dictionary<string, string[]> {
                { "SHIFTS (SECONDS)", new[] { "DayShiftDurationHours", "NightDuration", "ActiveCycleDuration", "TwilightDuration" } },
                { "MODAL TIMERS", new[] { "VentGracePeriod", "VentModalTimeout", "RoutingCycleDurationMinutes" } }
            });

            // Tab 2: Core (Θερμοδυναμική)
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Core/ContentVBox", new Dictionary<string, string[]> {
                { "TEMPERATURE LIMITS", new[] { "AmbientWeatherTemperature", "OptimalCoreTemp", "BoilingPointThreshold" } },
                { "THERMODYNAMICS", new[] { "HeatGainPerKgCoal", "CoreThermalMass", "LatentHeatSteamConversionRatio", "TurbineThermalShockPerBar", "SmeltingHeatDrainPerWorker" } },
                { "COOLING RATES", new[] { "GeneratorWeatherCoolingCoefficient", "CityWeatherCoolingCoefficient" } }
            });

            // Tab 3: Hull (Πίεση & Ζημιές)
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Hull/ContentVBox", new Dictionary<string, string[]> {
                { "PRESSURE LIMITS", new[] { "MaxSafePressureAtFullIntegrity", "IntegrityToSafePressureMultiplier", "BaseSteamGenerationRate" } },
                { "STRESS & DAMAGE", new[] { "StressAccumulationPerOverpressure", "StressToDamageThreshold", "IntegrityDamagePerStressBreak", "StressBreakGracePeriod", "BaseStressReliefRate" } },
                { "HULL CRACKS", new[] { "BaseCrackProbabilityPerTick", "InitialCrackLeakRate", "CrackLeakEscalationRate" } }
            });

            // Tab 4: Soot (Καπνιά)
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Soot/ContentVBox", new Dictionary<string, string[]> {
                { "SOOT GENERATION", new[] { "SootAccumulationPerKgCoal", "MaxSootCapacity" } },
                { "SOOT PENALTIES", new[] { "SootPenalty_FurnaceEfficiencyLoss", "SootPenalty_StressAccumulationMultiplier" } },
                { "CLEANING", new[] { "CleanSootWorkRequired", "CoalYieldFromCleaningSoot", "SootRemovedPerClean" } }
            });

            // Tab 5: Vent (Routing & Νύχτα)
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Vent/ContentVBox", new Dictionary<string, string[]> {
                { "ROUTING YIELDS", new[] { "SteamToCoalYield", "SteamToElectricityYield", "SteamToCityHeatYield" } },
                { "NIGHT BLEED", new[] { "NightShiftSteamBleedRate", "NightShiftCityBleedRatePerSec" } }
            });

            // Tab 6: Resources (Logistics & Αποθέματα)
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Resources/ContentVBox", new Dictionary<string, string[]> {
                { "STARTING STOCKPILES", new[] { "StartingCoal", "StartingRawScrap", "StartingRefinedMetal", "StartingElectricity" } },
                { "IDLE CONSUMPTION", new[] { "NightShiftIdleCoalConsumption" } }
            });

            // Tab 7: Population (Πληθυσμός & Υγεία)
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Population/ContentVBox", new Dictionary<string, string[]> {
                { "INITIAL WORKFORCE", new[] { "StartingHealthy", "StartingSick", "StartingGrave" } },
                { "CITY TEMP STAGES", new[] { "TempStage_Comfortable", "TempStage_Warm", "TempStage_Livable", "TempStage_Cold" } },
                { "SICKNESS RATES", new[] { "SicknessRate_Comfortable", "SicknessRate_Warm", "SicknessRate_Livable", "SicknessRate_Cold", "SicknessRate_Freezing" } },
                { "HEALING & HAZARDS", new[] { "DeathRollProbability", "NormalHealChance", "IntensiveCareHealChance" } }
            });

            // Tab 8: Jobs (Ρυθμοί Εργασίας)
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Jobs/ContentVBox", new Dictionary<string, string[]> {
                { "CONTINUOUS JOBS", new[] { "StokerCoalConsumptionPerWorker", "CoalMiningBaseRatePerWorker", "CoalMiningDrillBuffMultiplier", "SteamDrillBuffDurationPerBar", "ScavengeScrapBaseRatePerWorker", "SmeltingRawScrapCostPerWorker", "SmeltingRefinedMetalRatePerWorker", "TurbineSteamCostPerWorker", "TurbinePowerPerWorker" } },
                { "WORKSHOP UPKEEP", new[] { "UpkeepSafetyBonusPerWorker", "MaxUpkeepSafetyBonus" } },
                { "DISCRETE REPAIRS", new[] { "PatchWorkRequired", "NightRepairWorkRequired", "NightRepairRefinedMetalCost", "NightRepairIntegrityRestored" } }
            });

            // Tab 9: Equipment (Χρησιμοποιείται πλέον για Fuses & Crafting)
            PopulateTabWithGroups("MainVBox/AdminTabContainer/Tab_Equipment/ContentVBox", new Dictionary<string, string[]> {
                { "WORKSHOP CRAFTING", new[] { "CraftMetalPatchWorkRequired", "CraftMetalPatchRefinedMetalCost", "WorkshopElectricityConsumptionPerWorker", "WorkshopElectricitySpeedMultiplier" } },
                { "FUSE SYSTEM", new[] { "MaxFuses", "CraftFuseWorkRequired", "CraftFuseRefinedMetalCost", "FuseBlowIntegrityDamage", "FuseBlowPressureReleasePct" } }
            });
        }

        private void PopulateTabWithGroups(string containerPath, Dictionary<string, string[]> groups)
        {
            var container = GetNode<VBoxContainer>(containerPath);
            Type settingsType = typeof(GameSettings);

            foreach (var group in groups)
            {
                // Προσθήκη κενού χώρου
                if (container.GetChildCount() > 0)
                {
                    var spacer = new Control { CustomMinimumSize = new Vector2(0, 15) };
                    container.AddChild(spacer);
                }

                // Προσθήκη Τίτλου Ομάδας
                var title = new Label();
                title.Text = $"--- {group.Key} ---";
                title.Modulate = new Color(0.4f, 0.8f, 1f);
                container.AddChild(title);

                // Γέμισμα των μεταβλητών της ομάδας
                foreach (string propName in group.Value)
                {
                    PropertyInfo propInfo = settingsType.GetProperty(propName);
                    if (propInfo == null)
                    {
                        GD.PrintErr($"[AdminPanel] Property '{propName}' not found in GameSettings!");
                        continue;
                    }

                    var newField = (HBoxContainer)_prefab.Duplicate();
                    newField.Visible = true;
                    container.AddChild(newField);

                    newField.GetNode<Label>("PropertyNameLabel").Text = propName;

                    var spinBox = newField.GetNode<SpinBox>("PropertyValueSpinBox");
                    double currentValue = Convert.ToDouble(propInfo.GetValue(GameSettings.Instance));
                    spinBox.Value = currentValue;

                    // Λόγω Reflection, βάζουμε όρια ανάλογα με τον τύπο
                    if (propInfo.PropertyType == typeof(int))
                    {
                        spinBox.Step = 1;
                        spinBox.Rounded = true;
                    }
                    else
                    {
                        spinBox.Step = 0.01;
                    }

                    spinBox.ValueChanged += (newValue) =>
                    {
                        object convertedValue = Convert.ChangeType(newValue, propInfo.PropertyType);
                        propInfo.SetValue(GameSettings.Instance, convertedValue);
                    };
                }
            }
        }
    }
}