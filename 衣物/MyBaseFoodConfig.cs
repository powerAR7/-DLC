using System.Collections.Generic;
using TUNING;
using UnityEngine;

public abstract class MyBaseFoodConfig : DlcRestrictedEntityConfig
{
    protected abstract string FoodId { get; }
    protected abstract string FoodName { get; }
    protected abstract string FoodDescription { get; }
    protected abstract KAnimFile FoodAnimation { get; }
    protected abstract IReadOnlyList<string> FoodBuffIds { get; }

    protected virtual float Mass
    {
        get { return 1f; }
    }

    protected virtual float CaloriesPerUnit
    {
        get { return FOOD.FOOD_TYPES.MUSHBAR.CaloriesPerUnit; }
    }

    protected virtual int Quality
    {
        get { return FOOD.FOOD_TYPES.MUSHBAR.Quality; }
    }

    protected virtual float Width
    {
        get { return 0.8f; }
    }

    protected virtual float Height
    {
        get { return 0.4f; }
    }

    protected virtual int SortOrder
    {
        get { return 0; }
    }

    protected virtual SimHashes Element
    {
        get { return SimHashes.Creature; }
    }

    protected virtual float SpoilTime
    {
        get { return FOOD.SPOIL_TIME.VERYSLOW; }
    }

    protected virtual void LoadFoodAssets()
    {
    }

    public override GameObject CreatePrefab()
    {
        LoadFoodAssets();

        GameObject prefab = EntityTemplates.CreateLooseEntity(
            FoodId,
            FoodName,
            FoodDescription,
            Mass,
            false,
            FoodAnimation,
            "object",
            Grid.SceneLayer.Front,
            EntityTemplates.CollisionShape.RECTANGLE,
            Width,
            Height,
            true,
            SortOrder,
            Element,
            null);

        EdiblesManager.FoodInfo foodInfo = new EdiblesManager.FoodInfo(
            FoodId,
            CaloriesPerUnit,
            Quality,
            FOOD.DEFAULT_PRESERVE_TEMPERATURE,
            FOOD.DEFAULT_ROT_TEMPERATURE,
            SpoilTime,
            can_rot: true);

        if (FoodBuffIds != null && FoodBuffIds.Count > 0)
        {
            foodInfo = foodInfo.AddEffects(new List<string>(FoodBuffIds));
        }

        return EntityTemplates.ExtendEntityToFood(prefab, foodInfo);
    }
}
