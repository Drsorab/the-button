using System;

namespace PermafrostChieftain.Simulation
{
    public class LogisticsSubsystem
    {
        // Τα 4 βασικά υλικά της οικονομίας μας
        public double CoalStockpile { get; private set; }
        public double RawScrap { get; private set; }
        public double RefinedMetal { get; private set; }
        public double Electricity { get; private set; }

        public int AvailableFuses { get; private set; }

        // Το τελικό αντικείμενο για το μπάλωμα της γεννήτριας
        public int CraftedMetalPatches { get; private set; }

        // Γεγονός (Event) για να ανανεώνεται το UI κάθε φορά που αλλάζει ο αριθμός των πόρων
        public event Action OnResourcesChanged;

        public void Initialize()
        {
            // Αντλούμε τα αρχικά ποσά από τα Settings
            CoalStockpile = GameSettings.Instance.StartingCoal;
            RawScrap = GameSettings.Instance.StartingRawScrap;
            RefinedMetal = GameSettings.Instance.StartingRefinedMetal;
            Electricity = GameSettings.Instance.StartingElectricity;
            AvailableFuses = 0;
            OnResourcesChanged?.Invoke();
        }

        // =========================================================
        // ΜΕΘΟΔΟΙ ΠΡΟΣΘΗΚΗΣ ΠΟΡΩΝ (Από το Routing, τα Jobs, κλπ)
        // =========================================================
        public void AddCoal(double amount)
        {
            if (amount <= 0) return;
            CoalStockpile += amount;
            OnResourcesChanged?.Invoke();
        }

        public void AddRawScrap(double amount)
        {
            if (amount <= 0) return;
            RawScrap += amount;
            OnResourcesChanged?.Invoke();
        }

        public void AddRefinedMetal(double amount)
        {
            if (amount <= 0) return;
            RefinedMetal += amount;
            OnResourcesChanged?.Invoke();
        }

        public void AddElectricity(double amount)
        {
            if (amount <= 0) return;
            Electricity += amount;
            OnResourcesChanged?.Invoke();
        }

        // =========================================================
        // ΜΕΘΟΔΟΙ ΚΑΤΑΝΑΛΩΣΗΣ ΠΟΡΩΝ (Για Crafting, Λέβητα, Μπαλώματα)
        // Επιστρέφουν true αν υπήρχε αρκετό υλικό και καταναλώθηκε, αλλιώς false
        // =========================================================
        public bool TryConsumeCoal(double amount)
        {
            if (CoalStockpile >= amount)
            {
                CoalStockpile -= amount;
                OnResourcesChanged?.Invoke();
                return true;
            }
            // Αν θέλεις η φωτιά να καίει ό,τι έχει μείνει (ακόμα και αν ζητάει 10 και έχεις 5),
            // αυτό αντιμετωπίζεται στον Orchestrator (θα τραβάει το υπόλοιπο).
            return false;
        }

        // Μέθοδος που απλά αδειάζει ό,τι έχει μείνει (χρήσιμο για το κάψιμο κάρβουνου)
        public double ConsumeUpToCoal(double requestedAmount)
        {
            double actualConsumed = Math.Min(requestedAmount, CoalStockpile);
            CoalStockpile -= actualConsumed;
            if (actualConsumed > 0) OnResourcesChanged?.Invoke();
            return actualConsumed;
        }

        public bool TryConsumeRawScrap(double amount)
        {
            if (RawScrap >= amount)
            {
                RawScrap -= amount;
                OnResourcesChanged?.Invoke();
                return true;
            }
            return false;
        }

        public bool TryConsumeRefinedMetal(double amount)
        {
            if (RefinedMetal >= amount)
            {
                RefinedMetal -= amount;
                OnResourcesChanged?.Invoke();
                return true;
            }
            return false;
        }

        public bool TryConsumeElectricity(double amount)
        {
            if (Electricity >= amount)
            {
                Electricity -= amount;
                OnResourcesChanged?.Invoke();
                return true;
            }
            return false;
        }

        public void AddMetalPatch(int amount)
        {
            if (amount <= 0) return;
            CraftedMetalPatches += amount;
            OnResourcesChanged?.Invoke();
        }

        public bool TryConsumeMetalPatch(int amount)
        {
            if (CraftedMetalPatches >= amount)
            {
                CraftedMetalPatches -= amount;
                OnResourcesChanged?.Invoke();
                return true;
            }
            return false;
        }

        public void AddFuse()
        {
            if (AvailableFuses < GameSettings.Instance.MaxFuses)
            {
                AvailableFuses++;
                OnResourcesChanged?.Invoke();
            }
        }

        public bool TryConsumeFuse()
        {
            if (AvailableFuses > 0)
            {
                AvailableFuses--;
                OnResourcesChanged?.Invoke();
                return true;
            }
            return false;
        }
    }
}