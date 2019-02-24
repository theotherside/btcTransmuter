using System;
using System.Threading.Tasks;
using BtcTransmuter.Abstractions.Actions;
using BtcTransmuter.Abstractions.Helpers;
using BtcTransmuter.Abstractions.Recipes;
using BtcTransmuter.Data.Entities;
using BtcTransmuter.Extension.Recipe.Actions.CreateRecipe;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace BtcTransmuter.Extension.Recipe.Actions.ToggleRecipe
{
    public class ToggleRecipeActionHandler : BaseActionHandler<ToggleRecipeData>, IActionDescriptor
    {
        public override string ActionId => "ToggleRecipe";
        public string Name => "Toggle Recipe";

        public string Description =>
            "Enable/Disable a recipe within the system";

        public string ViewPartial => "ViewToggleRecipeAction";

        public Task<IActionResult> EditData(RecipeAction data)
        {
            using (var scope = DependencyHelper.ServiceScopeFactory.CreateScope())
            {
                var identifier = $"{Guid.NewGuid()}";
                var memoryCache = scope.ServiceProvider.GetService<IMemoryCache>();
                memoryCache.Set(identifier, data, new MemoryCacheEntryOptions()
                {
                    SlidingExpiration = TimeSpan.FromMinutes(60)
                });

                return Task.FromResult<IActionResult>(new RedirectToActionResult(
                    nameof(ToggleRecipeController.EditData),
                    "ToggleRecipe", new
                    {
                        identifier
                    }));
            }
        }

        protected override Task<bool> CanExecute(object triggerData, RecipeAction recipeAction)
        {
            return Task.FromResult(recipeAction.ActionId == ActionId);
        }

        protected override async Task<ActionHandlerResult> Execute(object triggerData, RecipeAction recipeAction,
            ToggleRecipeData actionData)
        {
            
            try
            {
                using (var scope = DependencyHelper.ServiceScopeFactory.CreateScope())
                {
                    var recipeManager = scope.ServiceProvider.GetService<IRecipeManager>();
                    var recipe = await recipeManager.GetRecipe(actionData.TargetRecipeId);
                    if (recipe == null)
                    {
                        return new ActionHandlerResult()
                        {
                            Executed = false,
                            Result =
                                $"Could not find recipe to toggle"
                        };
                    }

                    switch (actionData.Option)
                    {
                        case ToggleRecipeData.ToggleRecipeOption.Enable:
                            recipe.Enabled = true;
                            break;
                        case ToggleRecipeData.ToggleRecipeOption.Disable:
                            recipe.Enabled = false;
                            break;
                        case ToggleRecipeData.ToggleRecipeOption.Toggle:
                            recipe.Enabled = !recipe.Enabled;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    
                    await recipeManager.AddOrUpdateRecipe(recipe);
                    return new ActionHandlerResult()
                    {
                        Executed = true,
                        Result =
                            $"Recipe {recipe.Name} is now {(recipe.Enabled? "Enabled": "Disabled")}"
                    };
                }
            }
            catch (Exception e)
            {
                return new ActionHandlerResult()
                {
                    Executed = false,
                    Result =
                        $"Could not toggle recipe because {e.Message}"
                };
            }
        }
    }
}