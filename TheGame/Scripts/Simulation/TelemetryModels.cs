using System.Collections.Generic;

namespace PermafrostChieftain.Simulation
{
    public struct BoilerTelemetry
    {
        public double SteamPressure;
        public double CasingIntegrity;
        public double SootFraction;
        public int AvailableFuses;
        public bool IsHullCracked;

        // Στοιχεία για την επικοινωνία φάσεων με το UI
        public CyclePhase CurrentPhase;
        public double SecondsUntilNextVent;
    }

    public struct ThermodynamicsTelemetry
    {
        public double CoreTemperature;
        public double CityTemperature;
        public DraftStance DraftStance;
    }

    public struct WorkforceTelemetry
    {
        public int TotalPopulation;
        public int HealthyWorkers;
        public int SickWorkers;
        public int GraveWorkers;
        public int UnassignedReserve;
    }
    public struct LogisticsTelemetry
    {
        public double CoalStockpile;
        public double ScrapStockpile;
        public double Electricity; 
        public double WorkshopHazardLevel;
        public double RetrofitProgress;
    }

    // Νέα δομή για να διαβάζει το UI πόσος χρόνος μένει σε κάθε εργασία
    public struct JobTelemetry
    {
        public JobType Type;
        public int AssignedWorkers;
        public double WorkRemaining;
        public double BaseWorkRequired;

        public double ProgressPercentage =>
            BaseWorkRequired > 0 ? 100.0 * (1.0 - (WorkRemaining / BaseWorkRequired)) : 0.0;
    }
}