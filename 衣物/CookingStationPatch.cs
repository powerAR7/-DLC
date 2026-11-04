using System.Collections.Generic;
using HarmonyLib;
using TUNING;
using UnityEngine;

[HarmonyPatch(typeof(CookingStationConfig), "ConfigureBuildingTemplate")]
public static class CookingStationPatch
{
    [HarmonyPostfix]
    public static void Postfix(GameObject go, Tag prefab_tag)
    {
        if (!DlcManager.IsContentSubscribed("DLC5_ID"))
            return;

        ComplexRecipe.RecipeElement[] ingredients =
        {
            new ComplexRecipe.RecipeElement(
                new Tag[] { SimHashes.Mucus.CreateTag() },
                1f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                "",
                false,
                false),
            new ComplexRecipe.RecipeElement(
                new Tag[] { "SpiceNut".ToTag() },
                1f,
                ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                "",
                false,
                false)
        };
        ComplexRecipe.RecipeElement[] results =
        {
            new ComplexRecipe.RecipeElement(
                HuLaTangConfig.ID.ToTag(),
                1f,
                ComplexRecipe.RecipeElement.TemperatureOperation.Heated,
                false)
        };
        string recipeId = ComplexRecipeManager.MakeRecipeID("CookingStation", ingredients, results);

        new ComplexRecipe(recipeId, ingredients, results)
        {
            time = TUNING.FOOD.RECIPES.STANDARD_COOK_TIME,
            description = "用粘蛋白和火椒粒熬制的特色汤品。",
            nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
            fabricators = new List<Tag> { "CookingStation".ToTag() }
        };
    }
}
