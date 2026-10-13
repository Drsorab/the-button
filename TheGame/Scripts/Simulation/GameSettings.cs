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

        // ==========================================
        // 10. ΜΗΧΑΝΙΚΟΙ ΔΙΑΚΡΙΤΩΝ ΕΡΓΑΣΙΩΝ (DISCRETE JOBS & SOOT)
        // ==========================================
        // Καπνιά (Soot)
        public double SootAccumulationPerKgCoal { get; set; } = 0.5; // Πόση καπνιά (σε μονάδες) βγάζει 1 kg κάρβουνου
        public double MaxSootCapacity { get; set; } = 100.0; // Το μέγιστο όριο καπνιάς πριν το σύστημα φράξει τελείως

        // Τα Πέναλτι της Καπνιάς
        public double SootPenalty_FurnaceEfficiencyLoss { get; set; } = 0.2; // Στο 100% καπνιάς, χάνεις 20% απόδοση θερμότητας (Option A)
        public double SootPenalty_StressAccumulationMultiplier { get; set; } = 2.0; // Στο 100% καπνιάς, το στρες ανεβαίνει 2x πιο γρήγορα (Option B)

        // Ολοκλήρωση Εργασίας Καθαρισμού Καπνιάς
        public double CleanSootWorkRequired { get; set; } = 50.0; // Πόση "Δουλειά" (δευτερόλεπτα * εργάτες) χρειάζεται για 1 καθάρισμα
        public double CoalYieldFromCleaningSoot { get; set; } = 10.0; // Ελάχιστο κάρβουνο ως ανταμοιβή
        public double SootRemovedPerClean { get; set; } = 25.0; // Πόση καπνιά αφαιρείται σε κάθε ολοκλήρωση

        // Μπάλωμα Ημέρας (Daytime Patch)
        public double PatchWorkRequired { get; set; } = 120.0;

        // Νυχτερινή Επισκευή (Night Hull Repair)
        public double NightRepairWorkRequired { get; set; } = 200.0;
        public double NightRepairRefinedMetalCost { get; set; } = 10.0; // Κόστος μετάλλου ανά παρτίδα (Batch)
        public double NightRepairIntegrityRestored { get; set; } = 5.0; // Η αντοχή (Y%) που επιστρέφεται ανά παρτίδα


        // ==========================================
        // 11. ΜΗΧΑΝΟΥΡΓΕΙΟ: ΚΑΤΑΣΚΕΥΕΣ (WORKSHOP CRAFTING)
        // ==========================================
        // Πόση δουλειά απαιτείται για την κατασκευή ενός Metal Patch
        public double CraftMetalPatchWorkRequired { get; set; } = 180.0;
        public double CraftMetalPatchRefinedMetalCost { get; set; } = 10.0; // Πόσο Refined Metal κοστίζει

        // Μηχανισμός Ηλεκτρισμού (Electricity Synergy)
        public double WorkshopElectricityConsumptionPerWorker { get; set; } = 0.5; // kW ρεύματος ανά δευτερόλεπτο
        public double WorkshopElectricitySpeedMultiplier { get; set; } = 3.0; // Πόσο πιο γρήγορα τελειώνει η δουλειά αν υπάρχει ρεύμα!

        // ==========================================
        // 12. ΜΗΧΑΝΟΥΡΓΕΙΟ: ΣΥΝΤΗΡΗΣΗ (WORKSHOP UPKEEP)
        // ==========================================
        // Κάθε εργάτης που έχει ανατεθεί στο Upkeep μειώνει την πιθανότητα ατυχήματος σε ΟΛΕΣ τις δουλειές
        public double UpkeepSafetyBonusPerWorker { get; set; } = 0.05; // 5% μείωση ανά εργάτη
        public double MaxUpkeepSafetyBonus { get; set; } = 0.50; // Μέγιστη μείωση κινδύνου στο 50% (Hard cap)

        // ==========================================
        // ΧΡΟΝΟΣ ΚΑΙ ΒΑΡΔΙΕΣ (REAL-TIME SHIFT SETTINGS & CLOCK PHASES)
        // Προσοχή: Όλες οι διάρκειες φάσεων (Phases) είναι σε Δευτερόλεπτα (Seconds)
        // ==========================================

        // 16 Ώρες πραγματικού χρόνου νυχτερινής βάρδιας (960 λεπτά * 60)
        public double NightDuration { get; set; } = 57600.0;

        // Ο Κύκλος της Εκτόνωσης. 108 πραγματικά λεπτά (108 * 60 = 6480 δευτερόλεπτα)
        public double ActiveCycleDuration { get; set; } = 6480.0;

        // Ο χρόνος που μένει στο τέλος της 8ωρης βάρδιας, αφού γίνουν 4 κύκλοι των 108 λεπτών.
        // (8 ώρες = 28800 δευτερόλεπτα) - (4 κύκλοι * 6480 = 25920 δευτερόλεπτα) = 2880 δευτερόλεπτα (48 λεπτά)
        public double TwilightDuration { get; set; } = 2880.0;

        // Ο χρόνος (σε δευτερόλεπτα) που έχει ο παίκτης για να πατήσει το κουμπί από τη στιγμή που θα ανάψει.
        // Αν δεν το πατήσει, η πίεση εγκλωβίζεται (Grace Period). 
        // Π.χ. 300 δευτερόλεπτα = 5 λεπτά.
        public double VentGracePeriod { get; set; } = 300.0;

        // Ο χρόνος που έχει ο παίκτης αφού ανοίξει το παράθυρο (Modal) για να επιλέξει πού θα πάει ο ατμός.
        // Αν λήξει, το παράθυρο κλείνει αυτόματα.
        public double VentModalTimeout { get; set; } = 30.0;

        // ==========================================
        // 11. ΜΗΧΑΝΟΥΡΓΕΙΟ: ΚΑΤΑΣΚΕΥΕΣ (WORKSHOP CRAFTING & FUSES)
        // ==========================================
        // (Κράτα τα υπάρχοντα του Metal Patch από πάνω)

        // Κατασκευή Ασφαλειών (Fuses)
        public int MaxFuses { get; set; } = 3; // Μέγιστος αριθμός
        public double CraftFuseWorkRequired { get; set; } = 600.0; // Πολύς χρόνος (σημαντική ενέργεια)
        public double CraftFuseRefinedMetalCost { get; set; } = 30.0; // Πολύ ακριβό σε μέταλλο

        // Ζημιά όταν σπάει μια Ασφάλεια (λόγω χαμένου Routing)
        public double FuseBlowIntegrityDamage { get; set; } = 15.0; // Σπάει και κάνει ζημιά 15% στη γεννήτρια
        public double FuseBlowPressureReleasePct { get; set; } = 0.8; // Απελευθερώνει βίαια το 80% της πίεσης στην ατμόσφαιρα
    }
}