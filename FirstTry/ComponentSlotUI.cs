using Godot;
using System;

public partial class ComponentSlotUI : MarginContainer
{
    [Export] public ComponentType AcceptedType = ComponentType.Normal;

    public event Action<ComponentSlotUI, ComponentData> OnSlotClicked;
    public event Action<ComponentData> OnComponentEquipped;
    public event Action<ComponentData> OnComponentUnequipped;

    private TextureRect _componentIcon;
    private Button _clickArea;

    public ComponentData EquippedComponent { get; private set; }
    public bool IsOccupied => EquippedComponent != null;

    public override void _Ready()
    {
        _componentIcon = GetNode<TextureRect>("ComponentIcon");
        _clickArea = GetNode<Button>("ClickArea");

        _componentIcon.Visible = false;
        _clickArea.Pressed += () => OnSlotClicked?.Invoke(this, EquippedComponent);
    }

    // Προσπάθεια τοποθέτησης component στο συγκεκριμένο slot
    public bool TryEquip(ComponentData data)
    {
        if (data == null) return false;

        // Έλεγχος συμβατότητας τύπου (Normal slot δέχεται Normal, Premium δέχεται Premium)
        if (data.Type != AcceptedType)
        {
            GD.Print($"[SLOT] Μη συμβατός τύπος! Απαιτείται: {AcceptedType}, Δόθηκε: {data.Type}");
            return false;
        }

        EquippedComponent = data;
        _componentIcon.Texture = data.Icon;
        _componentIcon.Visible = true;

        OnComponentEquipped?.Invoke(data);
        return true;
    }

    // Αφαίρεση του component από το slot
    public ComponentData Unequip()
    {
        if (EquippedComponent == null) return null;

        ComponentData removed = EquippedComponent;
        EquippedComponent = null;
        _componentIcon.Texture = null;
        _componentIcon.Visible = false;

        OnComponentUnequipped?.Invoke(removed);
        return removed;
    }
}