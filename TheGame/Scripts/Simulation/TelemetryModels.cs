using System.Collections.Generic;

namespace PermafrostChieftain.Simulation
{
    public struct BoilerTelemetry
    {
        public double SteamPressure;
        public double StressLevel;       // ΝΕΟ: Απαραίτητο για το UI πανικού
        public double CasingIntegrity;
        public double SootFraction;
        public bool IsHullCracked;
        public int AvailableFuses;

        // Στοιχεία για την επικοινωνία φάσεων με το UI
        public CyclePhase CurrentPhase;
        public double SecondsUntilNextVent;
        public string FormattedTimeOfDay; // ΝΕΟ: Η ώρα π.χ. "14:30" από το ρολόι
    }

    public struct ThermodynamicsTelemetry
    {
        public double CoreTemperature;
        public double CityTemperature;
        public double TargetCityTemperature; // ΝΕΟ
        public double CitySteamBuffer;       // ΝΕΟ
        public double EstimatedCityHeatHours; // ΝΕΟ: Πόσες Ώρες θα αντέξει η ζέστη
    }

    public struct WorkforceTelemetry
    {
        public int TotalPopulation;
        public int HealthyWorkers;
        public int SickWorkers;
        public int GraveWorkers;
        public int DeadWorkers;          // ΝΕΟ
        public int UnassignedReserve;
        public CityLivingStage CurrentCityStage; // ΝΕΟ: Για να δείχνεις στο UI "Παγωνιά" ή "Βιώσιμα"
    }

    public struct LogisticsTelemetry
    {
        public double CoalStockpile;
        public double RawScrap;          // Μετονομάστηκε από ScrapStockpile για συνέπεια
        public double RefinedMetal;      // ΝΕΟ
        public double Electricity;
        public int CraftedMetalPatches;  // ΝΕΟ: Για τα ρήγματα
    }

    // Νέα δομή για να διαβάζει το UI πόσος χρόνος μένει σε κάθε εργασία
    public struct JobTelemetry
    {
        public JobType Type;
        public int AssignedWorkers;
        public double WorkRemaining;
        public double BaseWorkRequired;
        public bool IsContinuous;        // ΝΕΟ: Το UI το διαβάζει για να κρύψει ή να δείξει τη μπάρα!

        public double ProgressPercentage =>
            BaseWorkRequired > 0 ? 100.0 * (1.0 - (WorkRemaining / BaseWorkRequired)) : 0.0;
    }

    // =========================================================
    // ΝΕΟ: Το Κεντρικό Πακέτο Τηλεμετρίας 
    // =========================================================
    public class GameTelemetrySnapshot
    {
        public BoilerTelemetry Boiler;
        public ThermodynamicsTelemetry Thermo;
        public WorkforceTelemetry Workforce;
        public LogisticsTelemetry Logistics;
        public List<JobTelemetry> Jobs;
    }
}