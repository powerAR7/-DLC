using System.Collections.Generic;
using UnityEngine;
using TUNING;

namespace FandC
{
    public class BrewingTowerMasterConfig : IBuildingConfig
    {
        public const string ID = "BrewingTowerMaster";

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                ID,
                5,
                1,
                "multi_dupe_table_kanim",
                10,
                10f,
                TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER4,
                TUNING.MATERIALS.WOODS,
                1600f,
                BuildLocationRule.OnFloor,
                TUNING.BUILDINGS.DECOR.BONUS.TIER2,
                TUNING.NOISE_POLLUTION.NONE,
                0.2f);

            def.Floodable = false;
            def.AudioCategory = "Metal";
            def.Overheatable = false;
            def.AttachmentSlotTag = ID.ToTag();
            def.attachablePosition = new CellOffset(0, 0);
            def.InputConduitType = ConduitType.Liquid;
            def.OutputConduitType = ConduitType.Liquid;
            def.UtilityInputOffset = new CellOffset(-2, 0);
            def.UtilityOutputOffset = new CellOffset(2, 0);
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

            go.AddOrGet<BuildingAttachPoint>().points = new BuildingAttachPoint.HardPoint[10]
            {
                new BuildingAttachPoint.HardPoint(new CellOffset(-2, 1), BrewingTowerLayerConfig.ID.ToTag(), null),
                new BuildingAttachPoint.HardPoint(new CellOffset(-2, 1), BrewingBarrelConfig.ID.ToTag(), null),
                new BuildingAttachPoint.HardPoint(new CellOffset(-1, 1), BrewingTowerLayerConfig.ID.ToTag(), null),
                new BuildingAttachPoint.HardPoint(new CellOffset(-1, 1), BrewingBarrelConfig.ID.ToTag(), null),
                new BuildingAttachPoint.HardPoint(new CellOffset(0, 1), BrewingTowerLayerConfig.ID.ToTag(), null),
                new BuildingAttachPoint.HardPoint(new CellOffset(0, 1), BrewingBarrelConfig.ID.ToTag(), null),
                new BuildingAttachPoint.HardPoint(new CellOffset(1, 1), BrewingTowerLayerConfig.ID.ToTag(), null),
                new BuildingAttachPoint.HardPoint(new CellOffset(1, 1), BrewingBarrelConfig.ID.ToTag(), null),
                new BuildingAttachPoint.HardPoint(new CellOffset(2, 1), BrewingTowerLayerConfig.ID.ToTag(), null),
                new BuildingAttachPoint.HardPoint(new CellOffset(2, 1), BrewingBarrelConfig.ID.ToTag(), null)
            };

            BrewingBarrel.BrewingTowerMaster master =
                go.AddOrGet<BrewingBarrel.BrewingTowerMaster>();

            Storage water = go.AddComponent<Storage>();
            water.capacityKg = 200f;
            water.showInUI = true;
            water.storageFilters = new List<Tag> { GameTags.Liquid };
            master.WaterStorage = water;

            Storage ingredients = go.AddComponent<Storage>();
            ingredients.capacityKg = 200f;
            ingredients.showInUI = true;
            ingredients.storageFilters = new List<Tag> { "HuLaTang".ToTag() };
            master.IngredientStorage = ingredients;

            Storage output = go.AddComponent<Storage>();
            output.capacityKg = 300f;
            output.showInUI = true;
            output.storageFilters = new List<Tag> { GameTags.Liquid };
            master.OutputStorage = output;

            ConduitConsumer inPipe = go.AddOrGet<ConduitConsumer>();
            inPipe.conduitType = ConduitType.Liquid;
            inPipe.storage = water;

            ConduitDispenser outPipe = go.AddOrGet<ConduitDispenser>();
            outPipe.conduitType = ConduitType.Liquid;
            outPipe.storage = output;

            go.AddOrGet<BrewingBarrel.BrewingStirWorkable>();
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
        }
    }

    public class BrewingTowerLayerConfig : IBuildingConfig
    {
        public const string ID = "BrewingTowerLayer";

        public override BuildingDef CreateBuildingDef()
        {
            BuildingDef def = BuildingTemplates.CreateBuildingDef(
                ID,
                1,
                1,
                "ventgas_kanim",
                100,
                30f,
                TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER3,
                TUNING.MATERIALS.REFINED_METALS,
                800f,
                BuildLocationRule.BuildingAttachPoint,
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
            BrewingBarrel.BrewingTowerLayer layer = go.AddOrGet<BrewingBarrel.BrewingTowerLayer>();
            layer.duplicantOperated = false;
            BuildingTemplates.CreateComplexFabricatorStorage(go, layer);
            Storage solidInput = go.AddComponent<Storage>();
            solidInput.capacityKg = 200f;
            solidInput.storageFilters = new List<Tag> { "HuLaTang".ToTag() };
            layer.inStorage = solidInput;
            layer.outStorage.showInUI = true;
            layer.outStorage.storageFilters = new List<Tag> { GameTags.Liquid };

            AttachableBuilding attachable = go.AddOrGet<AttachableBuilding>();
            attachable.attachableToTag = BrewingTowerLayerConfig.ID.ToTag();
            go.AddOrGet<BuildingAttachPoint>().points = new BuildingAttachPoint.HardPoint[2]
            {
                new BuildingAttachPoint.HardPoint(
                    new CellOffset(0, 1),
                    BrewingTowerLayerConfig.ID.ToTag(),
                    null),
                new BuildingAttachPoint.HardPoint(
                    new CellOffset(0, 1),
                    BrewingBarrelConfig.ID.ToTag(),
                    null)
            };
            BrewingBarrelConfig.EnsureRecipesRegistered();
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
        }
    }
}
