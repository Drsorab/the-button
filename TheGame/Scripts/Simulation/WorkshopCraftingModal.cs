using Godot;
using PermafrostChieftain.Simulation;

namespace PermafrostChieftain.UI
{
    public partial class WorkshopCraftingModal : PanelContainer // Or whatever Control type the root is
    {
        private Control _recipePrefab;
        private GameOrchestrator _gameOrchestrator;

        public override void _Ready()
        {
            _gameOrchestrator = GetTree().Root.GetNode<GameOrchestrator>("TheGame");
            _recipePrefab = GetNode<Control>("MainVBox/ScrollArea/RecipeListVBox/RecipePrefab");

            // Hide the prefab so it doesn't show up as a blank entry
            _recipePrefab.Visible = false;

            // Optional: Connect close button
            GetNode<Button>("MainVBox/CloseShopBtn").Pressed += () => Visible = false;

            PopulateCraftingMenu();
        }

        public void PopulateCraftingMenu()
        {
            var listContainer = GetNode<VBoxContainer>("MainVBox/ScrollArea/RecipeListVBox");

            foreach (var recipe in RecipeRegistry.Recipes)
            {
                var node = (Control)_recipePrefab.Duplicate();
                node.Visible = true;
                listContainer.AddChild(node);

                node.GetNode<Label>("RecipeName").Text = recipe.ItemName;
                node.GetNode<Label>("CostLabel").Text = $"{recipe.RefinedMetalCost} Metal";

                var craftBtn = node.GetNode<Button>("CraftBtn");
                craftBtn.Pressed += () =>
                {
                    // Placeholder for assigning workers to this specific recipe
                    GD.Print($"Selected {recipe.ItemName} for crafting.");
                };
            }
        }
    }
}