using System;

namespace PermafrostChieftain.Simulation
{
    // Τύποι αναλώσιμων αντικειμένων εργαστηρίου
    public enum ConsumableType
    {
        PorcelainFuse, // Ασφάλεια ανακούφισης
        GraphitePlug,  // Τάπα μολύβδου-γραφίτη
        MedicalTonic   // Ιατρικό τονωτικό διεγερτικό
    }

    // Μόνιμες μηχανικές αναβαθμίσεις
    public enum EngineRetrofit
    {
        BypassShuntValve,       // +5s περίοδος χάριτος
        ReinforcedBoilerStaves, // Αύξηση ορίου πίεσης σε 155 Bar
        PneumaticCoalFeeders,   // Μείωση βέλτιστου προσωπικού καύσης σε 10
        CopperRadiatorCoils     // +25% απόδοση θέρμανσης πόλης
    }

    public class LogisticsSubsystem
    {
        // Αποθέματα πρώτων υλών
        public double CoalStockpile { get; set; } = 1200.0;    // Άνθρακας (kg)
        public double ScrapStockpile { get; set; } = 35.0;      // Παλιοσίδερα (units)
        public double BatteryCharge { get; set; } = 100.0;     // Εφεδρική μπαταρία (%)

        // Αποθέματα αναλωσίμων (μέγιστα όρια αποθήκευσης)
        public const int MaxFuses = 3;
        public const int MaxPlugs = 2;
        public const int MaxTonics = 3;

        public int StoredFuses { get; set; } = 3;
        public int StoredPlugs { get; set; } = 0;
        public int StoredTonics { get; set; } = 0;

        // Εγκατεστημένες μόνιμες αναβαθμίσεις
        public bool HasBypassShuntValve { get; private set; } = false;
        public bool HasReinforcedBoilerStaves { get; private set; } = false;
        public bool HasPneumaticFeeders { get; private set; } = false;
        public bool HasCopperCoils { get; private set; } = false;

        // Κατάσταση ενεργής κατασκευής αναλωσίμου
        public bool IsCrafting { get; private set; } = false;
        public ConsumableType ActiveCraftItem { get; private set; }
        public double CraftTimeRemaining { get; private set; } = 0.0;

        // Κατάσταση ενεργής εγκατάστασης αναβάθμισης (Νυχτερινή βάρδια)
        public bool IsRetrofitting { get; private set; } = false;
        public EngineRetrofit ActiveRetrofit { get; private set; }
        public double RetrofitProgress { get; private set; } = 0.0; // 0.0 έως 57.600 δευτερόλεπτα (16 ώρες)

        public void Tick(double deltaTime, double batteryDrainRate)
        {
            if (deltaTime <= 0.0) return;

            // Εκφόρτιση βοηθητικής μπαταρίας
            BatteryCharge = Math.Clamp(BatteryCharge - (batteryDrainRate * deltaTime), 0.0, 100.0);

            // Επεξεργασία ενεργής κατασκευής αναλωσίμου
            if (IsCrafting)
            {
                CraftTimeRemaining -= deltaTime;
                if (CraftTimeRemaining <= 0.0)
                {
                    FinalizeConsumableCrafting();
                }
            }

            // Επεξεργασία ενεργής αναβάθμισης
            if (IsRetrofitting)
            {
                RetrofitProgress += deltaTime;
                if (RetrofitProgress >= 57600.0) // 16 ώρες νυχτερινής εργασίας
                {
                    FinalizeRetrofit();
                }
            }
        }

        // Έναρξη κατασκευής αναλωσίμου
        public bool StartCraftingConsumable(ConsumableType item, int reserveHandsAvailable)
        {
            if (IsCrafting) return false;

            switch (item)
            {
                case ConsumableType.PorcelainFuse:
                    if (StoredFuses >= MaxFuses || ScrapStockpile < 4.0 || CoalStockpile < 150.0 || reserveHandsAvailable < 2)
                        return false;
                    ScrapStockpile -= 4.0;
                    CoalStockpile -= 150.0;
                    CraftTimeRemaining = 7200.0; // 2 ώρες
                    break;

                case ConsumableType.GraphitePlug:
                    if (StoredPlugs >= MaxPlugs || ScrapStockpile < 2.0 || reserveHandsAvailable < 1)
                        return false;
                    ScrapStockpile -= 2.0;
                    CraftTimeRemaining = 3600.0; // 1 ώρα
                    break;

                case ConsumableType.MedicalTonic:
                    if (StoredTonics >= MaxTonics || ScrapStockpile < 1.0 || CoalStockpile < 50.0 || reserveHandsAvailable < 1)
                        return false;
                    ScrapStockpile -= 1.0;
                    CoalStockpile -= 50.0;
                    CraftTimeRemaining = 3600.0; // 1 ώρα
                    break;
            }

            ActiveCraftItem = item;
            IsCrafting = true;
            return true;
        }

        private void FinalizeConsumableCrafting()
        {
            IsCrafting = false;
            switch (ActiveCraftItem)
            {
                case ConsumableType.PorcelainFuse:
                    StoredFuses = Math.Min(MaxFuses, StoredFuses + 1);
                    break;
                case ConsumableType.GraphitePlug:
                    StoredPlugs = Math.Min(MaxPlugs, StoredPlugs + 1);
                    break;
                case ConsumableType.MedicalTonic:
                    StoredTonics = Math.Min(MaxTonics, StoredTonics + 1);
                    break;
            }
        }

        // Έναρξη νυχτερινής εγκατάστασης αναβάθμισης
        public bool StartRetrofit(EngineRetrofit retrofit, int availableNightHands)
        {
            if (IsRetrofitting) return false;

            switch (retrofit)
            {
                case EngineRetrofit.BypassShuntValve:
                    if (HasBypassShuntValve || ScrapStockpile < 25.0 || availableNightHands < 4) return false;
                    ScrapStockpile -= 25.0;
                    break;

                case EngineRetrofit.ReinforcedBoilerStaves:
                    if (HasReinforcedBoilerStaves || ScrapStockpile < 40.0 || availableNightHands < 6) return false;
                    ScrapStockpile -= 40.0;
                    break;

                case EngineRetrofit.PneumaticCoalFeeders:
                    if (HasPneumaticFeeders || ScrapStockpile < 30.0 || availableNightHands < 4) return false;
                    ScrapStockpile -= 30.0;
                    break;

                case EngineRetrofit.CopperRadiatorCoils:
                    if (HasCopperCoils || ScrapStockpile < 35.0 || availableNightHands < 5) return false;
                    ScrapStockpile -= 35.0;
                    break;
            }

            ActiveRetrofit = retrofit;
            RetrofitProgress = 0.0;
            IsRetrofitting = true;
            return true;
        }

        private void FinalizeRetrofit()
        {
            IsRetrofitting = false;
            RetrofitProgress = 0.0;
            switch (ActiveRetrofit)
            {
                case EngineRetrofit.BypassShuntValve:
                    HasBypassShuntValve = true;
                    break;
                case EngineRetrofit.ReinforcedBoilerStaves:
                    HasReinforcedBoilerStaves = true;
                    break;
                case EngineRetrofit.PneumaticCoalFeeders:
                    HasPneumaticFeeders = true;
                    break;
                case EngineRetrofit.CopperRadiatorCoils:
                    HasCopperCoils = true;
                    break;
            }
        }
    }
}