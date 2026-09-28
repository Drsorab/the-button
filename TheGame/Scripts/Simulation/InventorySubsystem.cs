using System;
using System.Collections.Generic;

namespace PermafrostChieftain.Simulation
{
    // Κλάση που αντιπροσωπεύει ένα αντικείμενο στον εξοπλισμό
    public class EquipmentItem
    {
        public string Name { get; set; }
        public double Durability { get; set; } = 100.0; // Ξεκινάει πάντα στο 100%
        public bool IsEquipped { get; set; } = false;

        public EquipmentItem(string name)
        {
            Name = name;
        }
    }

    public class InventorySubsystem
    {
        // Η λίστα με όλα τα αντικείμενα που έχει κατασκευάσει ο παίκτης
        public List<EquipmentItem> Items { get; private set; } = new List<EquipmentItem>();

        // Προσθέτει ένα νέο αντικείμενο στο Inventory όταν ολοκληρωθεί το Crafting
        public void AddItem(string itemName)
        {
            Items.Add(new EquipmentItem(itemName));
        }

        // Εξοπλίζει ένα συγκεκριμένο αντικείμενο (αν ο παίκτης έχει πολλαπλά, εξοπλίζει το πρώτο διαθέσιμο)
        public void EquipItem(string itemName)
        {
            foreach (var item in Items)
            {
                if (item.Name == itemName && !item.IsEquipped && item.Durability > 0)
                {
                    item.IsEquipped = true;
                    return;
                }
            }
        }

        // Αφαιρεί τον εξοπλισμό ενός αντικειμένου
        public void UnequipItem(string itemName)
        {
            foreach (var item in Items)
            {
                if (item.Name == itemName && item.IsEquipped)
                {
                    item.IsEquipped = false;
                    return; // Αφαιρεί μόνο ένα
                }
            }
        }

        // Επιστρέφει το ΕΝΕΡΓΟ (εξοπλισμένο) αντικείμενο ενός συγκεκριμένου τύπου, για να τσεκάρουμε αντοχή
        public EquipmentItem GetEquippedItem(string itemName)
        {
            foreach (var item in Items)
            {
                if (item.Name == itemName && item.IsEquipped)
                {
                    return item;
                }
            }
            return null; // Δεν είναι εξοπλισμένο
        }

        // Εφαρμόζει φθορά σε ένα εξοπλισμένο αντικείμενο. Αν σπάσει, το κάνει Unequip.
        public void DegradeEquippedItem(string itemName, double damageAmount)
        {
            var item = GetEquippedItem(itemName);
            if (item != null)
            {
                item.Durability -= damageAmount;
                if (item.Durability <= 0)
                {
                    item.Durability = 0;
                    item.IsEquipped = false;
                    // ΣΗΜΕΙΩΣΗ: Αργότερα μπορούμε να πετάμε κι ένα Notification στο UI ότι "Έσπασε το φτυάρι!"
                }
            }
        }
    }
}