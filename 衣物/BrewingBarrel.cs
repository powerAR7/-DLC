using System.Collections.Generic;
using KSerialization;
using UnityEngine;
using TUNING;

namespace BrewingBarrel
{
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
                2,
                2,
                "ventgas_kanim",                 // 建筑本体动画：排气口
                100,
                30f,
                TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER3,
                TUNING.MATERIALS.REFINED_METALS,
                800f,
                BuildLocationRule.OnFloor,
                TUNING.BUILDINGS.DECOR.PENALTY.TIER1,
                TUNING.NOISE_POLLUTION.NONE,
                0.2f);

            def.Floodable = false;
            def.AudioCategory = "Metal";
            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.AddOrGet<LoopingSounds>();
            go.AddOrGet<Operational>();

            BrewingFabricator fabricator = go.AddOrGet<BrewingFabricator>();
            fabricator.duplicantOperated = false;   // 进度由搅拌计时器控制，不靠小人站桩
            fabricator.sideScreenStyle = ComplexFabricatorSideScreen.StyleSetting.ListQueueHybrid;

            // 制造机自己的输入仓 / 构建仓 / 输出仓（20t，UI 可见），配方取料靠它
            BuildingTemplates.CreateComplexFabricatorStorage(go, fabricator);

            // ComplexFabricator 内部 [MyCmpReq] 需要它；真正干活的是 BrewingWorkable
            go.AddOrGet<ComplexFabricatorWorkable>();

            BrewingWorkable workable = go.AddOrGet<BrewingWorkable>();
            workable.SetWorkerStatusItem(Db.Get().DuplicantStatusItems.Fabricating);

            // 第一道保险：Db 初始化装配建筑时注册（重复调用无副作用）
            EnsureRecipesRegistered();
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<BuildingComplete>().isManuallyOperated = false;
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
            ComplexRecipeManager manager = ComplexRecipeManager.Get();
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
    /// - 只有"正在发酵"时配方进度才推进（ComputeWorkProgress 返回 0 表示卡住）；
    /// - 发酵完成时把液体产物直接生成到世界格子里。
    /// </summary>
    public class BrewingFabricator : ComplexFabricator, ISim1000ms
    {
        [Serialize] private float stirTimeLeft;   // 剩余发酵时间，随存档保存

        public float stirDuration = 600f;         // 一次搅拌维持 600 秒
        public float minWaterMass = 5f;           // 周围格子里至少要有这么多水
        public bool requireWater = true;          // 关掉就变成"只要搅拌就推进"
        public bool spawnInAdjacentCell = false;  // true = 喷到右侧相邻格

        private Chore stirChore;

        public bool IsStirred
        {
            get { return stirTimeLeft > 0f; }
        }

        /// <summary>进度推进的总开关：搅拌窗口 + 环境水。</summary>
        public bool IsBrewing
        {
            get { return IsStirred && (!requireWater || HasWaterAround()); }
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

        /// <summary>小人搅拌完成时调用（由 BrewingWorkable 触发）。</summary>
        public void Stir()
        {
            stirTimeLeft = stirDuration;
            // chore 的清理交给 on_complete 回调和下一次 Sim1000ms，
            // 这里不要主动 Cancel，避免在"工作完成"回调里重入 chore 生命周期。
        }

        /// <summary>
        /// 核心切入点：只有正在发酵时才让配方进度推进。
        /// 返回 0 = 进度冻结（没搅拌 / 周围没水）。
        /// </summary>
        protected override float ComputeWorkProgress(float dt, ComplexRecipe recipe)
        {
            if (!IsBrewing)
            {
                return 0f;
            }

            if (!ConsumeWater(dt))
            {
                return 0f;
            }

            return base.ComputeWorkProgress(dt, recipe);
        }

        /// <summary>
        /// 重写产物生成：液体不走 Assets.GetPrefab，直接用 Substance.SpawnResource 喷进世界。
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
                    SpawnLiquid(element, result.amount);
                }
                else
                {
                    // 本建筑只出液体；万一配了固体产物，这里不调 GetPrefab，避免空引用
                    UnityEngine.Debug.LogWarning("[BrewingBarrel] 非液体产物被跳过: " + result.material);
                }
            }

            return spawned;
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

        /// <summary>
        /// 检查建筑本身及其上下左右四格是否有足够的水。
        /// 液体判定用 Grid.IsLiquid（内部就是 Element.IsLiquid），先 IsValidCell 防越界。
        /// </summary>
        public bool HasWaterAround()
        {
            int cell = Grid.PosToCell(this);
            if (IsWaterCell(cell))
            {
                return true;
            }
            return IsWaterCell(Grid.CellAbove(cell))
                || IsWaterCell(Grid.CellBelow(cell))
                || IsWaterCell(Grid.CellLeft(cell))
                || IsWaterCell(Grid.CellRight(cell));
        }

        private bool IsWaterCell(int cell)
        {
            if (!Grid.IsValidCell(cell) || !Grid.IsLiquid(cell))
            {
                return false;
            }
            if (Grid.Element[cell].id != SimHashes.Water)
            {
                return false;
            }
            return Grid.Mass[cell] >= minWaterMass;
        }

        protected virtual bool ConsumeWater(float dt)
        {
            if (dt <= 0f)
            {
                return false;
            }

            int cell = Grid.PosToCell(this);
            int[] candidateCells =
            {
                cell,
                Grid.CellAbove(cell),
                Grid.CellBelow(cell),
                Grid.CellLeft(cell),
                Grid.CellRight(cell)
            };
            float amount = 0.5f * dt;

            foreach (int candidate in candidateCells)
            {
                if (!IsWaterCell(candidate) || Grid.Mass[candidate] < amount)
                {
                    continue;
                }

                SimMessages.AddRemoveSubstance(
                    candidate,
                    SimHashes.Water,
                    default(CellAddRemoveSubstanceEvent),
                    -amount,
                    Grid.Temperature[candidate],
                    byte.MaxValue,
                    0);
                return true;
            }

            return false;
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
}
