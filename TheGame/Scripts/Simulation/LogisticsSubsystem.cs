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
        public double BaseMaxElectricity { get; set; } = 100.0; // Βασική χωρητικότητα

        // Αποθέματα αναλωσίμων (μέγιστα όρια αποθήκευσης)
        public const int MaxFuses = 3;
        public const int MaxPlugs = 2;
        public const int MaxTonics = 3;

        public int StoredFuses { get; set; } = 3;
        public int StoredPlugs { get; set; } = 0;
        public int StoredTonics { get; set; } = 0;


        // Κατάσταση ενεργής κατασκευής αναλωσίμου
        public bool IsCrafting { get; private set; } = false;
        public ConsumableType ActiveCraftItem { get; private set; }
        public double CraftTimeRemaining { get; private set; } = 0.0;

        public double GetMaxElectricity(InventorySubsystem inventory)
        {
            double max = BaseMaxElectricity;
            if (inventory != null && inventory.GetEquippedItem("Capacitor Batteries") != null)
            {
                max += GameSettings.Instance.BatteryCapacityBonus;
            }
            return max;
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

    }
}