using System;
using System.Collections.Generic;

namespace PermafrostChieftain.Simulation
{
    public class GameSettings
    {
        private static GameSettings _instance;
        public static GameSettings Instance => _instance ??= new GameSettings();

        public double StressBreakGracePeriod { get; set; } = 30.0;
        public double BaseStressReliefRate { get; set; } = 1.0;

        public double StokerCoalConsumptionPerWorker { get; set; } = 0.2;

        // Το Θερμικό Σοκ: Πόσους βαθμούς χάνει ο πυρήνας για κάθε 1 Bar που ρουφάει η Τουρμπίνα χειροκίνητα
        public double TurbineThermalShockPerBar { get; set; } = 2.0;

        // Η θερμική μάζα της γεννήτριας (όσο μεγαλύτερο, τόσο πιο αργά ζεσταίνεται και κρυώνει)
        public double CoreThermalMass { get; set; } = 25.0;

        // Όταν περάσουμε το σημείο βρασμού, το 60% της καθαρής ενέργειας γίνεται Ατμός 
        // και μόνο το 40% πάει στο να ανεβάσει κι άλλο τη θερμοκρασία του πυρήνα!
        public double LatentHeatSteamConversionRatio { get; set; } = 0.6;
        // ==========================================
        // 1. ΚΑΙΡΟΣ ΚΑΙ ΠΕΡΙΒΑΛΛΟΝ (WEATHER & AMBIENT)
        // ==========================================
        // Η εξωτερική θερμοκρασία που "παλεύει" να παγώσει τη γεννήτρια και την πόλη.
        public double AmbientWeatherTemperature { get; set; } = -30.0;

        // Πόσο γρήγορα το κρύο διαπερνά τη μόνωση της πόλης (City Cooling Rate)
        public double CityWeatherCoolingCoefficient { get; set; } = 0.05;

        // Πόσο γρήγορα το κρύο ρίχνει τη θερμοκρασία του λέβητα όταν δεν καίει κάρβουνο
        public double GeneratorWeatherCoolingCoefficient { get; set; } = 0.15;

        // ==========================================
        // 2. ΘΕΡΜΟΚΡΑΣΙΑ ΠΟΛΗΣ - ΟΙ 5 ΒΑΘΜΙΔΕΣ (CITY LIVING STAGES)
        // ==========================================
        // Αυτά είναι τα όρια (Thresholds) για τις 5 καταστάσεις διαβίωσης.
        public double TempStage_Comfortable { get; set; } = 15.0;  // > 15°C (Άνετα)
        public double TempStage_Warm { get; set; } = 5.0;          // 5°C έως 15°C (Ζεστά)
        public double TempStage_Livable { get; set; } = -5.0;      // -5°C έως 5°C (Βιώσιμα)
        public double TempStage_Cold { get; set; } = -15.0;        // -15°C έως -5°C (Κρύο)
        // Κάτω από -15°C θεωρείται Freezing (Παγωνιά)

        // Πιθανότητες ασθένειας ανά λεπτό (βάσει της βαθμίδας διαβίωσης)
        public double SicknessRate_Comfortable { get; set; } = 0.0;   // 0% κίνδυνος
        public double SicknessRate_Warm { get; set; } = 0.01;         // Πολύ μικρός κίνδυνος
        public double SicknessRate_Livable { get; set; } = 0.05;      // Μέτριος κίνδυνος
        public double SicknessRate_Cold { get; set; } = 0.15;         // Υψηλός κίνδυνος
        public double SicknessRate_Freezing { get; set; } = 0.35;     // Ακραίος κίνδυνος (Θάνατοι)

        // ==========================================
        // 3. ΓΕΝΝΗΤΡΙΑ: ΘΕΡΜΟΤΗΤΑ ΚΑΙ ΑΤΜΟΣ (GENERATOR CORE & STEAM)
        // ==========================================
        public double HeatGainPerKgCoal { get; set; } = 85.0;
        public double BoilingPointThreshold { get; set; } = 250.0;
        public double OptimalCoreTemp { get; set; } = 750.0;

        // Πόσος ατμός (πίεση) παράγεται ανά δευτερόλεπτο όταν βράζει το νερό
        public double BaseSteamGenerationRate { get; set; } = 0.5;

        // Πόση ενέργεια "κλέβει" το χυτήριο (Smelting) από τον πυρήνα ανά εργάτη
        public double SmeltingHeatDrainPerWorker { get; set; } = 2.5;

        // ==========================================
        // 4. ΠΙΕΣΗ, ΣΤΡΕΣ ΚΑΙ ΖΗΜΙΑ (PRESSURE, STRESS & INTEGRITY)
        // ==========================================
        // Η μέγιστη πίεση που μπορεί να κρατήσει μια γεννήτρια με 100% Integrity χωρίς να παίρνει Στρες
        public double MaxSafePressureAtFullIntegrity { get; set; } = 100.0;

        // Πόσο Στρες μαζεύει η μηχανή ανά δευτερόλεπτο, για κάθε 1 Bar πάνω από το ασφαλές όριο
        public double StressAccumulationPerOverpressure { get; set; } = 0.02;

        // Το όριο του Στρες. Όταν το φτάσει, παθαίνει μόνιμη ζημιά (Integrity Damage)
        public double StressToDamageThreshold { get; set; } = 100.0;
        public double IntegrityDamagePerStressBreak { get; set; } = 5.0; // Χάνει 5% αντοχή

        // Όσο χαμηλότερο το Integrity, τόσο κατεβαίνει το όριο ασφαλούς πίεσης
        // Π.χ. αν είναι 0.5, τότε στα 50% Integrity, η ασφαλής πίεση πέφτει στα 50 Bar!
        public double IntegrityToSafePressureMultiplier { get; set; } = 1.0;

        // ==========================================
        // 5. ΡΗΓΜΑΤΑ ΚΑΙ ΔΙΑΡΡΟΕΣ (HULL CRACKS & LEAKS)
        // ==========================================
        // Βασική πιθανότητα να ανοίξει ρήγμα ανά δευτερόλεπτο (αυξάνεται όσο πέφτει το Integrity)
        public double BaseCrackProbabilityPerTick { get; set; } = 0.0001;

        // Πόσος ατμός χάνεται στην αρχή του ρήγματος (Μικρή διαρροή)
        public double InitialCrackLeakRate { get; set; } = 0.1;

        // Πόσο γρήγορα χειροτερεύει η διαρροή όσο μένει αδιόρθωτη (Κλιμάκωση)
        public double CrackLeakEscalationRate { get; set; } = 0.05;

        // ==========================================
        // 7. ΑΠΟΘΕΜΑΤΑ ΚΑΙ ΛΟΓΙΣΤΙΚΗ (LOGISTICS & STARTING RESOURCES)
        // ==========================================
        // Αρχικό κάρβουνο (για να αντέξει το Cold Start)
        public double StartingCoal { get; set; } = 250.0;

        // Ακατέργαστα παλιοσίδερα που μαζεύουν από τους πάγους
        public double StartingRawScrap { get; set; } = 30.0;

        // Επεξεργασμένο μέταλλο (π.χ. 15.0 είναι αρκετό για 1 άμεσο μπάλωμα ρήγματος - Metal Patch)
        public double StartingRefinedMetal { get; set; } = 15.0;

        // Ρεύμα στις μπαταρίες
        public double StartingElectricity { get; set; } = 0.0;

        // ==========================================
        // ΧΡΟΝΟΣ ΚΑΙ ΒΑΡΔΙΕΣ (REAL-TIME SHIFT SETTINGS)
        // ==========================================
        // Η διάρκεια της Ημερήσιας Βάρδιας (σε πραγματικές ώρες)
        public double DayShiftDurationHours { get; set; } = 8.0;

        // Κάθε πότε μπορεί ο παίκτης να πατήσει το κουμπί Routing (σε πραγματικά λεπτά)
        public double RoutingCycleDurationMinutes { get; set; } = 108.0;

        // Πόσος ατμός (σε Bar) μεταφέρεται αυτόματα στην πόλη κάθε δευτερόλεπτο κατά τη Νύχτα
        public double NightShiftCityBleedRatePerSec { get; set; } = 0.05;

        // Η ελάχιστη κατανάλωση άνθρακα το βράδυ (Idle Stance) για να μη σβήσει εντελώς η γεννήτρια
        public double NightShiftIdleCoalConsumption { get; set; } = 0.2;

        // ==========================================
        // 6. ΔΡΟΜΟΛΟΓΗΣΗ ΑΤΜΟΥ (STEAM ROUTING - 108 MIN CYCLE)
        // ==========================================
        // Οι φόρμουλες μετατροπής όταν πατάμε το κουμπί (Πόσο αποδίδει 1 Bar ατμού)
        public double SteamToCoalYield { get; set; } = 5.0;        // 1 Bar -> 5kg Κάρβουνο από το Drill
        public double SteamToElectricityYield { get; set; } = 2.0; // 1 Bar -> 2 kW Ρεύμα
        public double SteamToCityHeatYield { get; set; } = 1.5;    // 1 Bar -> 1.5°C Ζέστη στην πόλη

        // Πόσο γρήγορα απελευθερώνεται ο ατμός στην πόλη κατά τη νυχτερινή βάρδια (ανά δευτερόλεπτο)
        public double NightShiftSteamBleedRate { get; set; } = 0.2;


        // ==========================================
        // 8. ΕΡΓΑΣΙΕΣ ΚΑΙ ΠΑΡΑΓΩΓΗ (CONTINUOUS JOBS)
        // ==========================================
        // Εξόρυξη Κάρβουνου (Coal Mining)
        public double CoalMiningBaseRatePerWorker { get; set; } = 0.5; // kg ανά δευτερόλεπτο, ανά εργάτη
        public double CoalMiningDrillBuffMultiplier { get; set; } = 3.0; // Πόσο πολλαπλασιάζεται η παραγωγή με το Steam Buff
        public double SteamDrillBuffDurationPerBar { get; set; } = 10.0; // Πόσα δευτερόλεπτα διαρκεί το Buff για κάθε 1 Bar που στέλνεις στο Drill

        // Ανασκαφή Πάγων (Scavenge Raw Scrap) - Η ταχύτητα θα εξαρτάται ΚΑΙ από τη θερμοκρασία της πόλης (thaw)
        public double ScavengeScrapBaseRatePerWorker { get; set; } = 0.05; // Raw Scrap ανά δευτερόλεπτο

        // Χυτήριο (Smelting Refined Metal)
        public double SmeltingRawScrapCostPerWorker { get; set; } = 0.1; // Πόσο Raw Scrap καταναλώνει ανά δευτερόλεπτο
        public double SmeltingRefinedMetalRatePerWorker { get; set; } = 0.05; // Πόσο Refined Metal παράγει ανά δευτερόλεπτο

        // Τουρμπίνες (Turbines)
        public double TurbineSteamCostPerWorker { get; set; } = 0.1; // Πόση πίεση (Bar) ρουφάει ανά δευτερόλεπτο
        public double TurbinePowerPerWorker { get; set; } = 1.0; // Πόσα kW ρεύματος παράγει


        // ==========================================
        // 9. ΠΛΗΘΥΣΜΟΣ ΚΑΙ ΥΓΕΙΑ (POPULATION & HEALTH)
        // ==========================================
        // Αρχικός αριθμός κατοίκων ανά κατηγορία (Cold Start Scenario)
        public int StartingHealthy { get; set; } = 30; // 30 Υγιείς
        public int StartingSick { get; set; } = 8;     // 8 Ήδη άρρωστοι (από το ταξίδι/σενάριο)
        public int StartingGrave { get; set; } = 2;    // 2 Κρίσιμα (Grave)

        // Όταν ένα ατύχημα/ασθένεια χαρακτηριστεί "Καταστροφικό" (λόγω Freezing ή μεγάλου κινδύνου),
        // αυτή είναι η πιθανότητα το αποτέλεσμα να είναι Θάνατος αντί για απλός Τραυματισμός (Grave).
        public double DeathRollProbability { get; set; } = 0.4; // 40% πιθανότητα θανάτου σε ακραία γεγονότα

        // Βασική πιθανότητα ανάρρωσης ανά βάρδια/νύχτα (πριν υπολογιστούν τα πέναλτι του κρύου)
        public double NormalHealChance { get; set; } = 0.15; // 15% αν αναρρώνουν μόνοι τους (ή με απλή φροντίδα)

        // Πιθανότητα ανάρρωσης αν ο παίκτης έχει διαθέσει εργάτες στην Εντατική (Intensive Care Job)
        public double IntensiveCareHealChance { get; set; } = 0.75; // 75% με οργανωμένο ιατρείο

    }
}