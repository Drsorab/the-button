using Godot;
using PermafrostChieftain.Simulation;

namespace PermafrostChieftain.UI
{
    public partial class InventoryModal : PanelContainer
    {
        private Control _itemPrefab;
        private GameOrchestrator _gameOrchestrator;
        private InventorySubsystem _inventory;

        public override void _Ready()
        {
            _gameOrchestrator = GetTree().Root.GetNode<GameOrchestrator>("TheGame");
            if (_gameOrchestrator != null)
            {
                _inventory = _gameOrchestrator.Inventory;
            }
            _itemPrefab = GetNode<Control>("MainVBox/ScrollArea/ItemListVBox/ItemPrefab");

            // Κρύβουμε το αρχικό prefab για να μη φαίνεται στη λίστα
            _itemPrefab.Visible = false;

            GetNode<Button>("MainVBox/CloseInventoryBtn").Pressed += () => Visible = false;
        }

        // Call this method whenever the modal becomes visible
        public void PopulateInventoryMenu()
        {
            if (_inventory == null) return;

            var listContainer = GetNode<VBoxContainer>("MainVBox/ScrollArea/ItemListVBox");

            // Clear existing generated items before repopulating (keep the prefab)
            foreach (Node child in listContainer.GetChildren())
            {
                if (child != _itemPrefab) child.QueueFree();
            }

            foreach (var item in _inventory.Items)
            {
                var node = (Control)_itemPrefab.Duplicate();
                node.Visible = true;
                listContainer.AddChild(node);

                node.GetNode<Label>("ItemNameLabel").Text = item.Name;

                var equipBtn = node.GetNode<Button>("EquipBtn");
                var durabilityBar = node.GetNode<ProgressBar>("DurabilityBar");

                if (item.Category == ItemCategory.Consumable)
                {
                    equipBtn.Visible = false;
                    durabilityBar.Visible = false;
                }
                else
                {
                    equipBtn.Visible = true;
                    durabilityBar.Visible = true;
                    durabilityBar.Value = item.Durability;

                    equipBtn.Text = item.IsEquipped ? "Unequip" : "Equip";
                    equipBtn.Pressed += () =>
                    {
                        if (item.IsEquipped) _inventory.UnequipItem(item.Name);
                        else _inventory.EquipItem(item.Name);

                        // Refresh the UI instantly
                        PopulateInventoryMenu();
                    };
                }
            }
        }
    }
}