using System;
using System.Collections.Generic;
using HarmonyLib;
using KSerialization;
using UnityEngine;
using TUNING;

namespace FandC
{
    public class BrewingBarrel : KMonoBehaviour, IProgressBarSideScreen
    {
        public Storage LiquidStorage;
        public Storage IngredientStorage;

        public float GetProgressBarMaxValue()
        {
            return 1f;
        }

        private float GetCurrentWorkingProgress()
        {
            if (this == null || gameObject == null)
            {
                return 0f;
            }

            ComplexFabricator fabricator = GetComponent<ComplexFabricator>();
            if (fabricator == null || fabricator.CurrentWorkingOrder == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(fabricator.OrderProgress);
        }

        public Color GetCurrentProgressColor()
        {
            return GetCurrentWorkingProgress() > 0f
                ? new Color(1f, 0.5f, 0f)
                : new Color(0.5f, 0.8f, 1f);
        }

        private float GetStoredHuLaTangUnits()
        {
            if (this == null || gameObject == null)
            {
                return 0f;
            }

            ComplexFabricator fabricator = GetComponent<ComplexFabricator>();
            if (fabricator == null)
            {
                return 0f;
            }

            Tag hulaTag = HuLaTangConfig.ID.ToTag();
            float totalUnits = 0f;
            if (fabricator.inStorage != null)
            {
                totalUnits += fabricator.inStorage.GetAmountAvailable(hulaTag);
            }
            if (fabricator.buildStorage != null)
            {
                totalUnits += fabricator.buildStorage.GetAmountAvailable(hulaTag);
            }

            return totalUnits;
        }

        public float GetProgressBarFillPercentage()
        {
            if (this == null || gameObject == null)
            {
                return 0f;
            }

            ComplexFabricator fabricator = GetComponent<ComplexFabricator>();
            if (fabricator != null && fabricator.CurrentWorkingOrder != null)
            {
                return GetCurrentWorkingProgress();
            }

            float waterMass = LiquidStorage != null ? LiquidStorage.GetMassAvailable(SimHashes.Water) : 0f;
            float currentGrams = GetStoredHuLaTangUnits() * 1000f;

            float waterPercent = Mathf.Clamp01(waterMass / 50f);
            float hulaPercent = Mathf.Clamp01(currentGrams / 1000f);
            return (waterPercent * 0.9f) + (hulaPercent * 0.1f);
        }

        public string GetProgressBarTitleLabel()
        {
            return "发酵与库存";
        }

        public string GetProgressBarLabel()
        {
            ComplexFabricator fabricator = GetComponent<ComplexFabricator>();
            if (fabricator != null && fabricator.CurrentWorkingOrder != null)
            {
                float currentProgress = GetCurrentWorkingProgress();
                return $"酿造中: {currentProgress * 100:F0}%";
            }

            float waterMass = LiquidStorage != null ? LiquidStorage.GetMassAvailable(SimHashes.Water) : 0f;
            float currentGrams = GetStoredHuLaTangUnits() * 1000f;
            return $"备料 - 水: {waterMass:F1}/50 | 料: {currentGrams:F0}/1000 克";
        }

        public string GetProgressBarTooltip()
        {
            return "备料完成后自动开始酿造";
        }
    }

    /// <summary>
    /// 搅拌型酿酒厂（最终版）：
    /// - 配方：胡辣汤 -> 乙醇（液体）
    /// - 小人每搅一次，建筑自动"发酵"一段时间；期间配方进度才会推进
    /// - 发酵完成时乙醇直接喷在建筑格子里，不需要接管子
    /// 本文件不包含任何房间系统（RoomType / RoomProber）代码。
    /// </summary>
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
            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            BrewingBarrel barrel = go.AddOrGet<BrewingBarrel>();
            ConfigureFermenterTemplate(go, ID.ToTag(), true);
            go.AddOrGet<BuildingAttachPoint>().points = new BuildingAttachPoint.HardPoint[1]
            {
                new BuildingAttachPoint.HardPoint(new CellOffset(0, 1), ID.ToTag(), null)
            };
            Storage liquid = go.AddComponent<Storage>();
            liquid.capacityKg = 50f;
            liquid.showInUI = true;
            liquid.storageFilters = new List<Tag> { GameTags.Liquid };
            barrel.LiquidStorage = liquid;
            EnsureRecipesRegistered();
        }

        internal static void ConfigureFermenterTemplate(GameObject go, Tag attachableToTag, bool configureFabricator)
        {
            go.AddOrGet<LoopingSounds>();
            go.AddOrGet<Operational>();

            if (configureFabricator)
            {
                BrewingFabricator fabricator = go.AddOrGet<BrewingFabricator>();
                fabricator.duplicantOperated = false;   // 进度由搅拌计时器控制，不靠小人站桩
                fabricator.sideScreenStyle = ComplexFabricatorSideScreen.StyleSetting.ListQueueHybrid;

                // 配方取料仓也用于接收管道来水，统一限制为 500kg。
                BuildingTemplates.CreateComplexFabricatorStorage(go, fabricator);
            }

            Storage storage = go.AddOrGet<Storage>();
            storage.capacityKg = 500f;
            storage.showInUI = true;
            if (storage.storageFilters == null)
            {
                storage.storageFilters = new List<Tag>();
            }
            else
            {
                storage.storageFilters.Clear();
            }
            Tag waterTag = SimHashes.Water.CreateTag();
            storage.storageFilters.Add(waterTag);
            Tag ethanolTag = SimHashes.Ethanol.CreateTag();
            storage.storageFilters.Add(ethanolTag);
            if (configureFabricator)
            {
                storage.storageFilters.Add("HuLaTang".ToTag());
                BrewingBarrel barrel = go.GetComponent<BrewingBarrel>();
                if (barrel != null)
                {
                    barrel.IngredientStorage = storage;
                }
            }

            AttachableBuilding attachable = go.AddOrGet<AttachableBuilding>();
            attachable.attachableToTag = attachableToTag;

            if (configureFabricator)
            {
                // ComplexFabricator 内部 [MyCmpReq] 需要它；真正干活的是 BrewingWorkable
                go.AddOrGet<ComplexFabricatorWorkable>();

                BrewingWorkable workable = go.AddOrGet<BrewingWorkable>();
                workable.SetWorkerStatusItem(Db.Get().DuplicantStatusItems.Fabricating);
            }

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
            recipe.time = 30f;                     // 配合 ComputeWorkProgress：搅拌后才开始涨
            recipe.nameDisplay = ComplexRecipe.RecipeNameDisplay.Result;
            recipe.description = "一碗胡辣汤，酿成一桶乙醇。";
            recipe.fabricators = new List<Tag> { ID.ToTag() };
            recipe.sortOrder = 1;

            recipeRegistered = true;
        }
    }

    /// <summary>
    /// 搅拌计时器卡进度：
    /// - 小人搅拌一次后 stirTimeLeft = stirDuration；
    /// - 只有"正在发酵"且内置水箱有水时配方进度才推进；
    /// - 发酵完成时把液体产物直接生成到世界格子里。
    /// </summary>
    public class BrewingFabricator : ComplexFabricator, ISim1000ms
    {
        private const float MIN_SAFE_MASS = 5f;
        private const float WATER_MASS_PER_SECOND = 0.5f;
        private const float WATER_BATCH_DURATION = MIN_SAFE_MASS / WATER_MASS_PER_SECOND;

        [Serialize] private float stirTimeLeft;   // 剩余发酵时间，随存档保存
        [Serialize] private float prepaidWaterTime;

        public float stirDuration = 600f;         // 一次搅拌维持 600 秒
        public bool spawnInAdjacentCell = false;  // true = 喷到右侧相邻格
        private bool missingWaterStatusDisplayed;

        private Chore stirChore;

        public bool IsStirred
        {
            get { return stirTimeLeft > 0f; }
        }

        /// <summary>进度推进的总开关：搅拌窗口。</summary>
        public bool IsBrewing
        {
            get { return IsStirred; }
        }

        public override void Sim1000ms(float dt)
        {
            base.Sim1000ms(dt);

            if (stirTimeLeft > 0f)
            {
                stirTimeLeft = Mathf.Max(0f, stirTimeLeft - dt);
            }

            RefreshStirChore();
        }

        private void SpawnStoredEthanol()
        {
            Storage storage = GetComponent<Storage>();
            if (storage == null) return;

            float ethanol = storage.GetMassAvailable(SimHashes.Ethanol);
            int cell = Grid.PosToCell(this);

            if (ethanol <= 0f || !Grid.IsValidCell(cell)) return;

            Element e = ElementLoader.FindElementByHash(SimHashes.Ethanol);
            GameObject spawned = e.substance.SpawnResource(
                Grid.CellToPos(cell),
                ethanol,
                e.defaultValues.temperature,
                0,
                0);

            if (spawned != null)
            {
                storage.ConsumeIgnoringDisease(SimHashes.Ethanol.CreateTag(), ethanol);
            }
        }

        /// <summary>小人搅拌完成时调用（由 BrewingWorkable 触发）。</summary>
        public void Stir()
        {
            stirTimeLeft = stirDuration;
            // chore 的清理交给 on_complete 回调和下一次 Sim1000ms，
            // 这里不要主动 Cancel，避免在"工作完成"回调里重入 chore 生命周期。
        }

        /// <summary>
        /// 核心切入点：只有正在发酵时才让配方进度推进。
        /// 返回 0 = 进度冻结（没搅拌 / 水箱没水）。
        /// </summary>
        protected override float ComputeWorkProgress(float dt, ComplexRecipe recipe)
        {
            if (!IsBrewing)
            {
                return 0f;
            }

            float waterTime = ConsumeWater(dt);
            if (waterTime <= 0f)
            {
                return 0f;
            }

            return base.ComputeWorkProgress(waterTime, recipe);
        }

        /// <summary>
        /// 重写产物生成：液体优先存入塔主控；未连接主控时才喷进世界。
        /// </summary>
        protected override List<GameObject> SpawnOrderProduct(ComplexRecipe recipe)
        {
            List<GameObject> spawned = new List<GameObject>();

            for (int i = 0; i < recipe.results.Length; i++)
            {
                ComplexRecipe.RecipeElement result = recipe.results[i];
                Element element = ElementLoader.GetElement(result.material);

                if (element != null && element.IsLiquid)
                {
                    Storage masterStorage = FindMasterStorage();
                    if (masterStorage == null || !StoreLiquid(masterStorage, element, result.amount))
                    {
                        SpawnLiquid(element, result.amount);
                    }
                }
                else
                {
                    // 本建筑只出液体；万一配了固体产物，这里不调 GetPrefab，避免空引用
                    UnityEngine.Debug.LogWarning("[BrewingBarrel] 非液体产物被跳过: " + result.material);
                }
            }

            return spawned;
        }

        private Storage FindMasterStorage()
        {
            AttachableBuilding attachable = GetComponent<AttachableBuilding>();
            BuildingAttachPoint attachedPoint = attachable != null ? attachable.GetAttachedTo() : null;
            while (attachedPoint != null && attachedPoint.gameObject != null)
            {
                GameObject attachedObject = attachedPoint.gameObject;
                if (attachedObject.GetComponent<BrewingTowerMaster>() != null)
                {
                    return attachedObject.GetComponent<Storage>();
                }

                AttachableBuilding parent = attachedObject.GetComponent<AttachableBuilding>();
                attachedPoint = parent != null ? parent.GetAttachedTo() : null;
            }

            return null;
        }

        private static bool StoreLiquid(Storage storage, Element element, float mass)
        {
            int cell = Grid.PosToCell(storage.gameObject);
            if (!Grid.IsValidCell(cell))
            {
                return false;
            }

            GameObject liquid = element.substance.SpawnResource(
                Grid.CellToPos(cell),
                mass,
                element.defaultValues.temperature,
                0,
                0);
            if (liquid == null)
            {
                return false;
            }

            storage.Store(liquid, false, false);
            return true;
        }

        /// <summary>
        /// 把液体生成到建筑所在的格子（或右侧邻格）。
        /// SpawnResource 的第 6~8 个参数都有默认值，这里用默认：会自然并进格子里的液体。
        /// </summary>
        private void SpawnLiquid(Element element, float mass)
        {
            int cell = Grid.PosToCell(this);
            if (!Grid.IsValidCell(cell))
            {
                return;
            }

            if (spawnInAdjacentCell)
            {
                int adjacent = Grid.CellRight(cell);
                if (Grid.IsValidCell(adjacent))
                {
                    cell = adjacent;
                }
            }

            Vector3 position = Grid.CellToPos(cell);
            element.substance.SpawnResource(
                position,
                mass,
                element.defaultValues.temperature,
                0,
                0);
        }

        /// <summary>按 5kg 一批扣水，并将每批折算为 10 秒可用发酵时间。</summary>
        protected virtual float ConsumeWater(float dt)
        {
            if (dt <= 0f)
            {
                return 0f;
            }

            Storage storage = GetComponent<Storage>();
            Tag waterTag = SimHashes.Water.CreateTag();
            float elapsed = 0f;
            while (dt - elapsed > 0.0001f)
            {
                if (prepaidWaterTime <= 0f)
                {
                    GameObject water = storage != null ? storage.FindFirst(waterTag) : null;
                    PrimaryElement primaryElement = water != null
                        ? water.GetComponent<PrimaryElement>()
                        : null;
                    if (primaryElement == null || primaryElement.Mass < MIN_SAFE_MASS)
                    {
                        if (!missingWaterStatusDisplayed)
                        {
                            GetComponent<KSelectable>().SetStatusItem(
                                Db.Get().StatusItemCategories.Main,
                                Db.Get().BuildingStatusItems.NeedLiquidIn,
                                null);
                            missingWaterStatusDisplayed = true;
                        }
                        break;
                    }

                    storage.ConsumeIgnoringDisease(waterTag, MIN_SAFE_MASS);
                    prepaidWaterTime += WATER_BATCH_DURATION;
                    if (missingWaterStatusDisplayed)
                    {
                        GetComponent<KSelectable>().SetStatusItem(
                            Db.Get().StatusItemCategories.Main,
                            Db.Get().BuildingStatusItems.Normal,
                            null);
                        missingWaterStatusDisplayed = false;
                    }
                }

                float consumedTime = Mathf.Min(dt - elapsed, prepaidWaterTime);
                prepaidWaterTime = Mathf.Max(0f, prepaidWaterTime - consumedTime);
                elapsed += consumedTime;
            }

            return elapsed;
        }

        /// <summary>用一个 WorkChore 让小人来搅拌；只要还没搅拌就维持这个 chore。</summary>
        private void RefreshStirChore()
        {
            if (stirChore != null && IsStirred)
            {
                stirChore.Cancel("已搅拌");
                stirChore = null;
                return;
            }

            if (IsStirred || stirChore != null)
            {
                return;
            }

            // 参数顺序照抄 Compost.CreateFlipChore；第 16 个是 PriorityClass.basic，第 17 个是优先级 5
            stirChore = new WorkChore<BrewingWorkable>(
                Db.Get().ChoreTypes.FlipCompost,
                this,
                null,
                true,
                OnStirChoreEnd,
                null,
                OnStirChoreEnd,
                true,
                null,
                false,
                true,
                null,
                false,
                true,
                true,
                PriorityScreen.PriorityClass.basic,
                5,
                false,
                true);
        }

        private void OnStirChoreEnd(Chore chore)
        {
            stirChore = null;
        }

        protected override void OnCleanUp()
        {
            if (stirChore != null)
            {
                stirChore.Cancel("建筑被拆除");
                stirChore = null;
            }
            base.OnCleanUp();
        }
    }

    /// <summary>
    /// 短时工作：小人只干 15 秒，干完给建筑加一段发酵时间。
    /// 交互动画直接借用肥堆的 anim_interacts_compost_kanim。
    /// </summary>
    public class BrewingWorkable : Workable
    {
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();

            workTime = 15f;
            showProgressBar = true;
            overrideAnims = new KAnimFile[]
            {
                Assets.GetAnim("anim_interacts_compost_kanim"),
            };
            SetWorkerStatusItem(Db.Get().DuplicantStatusItems.Fabricating);
        }

        protected override void OnCompleteWork(WorkerBase worker)
        {
            BrewingFabricator fabricator = GetComponent<BrewingFabricator>();
            if (fabricator != null)
            {
                fabricator.Stir();
            }
        }
    }

    [HarmonyPatch(typeof(ComplexFabricator), "ValidateNextOrder")]
    internal static class ComplexFabricatorValidateNextOrderPatch
    {
        private static bool Prefix(ComplexFabricator __instance)
        {
            ComplexRecipe[] recipes = __instance.GetRecipes();
            return __instance.HasAnyOrder
                && recipes != null
                && recipes.Length > 0
                && __instance.CurrentOrderIdx >= 0
                && __instance.CurrentOrderIdx < recipes.Length;
        }
    }
}
