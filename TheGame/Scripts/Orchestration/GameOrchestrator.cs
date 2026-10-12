using System;

namespace PermafrostChieftain.Simulation
{
    public class GameOrchestrator
    {
        private readonly BoilerSubsystem _boiler;
        private readonly ThermodynamicsSubsystem _thermo;
        private readonly PopulationSubsystem _population;
        private readonly LogisticsSubsystem _logistics;
        private readonly JobManager _jobManager;

        public TimeSpan DayShiftStartTime { get; private set; } = new TimeSpan(14, 0, 0);
        public bool IsDayShift { get; private set; }

        public double CycleTimerSeconds { get; private set; } = 0.0;
        public bool IsRoutingReady { get; private set; } = false;

        public event Action<string> OnGameEvent;

        // Σύνδεση όλων των υποσυστημάτων (Dependency Injection)
        public GameOrchestrator(
            BoilerSubsystem boiler,
            ThermodynamicsSubsystem thermo,
            PopulationSubsystem population,
            LogisticsSubsystem logistics,
            JobManager jobManager)
        {
            _boiler = boiler;
            _thermo = thermo;
            _population = population;
            _logistics = logistics;
            _jobManager = jobManager;
        }

        public void SetShiftStartTime(TimeSpan newStartTime)
        {
            DayShiftStartTime = newStartTime;
            OnGameEvent?.Invoke($"Η Ημερήσια Βάρδια ορίστηκε στις {newStartTime.ToString(@"hh\:mm")}.");
        }

        // =========================================================
        // ΤΟ ΚΕΝΤΡΙΚΟ LOOP ΤΟΥ ΠΑΙΧΝΙΔΙΟΥ (Εκτελείται κάθε frame)
        // =========================================================
        public void ProcessTick(double deltaTime, DateTime currentRealTime)
        {
            if (deltaTime <= 0.0) return;

            // 1. Ενημέρωση Βάρδιας (Day / Night)
            UpdateShiftStatus(currentRealTime);

            // 2. ΕΚΤΕΛΕΣΗ ΟΙΚΟΝΟΜΙΑΣ (Continuous Jobs)
            // Παίρνουμε πίσω τους ενεργούς Χυτευτές, το Θερμικό Σοκ και το Κάρβουνο των Stokers!
            var (activeSmelters, thermalShock, coalBurnedPerSec) = _jobManager.ProcessContinuousJobs(deltaTime, _logistics, _boiler, _population);

            // 3. Κατανάλωση κάρβουνου για τη Νυχτερινή Συντήρηση (Idle Stance)
            if (!IsDayShift)
            {
                double idleCoal = GameSettings.Instance.NightShiftIdleCoalConsumption * deltaTime;
                double actualIdleCoal = _logistics.ConsumeUpToCoal(idleCoal);

                // Προσθέτουμε το νυχτερινό κάρβουνο στον συνολικό ρυθμό καύσης
                coalBurnedPerSec += (actualIdleCoal / deltaTime);
            }

            // 4. ΘΕΡΜΟΔΥΝΑΜΙΚΗ (Παίρνει κάρβουνο & χυτευτές, αφαιρεί το θερμικό σοκ -> Επιστρέφει Ατμό)
            double steamGenerated = _thermo.ProcessTick(deltaTime, coalBurnedPerSec, activeSmelters, thermalShock);

            // 5. ΛΕΒΗΤΑΣ (Boiler: Δέχεται ατμό, υπολογίζει πίεση, στρες, διαρροές)
            _boiler.AddSteam(steamGenerated);
            _boiler.ProcessTick(deltaTime);

            // 6. ΠΛΗΘΥΣΜΟΣ (Ελέγχει αν αρρωσταίνουν βάσει της θερμοκρασίας της Πόλης)
            _population.ProcessTick(deltaTime, _thermo);

            // 7. Διαχείριση Χρονομέτρων (Routing & Night Bleed)
            UpdateTimersAndBleed(deltaTime);
        }

        private void UpdateTimersAndBleed(double deltaTime)
        {
            if (IsDayShift)
            {
                if (!IsRoutingReady)
                {
                    CycleTimerSeconds += deltaTime;

                    double cycleMaxSeconds = GameSettings.Instance.RoutingCycleDurationMinutes * 60.0;
                    if (CycleTimerSeconds >= cycleMaxSeconds)
                    {
                        IsRoutingReady = true;
                        OnGameEvent?.Invoke("Η πίεση κορυφώθηκε! Το πάνελ δρομολόγησης (Routing) είναι έτοιμο!");
                    }
                }
            }
            else
            {
                // Νυχτερινή Βάρδια: Το Routing ακυρώνεται, ο ατμός φεύγει αργά στην πόλη
                IsRoutingReady = false;
                CycleTimerSeconds = 0.0;

                if (_boiler.SteamPressure > 0)
                {
                    double nightBleed = GameSettings.Instance.NightShiftCityBleedRatePerSec * deltaTime;
                    double actualBleed = Math.Min(nightBleed, _boiler.SteamPressure);
                    _boiler.DeductSteam(actualBleed);

                    // Το στέλνουμε στο Buffer της πόλης για να παλέψει με το κρύο!
                    _thermo.AddSteamToCityBuffer(actualBleed);
                }
            }
        }

        private void UpdateShiftStatus(DateTime currentTime)
        {
            TimeSpan now = currentTime.TimeOfDay;
            TimeSpan shiftEnd = DayShiftStartTime.Add(TimeSpan.FromHours(GameSettings.Instance.DayShiftDurationHours));
            bool wasDay = IsDayShift;

            if (shiftEnd.Days > 0)
            {
                TimeSpan end = new TimeSpan(shiftEnd.Hours, shiftEnd.Minutes, shiftEnd.Seconds);
                IsDayShift = (now >= DayShiftStartTime || now < end);
            }
            else
            {
                IsDayShift = (now >= DayShiftStartTime && now < shiftEnd);
            }

            if (wasDay && !IsDayShift)
            {
                OnGameEvent?.Invoke("Η Ημερήσια Βάρδια έληξε. Ξεκινάει η Νυχτερινή Επιβίωση.");
                // Κλήση θεραπείας κατά τη διάρκεια της νύχτας
                bool hasIntensiveCare = _jobManager.Jobs.ContainsKey(JobType.WorkshopUpkeep) && _jobManager.Jobs[JobType.WorkshopUpkeep].AssignedWorkers > 0; // Απλοποιημένος έλεγχος ιατρείου
                _population.ProcessOvernightHealing(hasIntensiveCare);
            }
            else if (!wasDay && IsDayShift)
            {
                OnGameEvent?.Invoke("Ξημέρωσε! Η Ημερήσια Βάρδια ξεκινάει.");
            }
        }

        // =========================================================
        // ΤΟ ΚΟΥΜΠΙ ROUTING: Εκτελείται από το UI κάθε 108 λεπτά
        // Οι παράμετροι πρέπει να αθροίζουν σε 1.0 (100%)
        // =========================================================
        public void ExecuteRoutingPanel(double drillPct, double cityPct, double powerPct, double keepPct)
        {
            if (!IsRoutingReady || !IsDayShift)
            {
                OnGameEvent?.Invoke("ΑΡΝΗΣΗ: Το σύστημα δρομολόγησης δεν είναι έτοιμο ή είναι Νύχτα.");
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

            // Εφαρμογή αποδόσεων (Yields)
            double coalGenerated = steamForDrill * GameSettings.Instance.SteamToCoalYield;
            double cityHeatGenerated = steamForCity * GameSettings.Instance.SteamToCityHeatYield;
            double electricityGenerated = steamForPower * GameSettings.Instance.SteamToElectricityYield;

            // Αφαίρεση πίεσης
            _boiler.DeductSteam(steamToConsume);

            // Ενεργοποίηση του Drill Buff στον JobManager
            _jobManager.AddDrillBuff(steamForDrill);

            // Προσθήκη Πόρων
            _logistics.AddCoal(coalGenerated);
            _logistics.AddElectricity(electricityGenerated);
            // Στέλνουμε τον ατμό κατευθείαν στο σύστημα της πόλης
            _thermo.AddSteamToCityBuffer(steamForCity);
            IsRoutingReady = false;
            CycleTimerSeconds = 0.0;

            OnGameEvent?.Invoke($"ΕΚΤΟΝΩΣΗ: Προστέθηκαν {coalGenerated:F0}kg άνθρακα, {electricityGenerated:F0}kW ρεύμα, +{cityHeatGenerated:F1}°C στην πόλη.");
        }
    }
}