using System.Collections.Generic;
using UnityEngine;
using TUNING;

namespace FandC
{
    public class BrewingBarrelConfig : IBuildingConfig
    {
        public const string ID = "BrewingBarrel";

        // 固定配方 ID：稳定、可以直接拿来去重（读档 / 回主菜单再开新档都不会重复注册）
        private const string RECIPE_ID = "BrewingBarrel_HuLaTang_Ethanol";

        // 双保险用的静态状态：记住上次注册时用的 manager 实例，
        // 一旦 manager 换了（新的一局），就允许重新注册。
        private static ComplexRecipeManager cachedRecipeManager;
        private static bool recipeRegistered;

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                ID,
                1,
                1,
                "ventgas_kanim",                 // 建筑本体动画：排气口
                100,
                30f,
                TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER3,
                TUNING.MATERIALS.REFINED_METALS,
                800f,
                BuildLocationRule.OnFloorOrBuildingAttachPoint,
                TUNING.BUILDINGS.DECOR.PENALTY.TIER1,
                TUNING.NOISE_POLLUTION.NONE,
                0.2f);

            def.Floodable = false;
            def.AudioCategory = "Metal";
            def.AttachmentSlotTag = ID.ToTag();
            def.attachablePosition = new CellOffset(0, 0);
            BuildingConfigManager.Instance.IgnoreDefaultKComponent(
                typeof(RequiresFoundation),
                def.PrefabID.ToTag());
            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            BuildingConfigManager.Instance.IgnoreDefaultKComponent(typeof(RequiresFoundation), prefab_tag);
            go.AddOrGet<LoopingSounds>();
            go.AddOrGet<Operational>();
            go.AddOrGet<AttachableBuilding>().attachableToTag = ID.ToTag();
            BrewingBarrel.BrewingBarrelFabricator fab = go.AddOrGet<BrewingBarrel.BrewingBarrelFabricator>();
            fab.duplicantOperated = false;
            fab.allowManualFluidDelivery = true;
            fab.MaxReadyBatches = 3;
            fab.stirDuration = 600f;
            BuildingTemplates.CreateComplexFabricatorStorage(go, fab);
            fab.outStorage.capacityKg = 100f;
            fab.outStorage.showInUI = true;
            fab.outStorage.storageFilters = new List<Tag> { GameTags.Liquid };

            go.AddOrGet<BuildingAttachPoint>().points = new BuildingAttachPoint.HardPoint[1]
            {
                new BuildingAttachPoint.HardPoint(
                    new CellOffset(0, 1),
                    ID.ToTag(),
                    null)
            };

            fab.sideScreenStyle = ComplexFabricatorSideScreen.StyleSetting.ListQueueHybrid;
            go.AddOrGet<BrewingBarrel.BrewingStirWorkable>();
            go.AddOrGet<BrewingBarrel.BrewingPickupWorkable>();
            EnsureRecipesRegistered();
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
        }

        /// <summary>
        /// 双保险注册"胡辣汤 -> 乙醇"配方，重复调用无副作用。两个调用点：
        ///   1) ConfigureBuildingTemplate：Db 初始化时（默认路径）
        ///   2) Mod.OnLoad：更早的时机，赶在 ComplexRecipeManager.PostProcess() 之前
        ///
        /// 去重逻辑：
        ///   - manager 实例变了（回主菜单再开新档）→ 重置状态、允许重新注册；
        ///   - 同一个 manager 里已经有同 ID 的配方 → 直接跳过。
        /// </summary>
        public static void EnsureRecipesRegistered()
        {
            if (Db.Get() == null)
            {
                return;
            }

            ComplexRecipeManager manager = ComplexRecipeManager.Get();
            if (manager == null)
            {
                return;
            }

            if (manager != cachedRecipeManager)
            {
                cachedRecipeManager = manager;
                recipeRegistered = false;
            }
            if (recipeRegistered)
            {
                return;
            }
            if (manager.GetRecipe(RECIPE_ID) != null)
            {
                recipeRegistered = true;
                return;
            }

            ComplexRecipe.RecipeElement[] ingredients = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(
                    "HuLaTang".ToTag(),
                    1f,
                    ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                    "",
                    false),
                new ComplexRecipe.RecipeElement(
                    GameTags.Water,
                    50f,
                    ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                    "",
                    false),
            };

            ComplexRecipe.RecipeElement[] results = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(
                    SimHashes.Ethanol.CreateTag(),
                    10f,
                    ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature,
                    false),
            };

            ComplexRecipe recipe = new ComplexRecipe(RECIPE_ID, ingredients, results);
            recipe.time = 30f;
            recipe.nameDisplay = ComplexRecipe.RecipeNameDisplay.Result;
            recipe.description = "一碗胡辣汤，酿成一桶乙醇。";
            recipe.fabricators = new List<Tag> { ID.ToTag(), BrewingTowerLayerConfig.ID.ToTag() };
            recipe.sortOrder = 1;

            recipeRegistered = true;
        }
    }
}
