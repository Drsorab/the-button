using System.Collections.Generic;

namespace PermafrostChieftain.Simulation
{
    public enum ItemCategory
    {
        Consumable, // e.g., Metal Patch, Boiler Fuse (Used automatically or via jobs)
        Equippable  // e.g., Furnace Shovel, Hazard Respirator (Must be equipped in Inventory)
    }

    public class CraftingRecipe
    {
        public string ItemName { get; set; }
        public ItemCategory Category { get; set; }
        public double WorkRequired { get; set; }
        public double RefinedMetalCost { get; set; }
        public JobType AssociatedJob { get; set; } // The JobType that triggers this crafting

        public CraftingRecipe(string name, ItemCategory category, double work, double cost, JobType job)
        {
            ItemName = name;
            Category = category;
            WorkRequired = work;
            RefinedMetalCost = cost;
            AssociatedJob = job;
        }
    }

    // A central registry for all craftable items in the game
    public static class RecipeRegistry
    {
        public static readonly List<CraftingRecipe> Recipes = new List<CraftingRecipe>
        {
            new CraftingRecipe("Metal Patch", ItemCategory.Consumable, GameSettings.Instance.CraftMetalPatchWorkRequired, GameSettings.Instance.CraftMetalPatchRefinedMetalCost, JobType.WorkshopCraftPatch),
            new CraftingRecipe("Boiler Fuse", ItemCategory.Consumable, GameSettings.Instance.CraftFuseWorkRequired, GameSettings.Instance.CraftFuseRefinedMetalCost, JobType.WorkshopCraftFuse),
            
            // Example future equippables:
            // new CraftingRecipe("Steel Shovel", ItemCategory.Equippable, 300.0, 15.0, JobType.WorkshopCraftShovel),
            // new CraftingRecipe("Respirator", ItemCategory.Equippable, 400.0, 20.0, JobType.WorkshopCraftRespirator)
        };
    }
}