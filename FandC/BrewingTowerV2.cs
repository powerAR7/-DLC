using System.Collections.Generic;
using HarmonyLib;
using KSerialization;
using UnityEngine;

namespace BrewingBarrel
{
    /// <summary>通用批次酿造基类：不绑定配方，液体原料随配方走，产物按 results 通用处理。</summary>
    public abstract class BrewingFabricatorBase : ComplexFabricator, ISim1000ms, IProgressBarSideScreen
    {
        public int MaxReadyBatches = 3;
        public float stirDuration = 600f;

        [Serialize] protected float stirTimeLeft;
        [Serialize] protected float lastBatchMass;
        [Serialize] protected Tag lastProductTag;

        public virtual bool IsStirred
        {
            get { return stirTimeLeft > 0f; }
        }

        /// <summary>所在塔的主控；单体未连接时为 null。用于统一屏蔽取料广播。</summary>
        public virtual BrewingTowerMaster Master
        {
            get { return null; }
        }

        public virtual void Stir()
        {
            stirTimeLeft = stirDuration;
        }

        // ===== IProgressBarSideScreen：让每台酿造机在侧屏显示实时酿造进度 =====
        public float GetProgressBarMaxValue()
        {
            return 1f;
        }

        public float GetProgressBarFillPercentage()
        {
            return Mathf.Clamp01(OrderProgress);
        }

        public string GetProgressBarTitleLabel()
        {
            return "酿造进度";
        }

        public string GetProgressBarLabel()
        {
            return (Mathf.Clamp01(OrderProgress) * 100f).ToString("F0") + "%";
        }

        public string GetProgressBarTooltip()
        {
            return "当前批次的酿造进度";
        }

        protected virtual Storage OutputStorage
        {
            get { return outStorage; }
        }

        public int ReadyBatches
        {
            get
            {
                if (OutputStorage == null || lastBatchMass <= 0.001f) return 0;
                return Mathf.FloorToInt(OutputStorage.MassStored() / lastBatchMass);
            }
        }

        public bool OutputFull
        {
            get
            {
                if (OutputStorage == null) return true;
                return OutputStorage.RemainingCapacity() <= 0f || ReadyBatches >= MaxReadyBatches;
            }
        }

        public override void Sim1000ms(float dt)
        {
            base.Sim1000ms(dt);
            if (stirTimeLeft > 0f) stirTimeLeft = Mathf.Max(0f, stirTimeLeft - dt);
        }

        protected override float ComputeWorkProgress(float dt, ComplexRecipe recipe)
        {
            if (!IsStirred || OutputFull) return 0f;
            return base.ComputeWorkProgress(dt, recipe);
        }

        /// <summary>液体进 OutputStorage，固体走原版 prefab；不写死任何元素。</summary>
        protected override List<GameObject> SpawnOrderProduct(ComplexRecipe recipe)
        {
            List<GameObject> spawned = new List<GameObject>();
            Storage target = OutputStorage;
            if (target == null || recipe == null) return spawned;

            float batchMass = 0f;
            for (int i = 0; i < recipe.results.Length; i++)
            {
                ComplexRecipe.RecipeElement result = recipe.results[i];
                Element element = ElementLoader.GetElement(result.material);

                if (element != null && element.IsLiquid)
                {
                    target.AddLiquid(element.id, result.amount, element.defaultValues.temperature, 0, 0, false, false);
                    lastProductTag = result.material;
                    batchMass += result.amount;
                    continue;
                }

                GameObject prefab = Assets.GetPrefab(result.material);
                if (prefab == null) continue;

                GameObject go = GameUtil.KInstantiate(prefab, transform.position, Grid.SceneLayer.Ore);
                target.Store(go, false, false, true, false);
                spawned.Add(go);
                batchMass += result.amount;
            }

            lastBatchMass = Mathf.Max(0.001f, batchMass);

            // 原版在完工时要求 buildStorage 为空；这里显式清掉本批原料，避免 51kg 残留警告
            if (buildStorage != null)
                buildStorage.ConsumeAllIgnoringDisease();

            return spawned;
        }
    }

    /// <summary>单体桶：自带搅拌 + 待取仓 + 小人取货；不接管道、不连主控。</summary>
    public class BrewingBarrelFabricator : BrewingFabricatorBase
    {
        public float pickupWorkTime = 5f;

        private AttachableBuilding attachable;
        private BrewingTowerMaster master;
        private Chore stirChore;
        private Chore pickupChore;

        public override BrewingTowerMaster Master
        {
            get { return master; }
        }

        protected override Storage OutputStorage
        {
            get
            {
                if (master != null && master.OutputStorage != null)
                    return master.OutputStorage;
                return outStorage;
            }
        }

        public override bool IsStirred
        {
            get { return master != null ? master.IsStirred : base.IsStirred; }
        }

        public override void Stir()
        {
            if (master != null)
                master.Stir();
            else
                base.Stir();
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            attachable = GetComponent<AttachableBuilding>();
            if (attachable != null)
                attachable.onAttachmentNetworkChanged += OnNetworkChanged;
            RefreshMaster();
        }

        protected override void OnCleanUp()
        {
            if (attachable != null)
                attachable.onAttachmentNetworkChanged -= OnNetworkChanged;
            CancelChores();
            base.OnCleanUp();
        }

        public override void Sim1000ms(float dt)
        {
            base.Sim1000ms(dt);
            RefreshMaster();
            if (master != null)
            {
                CancelChores();
                return;
            }
            RefreshStirChore();
            RefreshPickupChore();
        }

        public void RefreshMaster()
        {
            master = null;
            if (attachable == null)
                return;

            List<GameObject> below = new List<GameObject>();
            AttachableBuilding.GetAttachedBelow(attachable, ref below);
            for (int i = 0; i < below.Count; i++)
            {
                if (below[i] == null || below[i] == gameObject)
                    continue;

                BrewingTowerMaster found = below[i].GetComponent<BrewingTowerMaster>();
                if (found != null)
                {
                    master = found;
                    break;
                }
            }

            if (master != null)
                CancelChores();
        }

        private void OnNetworkChanged(object who)
        {
            RefreshMaster();
        }

        private void CancelChores()
        {
            if (stirChore != null)
            {
                stirChore.Cancel("已连接酿造塔");
                stirChore = null;
            }
            if (pickupChore != null)
            {
                pickupChore.Cancel("已连接酿造塔");
                pickupChore = null;
            }
        }

        private void RefreshStirChore()
        {
            if (stirChore != null && IsStirred)
            {
                stirChore.Cancel("已搅拌");
                stirChore = null;
                return;
            }
            if (IsStirred || stirChore != null) return;

            stirChore = new WorkChore<BrewingStirWorkable>(
                Db.Get().ChoreTypes.FlipCompost, this, null, true,
                OnStirDone, null, OnStirDone, true, null, false, true, null,
                false, true, true, PriorityScreen.PriorityClass.basic, 5, false, true);
        }

        private void OnStirDone(Chore chore) { stirChore = null; }

        private void RefreshPickupChore()
        {
            bool hasProduct = outStorage != null && outStorage.MassStored() > 0.001f;
            if (pickupChore != null && !hasProduct)
            {
                pickupChore.Cancel("已取完");
                pickupChore = null;
                return;
            }
            if (!hasProduct || pickupChore != null) return;

            pickupChore = new WorkChore<BrewingPickupWorkable>(
                Db.Get().ChoreTypes.EmptyStorage, this, null, true,
                OnPickupDone, null, OnPickupDone, true, null, false, true, null,
                false, true, true, PriorityScreen.PriorityClass.basic, 5, false, true);
        }

        private void OnPickupDone(Chore chore) { pickupChore = null; }

        /// <summary>小人取走一批产物：优先给瓶装物，找不到瓶装 prefab 时把液体块塞进小人背包。</summary>
        public void GiveProductToWorker(WorkerBase worker)
        {
            if (outStorage == null || worker == null) return;
            float amount = Mathf.Min(outStorage.GetMassAvailable(lastProductTag), Mathf.Max(0.001f, lastBatchMass));
            if (amount <= 0f) return;

            Element element = ElementLoader.GetElement(lastProductTag);
            if (element == null || !element.IsLiquid) return;

            Storage workerStorage = worker.GetComponent<Storage>();
            if (workerStorage == null) return;

            GameObject bottle = FindBottlePrefab(element);
            if (bottle != null)
            {
                GameObject go = GameUtil.KInstantiate(bottle, transform.position, Grid.SceneLayer.Ore);
                if (go != null)
                {
                    workerStorage.Store(go, false, false, true, false);
                    outStorage.ConsumeIgnoringDisease(lastProductTag, amount);
                    return;
                }
            }

            // 兜底：没有瓶装 prefab 时，生成液体块塞进小人背包，绝不落地
            GameObject chunk = element.substance.SpawnResource(transform.position, amount, element.defaultValues.temperature, 0, 0);
            if (chunk != null)
            {
                workerStorage.Store(chunk, false, false, true, false);
                outStorage.ConsumeIgnoringDisease(lastProductTag, amount);
            }
        }

        private static GameObject FindBottlePrefab(Element element)
        {
            string id = element.tag.ToString();
            string[] candidates = { "Bottled" + id, id + "Bottle", "Bottle" + id };
            for (int i = 0; i < candidates.Length; i++)
            {
                GameObject prefab = Assets.GetPrefab(candidates[i].ToTag());
                if (prefab != null) return prefab;
            }
            return null;
        }
    }

    /// <summary>塔层：不接管道、无 Workable，搅拌看主控，产物写主控输出仓。</summary>
    public class BrewingTowerLayer : BrewingFabricatorBase
    {
        private BrewingTowerMaster master;

        public override BrewingTowerMaster Master
        {
            get { return master; }
        }

        public override bool IsStirred
        {
            get { return master != null && master.IsStirred; }
        }

        protected override Storage OutputStorage
        {
            get
            {
                if (master != null && master.OutputStorage != null)
                    return master.OutputStorage;
                return outStorage;
            }
        }

        protected override bool HasIngredients(ComplexRecipe recipe, Storage storage)
        {
            if (base.HasIngredients(recipe, storage))
                return true;

            if (master == null)
                return false;

            master.SupplyIngredientsTo(recipe, storage);
            return base.HasIngredients(recipe, storage);
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            RefreshMaster();
        }

        public void RefreshMaster()
        {
            master = null;
            AttachableBuilding attachable = GetComponent<AttachableBuilding>();
            if (attachable == null) return;

            List<GameObject> below = new List<GameObject>();
            AttachableBuilding.GetAttachedBelow(attachable, ref below);
            for (int i = 0; i < below.Count; i++)
            {
                if (below[i] == null || below[i] == gameObject) continue;
                BrewingTowerMaster found = below[i].GetComponent<BrewingTowerMaster>();
                if (found != null) { master = found; return; }
            }
        }
    }

    /// <summary>塔主控：管道进水/出酒、按配方液体原料给上层配液、统一搅拌。</summary>
    public class BrewingTowerMaster : KMonoBehaviour, ISim1000ms
    {
        public Storage WaterStorage;
        public Storage IngredientStorage;
        public Storage OutputStorage;
        public float stirDuration = 600f;

        [Serialize] private float stirTimeLeft;

        private AttachableBuilding attachable;
        private List<GameObject> above = new List<GameObject>();
        private Chore stirChore;
        private readonly List<FetchList2> fetchLists = new List<FetchList2>();
        public bool debugLog = true;
        private float lastDebugLog;

        public bool IsStirred
        {
            get { return stirTimeLeft > 0f; }
        }

        protected override void OnSpawn()
        {
            base.OnSpawn();
            attachable = GetComponent<AttachableBuilding>();
            if (attachable != null) attachable.onAttachmentNetworkChanged += OnNetworkChanged;
            RebuildLayers();
        }

        protected override void OnCleanUp()
        {
            if (attachable != null) attachable.onAttachmentNetworkChanged -= OnNetworkChanged;
            base.OnCleanUp();
        }

        private void OnNetworkChanged(object who) { RebuildLayers(); }

        private void RebuildLayers()
        {
            above.Clear();
            if (attachable == null) return;

            // 整张附着网络里除自己以外的所有建筑都算塔体（不再按高度过滤，避免漏掉同层/多列）
            List<GameObject> network = AttachableBuilding.GetAttachedNetwork(attachable);
            for (int i = 0; i < network.Count; i++)
            {
                GameObject go = network[i];
                if (go == null || go == gameObject) continue;
                above.Add(go);
            }
            for (int i = 0; i < above.Count; i++)
            {
                BrewingTowerLayer layer = above[i] == null ? null : above[i].GetComponent<BrewingTowerLayer>();
                if (layer != null) layer.RefreshMaster();
                BrewingBarrelFabricator barrel = above[i] == null ? null : above[i].GetComponent<BrewingBarrelFabricator>();
                if (barrel != null) barrel.RefreshMaster();
            }
        }

        public void Sim1000ms(float dt)
        {
            if (stirTimeLeft > 0f) stirTimeLeft = Mathf.Max(0f, stirTimeLeft - dt);
            RebuildLayers();
            RefreshStirChore();
            DistributeIngredients();
            UpdateFetches();
        }

        /// <summary>主控自己向殖民地喊料：按上层队列汇总缺口，分别补到液体仓/固体仓。</summary>
        private void UpdateFetches()
        {
            for (int i = fetchLists.Count - 1; i >= 0; i--)
            {
                if (fetchLists[i] == null || fetchLists[i].IsComplete)
                    fetchLists.RemoveAt(i);
            }

            Dictionary<Tag, float> needs = new Dictionary<Tag, float>();
            for (int i = 0; i < above.Count; i++)
            {
                BrewingFabricatorBase fab = above[i] == null ? null : above[i].GetComponent<BrewingFabricatorBase>();
                if (fab == null) continue;

                ComplexRecipe[] recipes = fab.GetRecipes();
                for (int r = 0; r < recipes.Length; r++)
                {
                    int queued = fab.GetRecipeQueueCount(recipes[r]);
                    if (queued <= 0) continue;

                    for (int g = 0; g < recipes[r].ingredients.Length; g++)
                    {
                        Tag tag = recipes[r].ingredients[g].material;
                        float amount = recipes[r].ingredients[g].amount * queued;
                        needs[tag] = needs.TryGetValue(tag, out float old) ? old + amount : amount;
                    }
                }
            }

            foreach (KeyValuePair<Tag, float> pair in needs)
            {
                Element element = ElementLoader.GetElement(pair.Key);
                Storage dest = element != null && element.IsLiquid ? WaterStorage : IngredientStorage;
                if (dest == null) continue;

                float deficit = pair.Value - dest.GetMassAvailable(pair.Key);
                if (deficit <= 0.001f || HasPendingFetch(pair.Key)) continue;

                FetchList2 list = new FetchList2(dest, Db.Get().ChoreTypes.Fetch);
                list.Add(pair.Key, null, deficit, Operational.State.Active);
                list.ShowStatusItem = false;
                list.Submit(null, false);
                fetchLists.Add(list);
            }
        }

        private bool HasPendingFetch(Tag tag)
        {
            for (int i = 0; i < fetchLists.Count; i++)
            {
                FetchList2 list = fetchLists[i];
                if (list == null || list.IsComplete) continue;

                Dictionary<Tag, float> remaining = list.GetRemaining();
                if (remaining != null && remaining.TryGetValue(tag, out float amount) && amount > 0f)
                    return true;
            }
            return false;
        }

        private void DistributeIngredients()
        {
            bool doLog = debugLog && Time.time - lastDebugLog > 5f;
            if (doLog)
            {
                lastDebugLog = Time.time;
                Debug.Log("[Brewing] master above=" + above.Count);
            }

            for (int i = 0; i < above.Count; i++)
            {
                BrewingFabricatorBase fabricator = above[i] == null
                    ? null
                    : above[i].GetComponent<BrewingFabricatorBase>();
                if (fabricator == null)
                    continue;

                ComplexRecipe[] recipes = fabricator.GetRecipes();
                if (doLog)
                    Debug.Log("[Brewing] " + above[i].name + " recipes=" + recipes.Length + " stored=" + fabricator.inStorage.MassStored().ToString("F1") + "kg");
                if (recipes.Length == 0)
                    continue;

                BrewingTowerLayer layer = fabricator as BrewingTowerLayer;
                if (layer != null)
                {
                    // 只发料、不排单；正在生产时不补料，避免重复投料被原版当"多余原料"吐出来
                    if (fabricator.CurrentWorkingOrder != null)
                        continue;

                    for (int r = 0; r < recipes.Length; r++)
                    {
                        int queued = layer.GetRecipeQueueCount(recipes[r]);
                        if (queued > 0)
                            SupplyIngredientsTo(recipes[r], layer.inStorage, queued);
                    }
                }
                else
                {
                    // 只发料、不排单；正在生产时不补料，避免重复投料被原版当"多余原料"吐出来
                    if (fabricator.CurrentWorkingOrder != null)
                        continue;

                    for (int r = 0; r < recipes.Length; r++)
                    {
                        int queued = fabricator.GetRecipeQueueCount(recipes[r]);
                        if (queued > 0)
                            SupplyBarrelIngredients(fabricator, recipes[r], queued);
                    }
                }
            }
        }

        public void SupplyIngredientsTo(ComplexRecipe recipe, Storage storage, int batches = 1)
        {
            if (storage == null || recipe == null)
                return;

            if (batches < 1)
                batches = 1;

            for (int i = 0; i < recipe.ingredients.Length; i++)
            {
                ComplexRecipe.RecipeElement ingredient = recipe.ingredients[i];
                Tag tag = ingredient.material;
                Element element = ElementLoader.GetElement(tag);
                float current = storage.GetMassAvailable(tag);
                float deficit = ingredient.amount * batches - current;
                float room = storage.RemainingCapacity();
                if (deficit <= 0f || room <= 0f)
                    continue;

                float amount;
                if (element != null && element.IsLiquid)
                {
                    if (WaterStorage == null)
                        continue;

                    amount = Mathf.Min(
                        deficit,
                        Mathf.Min(room, WaterStorage.GetMassAvailable(tag)));
                    if (amount <= 0f)
                        continue;

                    PrimaryElement added = storage.AddLiquid(
                        element.id,
                        amount,
                        element.defaultValues.temperature,
                        0,
                        0,
                        false,
                        false);
                    if (added != null)
                        WaterStorage.ConsumeIgnoringDisease(tag, amount);
                }
                else
                {
                    if (IngredientStorage == null)
                        continue;

                    amount = Mathf.Min(
                        deficit,
                        Mathf.Min(room, IngredientStorage.GetMassAvailable(tag)));
                    if (amount > 0f)
                        IngredientStorage.Transfer(storage, tag, amount, false, false);
                }
            }
        }

        private void SupplyBarrelIngredients(BrewingFabricatorBase fabricator, ComplexRecipe recipe, int batches)
        {
            if (fabricator == null || fabricator.inStorage == null || recipe == null)
                return;

            if (batches < 1)
                batches = 1;

            for (int i = 0; i < recipe.ingredients.Length; i++)
            {
                Tag tag = recipe.ingredients[i].material;
                Element element = ElementLoader.GetElement(tag);
                Storage source = element != null && element.IsLiquid ? WaterStorage : IngredientStorage;
                if (source == null)
                    continue;

                float deficit = recipe.ingredients[i].amount * batches -
                    fabricator.inStorage.GetMassAvailable(tag);
                float amount = Mathf.Min(
                    deficit,
                    Mathf.Min(fabricator.inStorage.RemainingCapacity(), source.GetMassAvailable(tag)));
                if (amount > 0f)
                    source.Transfer(fabricator.inStorage, tag, amount, false, false);
            }
        }

        public void Stir()
        {
            stirTimeLeft = stirDuration;
        }

        private void RefreshStirChore()
        {
            if (stirChore != null && IsStirred)
            {
                stirChore.Cancel("已搅拌");
                stirChore = null;
                return;
            }
            if (IsStirred || stirChore != null) return;

            stirChore = new WorkChore<BrewingStirWorkable>(
                Db.Get().ChoreTypes.FlipCompost, this, null, true,
                OnStirDone, null, OnStirDone, true, null, false, true, null,
                false, true, true, PriorityScreen.PriorityClass.basic, 5, false, true);
        }

        private void OnStirDone(Chore chore) { stirChore = null; }
    }

    /// <summary>搅拌工作：单体桶直接 Stir，主控则给整塔加发酵时间。</summary>
    public class BrewingStirWorkable : Workable
    {
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            workTime = 15f;
            showProgressBar = true;
            overrideAnims = new KAnimFile[] { Assets.GetAnim("anim_interacts_compost_kanim") };
            SetWorkerStatusItem(Db.Get().DuplicantStatusItems.Fabricating);
        }

        protected override void OnCompleteWork(WorkerBase worker)
        {
            BrewingTowerMaster master = GetComponent<BrewingTowerMaster>();
            if (master != null) { master.Stir(); return; }

            BrewingBarrelFabricator barrel = GetComponent<BrewingBarrelFabricator>();
            if (barrel != null) barrel.Stir();
        }
    }

    /// <summary>取货工作：把一批产物交给小人（瓶装优先，液体块兜底）。</summary>
    public class BrewingPickupWorkable : Workable
    {
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            workTime = 5f;
            showProgressBar = true;
            SetWorkerStatusItem(Db.Get().DuplicantStatusItems.Fabricating);
        }

        protected override void OnCompleteWork(WorkerBase worker)
        {
            BrewingBarrelFabricator barrel = GetComponent<BrewingBarrelFabricator>();
            if (barrel != null) barrel.GiveProductToWorker(worker);
        }
    }

    [HarmonyPatch(typeof(ComplexFabricator), "UpdateFetches")]
    internal static class BrewingTowerLayerOrderPatch
    {
        private static readonly System.Reflection.FieldInfo MaterialNeedCacheField =
            typeof(ComplexFabricator).GetField("materialNeedCache",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private static bool Prefix(ComplexFabricator __instance)
        {
            // 任何已接入主控的酿造机（塔层 或 单体桶）都不再向殖民地广播取料任务
            BrewingFabricatorBase fabricator = __instance.GetComponent<BrewingFabricatorBase>();
            if (fabricator == null || fabricator.Master == null)
                return true;

            // 顺手清掉需求缓存，否则原版每 tick 刷 "no open fetches" 警告
            try
            {
                if (MaterialNeedCacheField != null)
                {
                    Dictionary<Tag, float> cache =
                        MaterialNeedCacheField.GetValue(__instance) as Dictionary<Tag, float>;
                    if (cache != null) cache.Clear();
                }
            }
            catch
            {
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ComplexFabricatorSideScreen), "IsValidForTarget")]
    internal static class BrewingTowerLayerSideScreenPatch
    {
        private static bool Prefix(GameObject target, ref bool __result)
        {
            if (target == null || target.GetComponent<BrewingTowerLayer>() == null)
                return true;

            __result = false;
            return false;
        }
    }

}









