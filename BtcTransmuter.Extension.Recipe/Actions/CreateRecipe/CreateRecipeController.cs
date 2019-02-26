using System.Threading.Tasks;
using BtcTransmuter.Abstractions.Actions;
using BtcTransmuter.Abstractions.Recipes;
using BtcTransmuter.Data.Entities;
using BtcTransmuter.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;

namespace BtcTransmuter.Extension.Recipe.Actions.CreateRecipe
{
    [Route("recipe-plugin/actions/create-recipe")]
    [Authorize]
    public class CreateRecipeController : BaseActionController<CreateRecipeController.CreateRecipeViewModel, CreateRecipeData>
    {
        public CreateRecipeController(IMemoryCache memoryCache, UserManager<User> userManager,
            IRecipeManager recipeManager) : base(memoryCache, userManager, recipeManager)
        {
        }

        protected override async Task<CreateRecipeViewModel> BuildViewModel(RecipeAction from)
        {
            var data = from.Get<CreateRecipeData>();
            return new CreateRecipeViewModel
            {
                RecipeId = from.RecipeId,
                Recipes = new SelectList(
                    await _recipeManager.GetRecipes(new RecipesQuery() {UserId = _userManager.GetUserId(User)}),
                    nameof(BtcTransmuter.Data.Entities.Recipe.Id), nameof(BtcTransmuter.Data.Entities.Recipe.Name),
                    data.RecipeTemplateId),
                RecipeTemplateId = data.RecipeTemplateId,
                Enable = data.Enable
            };
        }

        protected override async Task<CreateRecipeViewModel> BuildViewModel(CreateRecipeViewModel vm)
        {
            vm.Recipes = new SelectList(
                await _recipeManager.GetRecipes(new RecipesQuery() {UserId = _userManager.GetUserId(User)}),
                nameof(BtcTransmuter.Data.Entities.Recipe.Id), nameof(BtcTransmuter.Data.Entities.Recipe.Name),
                vm.RecipeTemplateId);
            return vm;
        }

        public class CreateRecipeViewModel : CreateRecipeData
        {
            public SelectList Recipes { get; set; }
            public string RecipeId { get; set; }
        }
    }
}