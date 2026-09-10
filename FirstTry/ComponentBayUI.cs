using Godot;
using System;
using System.Collections.Generic;

public partial class ComponentBayUI : Control
{
    private readonly List<ComponentSlotUI> _normalSlots = new();
    private readonly List<ComponentSlotUI> _premiumSlots = new();
    private ComponentManager _componentManager;
    private ResourceManager _resourceManager;

    public ComponentData CurrentlySelectedComponent { get; set; }

    public override void _Ready()
    {
        Node normalContainer = GetNodeOrNull<Node>("NormalGrid");
        if (normalContainer != null)
        {
            foreach (Node child in normalContainer.GetChildren())
            {
                if (child is ComponentSlotUI slot)
                {
                    slot.AcceptedType = ComponentType.Normal;
                    SetupSlotEvents(slot);
                    _normalSlots.Add(slot);
                }
            }
        }

        Node premiumContainer = GetNodeOrNull<Node>("PremiumContainer");
        if (premiumContainer != null)
        {
            foreach (Node child in premiumContainer.GetChildren())
            {
                if (child is ComponentSlotUI slot)
                {
                    slot.AcceptedType = ComponentType.Premium;
                    SetupSlotEvents(slot);
                    _premiumSlots.Add(slot);
                }
            }
        }
    }

    // Αρχικοποίηση με ComponentManager & ResourceManager
    public void Initialize(ComponentManager manager, ResourceManager resourceManager)
    {
        _componentManager = manager;
        _resourceManager = resourceManager;
    }

    // Κεντρική μέθοδος: Ελέγχει κόστος, αφαιρεί πόρους και κουμπώνει το component
    private bool TryPurchaseAndEquip(ComponentSlotUI slot, ComponentData item)
    {
        if (item == null || slot.IsOccupied) return false;

        // 1. Έλεγχος συμβατότητας τύπου (Normal vs Premium)
        if (slot.AcceptedType != item.Type)
        {
            GD.Print($"[BAY] Μη συμβατή θέση! Το slot απαιτεί: {slot.AcceptedType}, αλλά δόθηκε: {item.Type}");
            return false;
        }

        // 2. Έλεγχος και αφαίρεση πόρων
        if (_resourceManager != null)
        {
            bool hasResources = _resourceManager.TrySpendResources(item.MetalScrapCost, item.ElectricityCost, item.HeatCost);
            if (!hasResources)
            {
                GD.Print($"[BAY] Ανεπαρκείς πόροι για το '{item.DisplayName}'! (Απαιτούνται: Metal {item.MetalScrapCost}, Elec {item.ElectricityCost}, Heat {item.HeatCost})");
                return false;
            }
        }

        // 3. Εφόσον πληρώθηκε, κουμπώνει στο Slot
        bool equipped = slot.TryEquip(item);
        if (equipped)
        {
            GD.Print($"[BAY] Αγοράστηκε & Τοποθετήθηκε: '{item.DisplayName}' στο {slot.Name}.");
            return true;
        }

        return false;
    }

    private void HandleSlotClicked(ComponentSlotUI clickedSlot, ComponentData currentItemInSlot)
    {
        // Περίπτωση Α: Τοποθέτηση επιλεγμένου component
        if (CurrentlySelectedComponent != null && !clickedSlot.IsOccupied)
        {
            if (TryPurchaseAndEquip(clickedSlot, CurrentlySelectedComponent))
            {
                CurrentlySelectedComponent = null; // Καθαρισμός επιλογής
            }
            return;
        }

        // Περίπτωση Β: Αφαίρεση εξαρτήματος με κλικ
        if (clickedSlot.IsOccupied)
        {
            ComponentData removed = clickedSlot.Unequip();
            GD.Print($"[BAY] Αφαιρέθηκε το '{removed.DisplayName}' από το {clickedSlot.Name}.");
        }
    }

    // Αυτόματη αγορά & τοποθέτηση στην πρώτη ελεύθερη θέση
    public bool AutoEquip(ComponentData component)
    {
        if (component == null) return false;

        List<ComponentSlotUI> targetList = component.Type == ComponentType.Premium
            ? _premiumSlots
            : _normalSlots;

        ComponentSlotUI freeSlot = targetList.Find(s => !s.IsOccupied);
        if (freeSlot != null)
        {
            return TryPurchaseAndEquip(freeSlot, component);
        }

        GD.Print($"[BAY] Δεν υπάρχει ελεύθερη θέση για {component.Type} component.");
        return false;
    }

    private void SetupSlotEvents(ComponentSlotUI slot)
    {
        slot.OnSlotClicked += HandleSlotClicked;
        slot.OnComponentEquipped += (item) => _componentManager?.EquipComponent(item);
        slot.OnComponentUnequipped += (item) => _componentManager?.UnequipComponent(item);
    }
}