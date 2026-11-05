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
                1,
                1,
                "ventgas_kanim",
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
            def.InputConduitType = ConduitType.Liquid;
            def.UtilityInputOffset = new CellOffset(0, 0);
            def.AttachmentSlotTag = ID.ToTag();
            def.attachablePosition = new CellOffset(0, 0);
            return def;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            BrewingBarrelConfig.ConfigureFermenterTemplate(go, ID.ToTag(), false);

            go.AddOrGet<BuildingAttachPoint>().points = new BuildingAttachPoint.HardPoint[1]
            {
                new BuildingAttachPoint.HardPoint(
                    new CellOffset(0, 1),
                    BrewingBarrelConfig.ID.ToTag(),
                    null)
            };

            ConduitConsumer consumer = go.AddOrGet<ConduitConsumer>();
            consumer.conduitType = ConduitType.Liquid;
            consumer.consumptionRate = 10f;
            consumer.capacityTag = SimHashes.Water.CreateTag();
            consumer.capacityKG = 500f;

            go.AddOrGet<BrewingTowerMaster>();
        }

        public override void DoPostConfigureComplete(GameObject go)
        {
        }
    }
}
