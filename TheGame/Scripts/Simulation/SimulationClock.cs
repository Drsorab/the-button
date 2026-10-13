using System;

namespace PermafrostChieftain.Simulation
{
    public enum CyclePhase
    {
        DayActive,         // Building pressure (108 mins)
        VentPending,       // 108 mins reached. Button available. Grace period active.
        VentRouting,       // Button pressed. Modal open. 10-second timeout running.
        TwilightWindDown,  // Remaining 48 mins of the 8-hour shift after the 4th vent
        NightBleed         // 16-hour night shift
    }

    public class SimulationClock
    {
        public double ActiveCycleDuration => GameSettings.Instance.ActiveCycleDuration;
        public double VentGracePeriod => GameSettings.Instance.VentGracePeriod;
        public double TwilightDuration => GameSettings.Instance.TwilightDuration;
        public double VentModalTimeout => GameSettings.Instance.VentModalTimeout;      // Time allowed to make a choice in the modal
        public double NightDuration => GameSettings.Instance.NightDuration;      // 16 hours
        public bool IsDayShift => CurrentPhase != CyclePhase.NightBleed;
        public double CurrentTimeOfDaySeconds { get; private set; } = 28800.0; // Starts at 08:00
        public double PhaseElapsedSeconds { get; private set; } = 0.0;

        public int TotalDaysCompleted { get; private set; } = 0;
        public int ActiveCyclesCompletedToday { get; private set; } = 0; // Tracks 0 to 4 vents

        public CyclePhase CurrentPhase { get; private set; } = CyclePhase.DayActive;

        // Signals for GameOrchestrator to handle penalties
        public event Action VentGracePeriodExpired;
        public event Action VentModalTimeoutExpired;

        public bool Tick(double deltaTime, out bool phaseChanged)
        {
            phaseChanged = false;
            if (deltaTime <= 0.0) return false;

            PhaseElapsedSeconds += deltaTime;

            // The world clock pauses while the player is routing pressure in the modal, 
            // but the internal PhaseElapsedSeconds still counts the 10-second UI timeout.
            if (CurrentPhase != CyclePhase.VentRouting)
            {
                CurrentTimeOfDaySeconds = (CurrentTimeOfDaySeconds + deltaTime) % 86400.0;
            }

            switch (CurrentPhase)
            {
                case CyclePhase.DayActive:
                    if (PhaseElapsedSeconds >= ActiveCycleDuration)
                    {
                        PhaseElapsedSeconds = 0.0;
                        CurrentPhase = CyclePhase.VentPending;
                        phaseChanged = true;
                        return true;
                    }
                    break;

                case CyclePhase.VentPending:
                    if (PhaseElapsedSeconds >= VentGracePeriod)
                    {
                        // Player forgot to press the button. Trigger fuse/damage logic.
                        PhaseElapsedSeconds = 0.0;
                        VentGracePeriodExpired?.Invoke();
                        AdvanceAfterVent(out phaseChanged);
                        return true;
                    }
                    break;

                case CyclePhase.VentRouting:
                    if (PhaseElapsedSeconds >= VentModalTimeout)
                    {
                        // Player opened the modal but took too long. Trigger default vent.
                        PhaseElapsedSeconds = 0.0;
                        VentModalTimeoutExpired?.Invoke();
                        AdvanceAfterVent(out phaseChanged);
                        return true;
                    }
                    break;

                case CyclePhase.TwilightWindDown:
                    if (PhaseElapsedSeconds >= TwilightDuration)
                    {
                        CurrentPhase = CyclePhase.NightBleed;
                        PhaseElapsedSeconds = 0.0;
                        phaseChanged = true;
                        return true;
                    }
                    break;

                case CyclePhase.NightBleed:
                    if (PhaseElapsedSeconds >= NightDuration)
                    {
                        TotalDaysCompleted++;
                        ActiveCyclesCompletedToday = 0;
                        CurrentPhase = CyclePhase.DayActive;
                        PhaseElapsedSeconds = 0.0;
                        phaseChanged = true;
                        return true;
                    }
                    break;
            }

            return false;
        }

        // Called by UIOrchestrator when the player clicks the Vent Actuator Button
        public void TriggerVentModal()
        {
            if (CurrentPhase == CyclePhase.VentPending)
            {
                CurrentPhase = CyclePhase.VentRouting;
                PhaseElapsedSeconds = 0.0; // Start the 10-second modal countdown
            }
        }

        // Called by UIOrchestrator upon confirming routing, or called automatically on timeouts
        public void AdvanceAfterVent(out bool phaseChanged)
        {
            ActiveCyclesCompletedToday++;
            PhaseElapsedSeconds = 0.0;
            phaseChanged = true;

            if (ActiveCyclesCompletedToday >= 4)
            {
                CurrentPhase = CyclePhase.TwilightWindDown;
            }
            else
            {
                CurrentPhase = CyclePhase.DayActive;
            }
        }

        public string GetFormattedTimeOfDay()
        {
            int hours = (int)(CurrentTimeOfDaySeconds / 3600.0);
            int minutes = (int)((CurrentTimeOfDaySeconds % 3600.0) / 60.0);
            return $"{hours:D2}:{minutes:D2}";
        }

        public double GetSecondsUntilNextVent()
        {
            if (CurrentPhase != CyclePhase.DayActive) return 0.0;
            return Math.Max(0.0, ActiveCycleDuration - PhaseElapsedSeconds);
        }

        public void SetCosmeticTimeOfDay(TimeSpan newTime)
        {
            CurrentTimeOfDaySeconds = newTime.TotalSeconds;
        }


        public void StartAtMiddleOfDay()
        {
            // 1. Βάζουμε το παιχνίδι στην τελευταία φάση της ημέρας
            CurrentPhase = CyclePhase.TwilightWindDown;
            ActiveCyclesCompletedToday = 4;

            // 2. Η φάση TwilightWindDown διαρκεί 48 λεπτά (2880 δευτερόλεπτα).
            // Θέλουμε να αφήσουμε 45 λεπτά (2700 δευτερόλεπτα) στον παίκτη.
            // Επομένως, έχουν περάσει 3 λεπτά (180 δευτερόλεπτα) ΑΠΟ ΤΗΝ ΕΝΑΡΞΗ ΑΥΤΗΣ ΤΗΣ ΦΑΣΗΣ.
            PhaseElapsedSeconds = 180.0;
        }
    }
}