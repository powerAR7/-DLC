using System.Collections.Generic;
using STRINGS;
using TUNING;
using UnityEngine;

public class StandaloneBuildingConfig : IBuildingConfig
{
    // 保留现有 ID，避免更改后影响已放置建筑的存档数据。
    public const string ID = "StandaloneBuilding";

    public override BuildingDef CreateBuildingDef()
    {
        // 使用原版储物柜动画，建筑占地为 1×2 格。
        BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef(
            ID,
            1,
            2,
            "storagelocker_kanim",
            30,
            10f,
            TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER4,
            MATERIALS.RAW_MINERALS_OR_METALS,
            1600f,
            BuildLocationRule.OnFloor,
            TUNING.BUILDINGS.DECOR.PENALTY.TIER1,
            NOISE_POLLUTION.NONE,
            0.2f);
       
        // 强制从资源库拿取动画（因为我们用同名覆盖了，所以拿到的会是你改色的图）
        //buildingDef.AnimFiles = new KAnimFile[] { Assets.GetAnim("wardrobe_kanim") };
        
        buildingDef.Floodable = false;
        buildingDef.AudioCategory = "Metal";
        buildingDef.Overheatable = false;
        buildingDef.AddSearchTerms(SEARCH_TERMS.STORAGE);
        return buildingDef;
    }

    public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
    {
        SoundEventVolumeCache.instance.AddVolume(
            "storagelocker_kanim",
            "StorageLocker_Hit_metallic_low",
            NOISE_POLLUTION.NOISY.TIER1);
        Prioritizable.AddRef(go);

        Storage storage = go.AddOrGet<Storage>();
        storage.capacityKg = 600f;
        storage.showInUI = true;
        storage.allowItemRemoval = true;
        storage.showDescriptor = true;
        // 使用单一过滤标签，储物柜只接受衣物。
        storage.storageFilters = new List<Tag> { GameTags.Clothes };
        storage.storageFullMargin = STORAGE.STORAGE_LOCKER_FILLED_MARGIN;
        storage.fetchCategory = Storage.FetchCategory.GeneralStorage;
        storage.showCapacityStatusItem = true;
        storage.showCapacityAsMainStatus = true;

        go.AddOrGet<CopyBuildingSettings>().copyGroupTag = GameTags.StorageLocker;
        go.AddOrGet<StorageLocker>();
        go.AddOrGet<UserNameable>();
        go.AddOrGetDef<RocketUsageRestriction.Def>();
    }

    public override void DoPostConfigureComplete(GameObject go)
    {
        go.AddOrGetDef<StorageController.Def>();
    }
}