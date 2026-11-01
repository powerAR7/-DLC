using System;
using System.Collections.Generic;
using System.IO;
using STRINGS;
using TUNING;
using UnityEngine;
using HarmonyLib;

// ONI 通过 UserMod2 发现 Mod 入口；调用基类以启用本程序集中的 Harmony 补丁。
public class StandaloneBuildingMod : KMod.UserMod2
{
    public static KAnimFile SewingAnim;
    private const string SewingAnimationDirectory = @"C:\Users\m1567\Documents\Klei\OxygenNotIncluded\mods\Dev\缝纫机\anim\sewingmachine";

    public override void OnLoad(Harmony harmony)
    {
        base.OnLoad(harmony);
        MyMod.Clothing.ClothingMod.Initialize();
        
        LocString.CreateLocStringKeys(typeof(MyModStrings), "STRINGS");

        // 直接使用 ModUtil 挂载建筑。
        // 第一个参数 "MyCustomCategory" 是全新的分类名（游戏会自动为你新建一个分类标签）
        // 第二个参数 "StandaloneBuilding" 是你的建筑ID
        ModUtil.AddBuildingToPlanScreen("MyCustomCategory", "StandaloneBuilding");
    }

    internal static void LoadSewingAnimation()
    {
        if (SewingAnim != null)
            return;

        string texturePath = Path.Combine(SewingAnimationDirectory, "sewingmachine_0.png");
        string animPath = Path.Combine(SewingAnimationDirectory, "sewingmachine_anim.bytes");
        string buildPath = Path.Combine(SewingAnimationDirectory, "sewingmachine_build.bytes");
        foreach (string assetPath in new[] { texturePath, animPath, buildPath })
        {
            if (!File.Exists(assetPath))
                throw new FileNotFoundException("Missing sewing machine animation asset.", assetPath);
        }

        KAnimFile.Mod animationMod = new KAnimFile.Mod();
        animationMod.anim = File.ReadAllBytes(animPath);
        animationMod.build = File.ReadAllBytes(buildPath);
        Texture2D texture = ModUtil.LoadTexture(texturePath);
        if (texture == null)
            throw new InvalidDataException("Failed to load sewing machine animation texture: " + texturePath);
        animationMod.textures.Add(texture);

        if (!animationMod.IsValid())
            throw new InvalidDataException("The sewing machine animation assets are incomplete in " + SewingAnimationDirectory);

        SewingAnim = ModUtil.AddKAnimMod("sewingmachine_kanim", animationMod);
    }
}

[HarmonyPatch(typeof(KAnimGroupFile), nameof(KAnimGroupFile.LoadGroupResourceFile))]
public static class SewingMachineAnimationRegistrationPatch
{
    public static void Postfix()
    {
        StandaloneBuildingMod.LoadSewingAnimation();
    }
}

public class SewingMachineConfig : IBuildingConfig
{
   
    public override BuildingDef CreateBuildingDef()
    {
        string id = "SewingMachine";
        int width = 2;
        int height = 2;
        string anim = "sewingmachine_kanim";
        int hitpoints = 100;
        float construction_time = 240f;
        float[] tier = TUNING.BUILDINGS.CONSTRUCTION_MASS_KG.TIER3;
        string[] refined_METALS = MATERIALS.REFINED_METALS;
        float melting_point = 1600f;
        BuildLocationRule build_location_rule = BuildLocationRule.OnFloor;
        EffectorValues tier2 = NOISE_POLLUTION.NOISY.TIER5;
        BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef(id, width, height, anim, hitpoints, construction_time, tier, refined_METALS, melting_point, build_location_rule, TUNING.BUILDINGS.DECOR.NONE, tier2, 0.2f);
        buildingDef.RequiresPowerInput = false;//电源输入设置：关闭（不需要供电）
        //buildingDef.EnergyConsumptionWhenActive = 240f;
        buildingDef.ViewMode = OverlayModes.Power.ID;
        buildingDef.AudioCategory = "Metal";
        buildingDef.PowerInputOffset = new CellOffset(2, 0);
        buildingDef.AnimFiles = new KAnimFile[] { StandaloneBuildingMod.SewingAnim };
        return buildingDef;
    }

    // Token: 0x06000156 RID: 342 RVA: 0x0000A388 File Offset: 0x00008588
    public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
    {
        go.AddOrGet<BuildingComplete>().isManuallyOperated = true;
        go.AddOrGet<DropAllWorkable>();
        Prioritizable.AddRef(go);
        ComplexFabricator complexFabricator = go.AddOrGet<ComplexFabricator>();
        ComplexFabricatorLayeredWorkable workable = go.AddOrGet<ComplexFabricatorLayeredWorkable>();
        workable.overrideAnims = new KAnimFile[]
        {
            Assets.GetAnim("anim_eat_table_kanim")
        };
        workable.foregroundLayer = Grid.SceneLayer.Background;
        //↑更改操作交互动画
        go.AddOrGet<ComplexFabricatorWorkable>();
        complexFabricator.sideScreenStyle = ComplexFabricatorSideScreen.StyleSetting.ListQueueHybrid;
        go.AddOrGet<FabricatorIngredientStatusManager>();
        go.AddOrGet<CopyBuildingSettings>();
        this.ConfigureRecipes();
        BuildingTemplates.CreateComplexFabricatorStorage(go, complexFabricator);
    }

    // Token: 0x06000157 RID: 343 RVA: 0x0000A404 File Offset: 0x00008604
    private void ConfigureRecipes()
    {
        ComplexRecipe.RecipeElement[] array = new ComplexRecipe.RecipeElement[]
        {
            new ComplexRecipe.RecipeElement(GameTags.Fabrics, (float)TUNING.EQUIPMENT.VESTS.WARM_VEST_MASS, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "", false, false)
        };
        ComplexRecipe.RecipeElement[] array2 = new ComplexRecipe.RecipeElement[]
        {
            new ComplexRecipe.RecipeElement("Warm_Vest".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, false)
        };
        WarmVestConfig.recipe = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("SewingMachine", array, array2), array, array2)
        {
            time = TUNING.EQUIPMENT.VESTS.WARM_VEST_FABTIME,
            description = STRINGS.EQUIPMENT.PREFABS.WARM_VEST.RECIPE_DESC,
            nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
            fabricators = new List<Tag>
            {
                "SewingMachine"
            },
            sortOrder = 1
        };
        ComplexRecipe.RecipeElement[] array3 = new ComplexRecipe.RecipeElement[]
        {
            new ComplexRecipe.RecipeElement(GameTags.Fabrics, (float)TUNING.EQUIPMENT.VESTS.FUNKY_VEST_MASS, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "", false, false)
        };
        ComplexRecipe.RecipeElement[] array4 = new ComplexRecipe.RecipeElement[]
        {
            new ComplexRecipe.RecipeElement("Funky_Vest".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, false)
        };
        FunkyVestConfig.recipe = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("SewingMachine", array3, array4), array3, array4)
        {
            time = TUNING.EQUIPMENT.VESTS.FUNKY_VEST_FABTIME,
            description = STRINGS.EQUIPMENT.PREFABS.FUNKY_VEST.RECIPE_DESC,
            nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
            fabricators = new List<Tag>
            {
                "SewingMachine"
            },
            sortOrder = 1
        };
        if (DlcManager.IsContentSubscribed("DLC5_ID"))
        {
            ComplexRecipe.RecipeElement[] array5 = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(new Tag[]
                {
                    SimHashes.Rubber.CreateTag()
                }, (float)TUNING.EQUIPMENT.SUITS.DRY_SUIT_MASS, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "", false, false),
                new ComplexRecipe.RecipeElement(GameTags.Fabrics, 2f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, "", false, false)
            };
            ComplexRecipe.RecipeElement[] array6 = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement("DrySuit".ToTag(), 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, false)
            };
            DrySuitConfig.recipe = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("SewingMachine", array5, array6), array5, array6)
            {
                time = TUNING.EQUIPMENT.VESTS.WARM_VEST_FABTIME,
                description = STRINGS.EQUIPMENT.PREFABS.DRYSUIT.RECIPE_DESC,
                nameDisplay = ComplexRecipe.RecipeNameDisplay.Result,
                fabricators = new List<Tag>
                {
                    "SewingMachine"
                },
                sortOrder = 1,
                requiredTech = Db.Get().TechItems.drySuit.parentTechId
            };
        }
        if (DlcManager.IsContentSubscribed("DLC5_ID"))
        {
            ComplexRecipe.RecipeElement[] array7 = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(new Tag[]
                {
                    SimHashes.Rubber.CreateTag()
                }, TUNING.EQUIPMENT.SHOES.BOOTS_RUBBER_FABRICATION_MASS, ComplexRecipe.RecipeElement.TemperatureOperation.Heated, "", false, true)
            };
            ComplexRecipe.RecipeElement[] array8 = new ComplexRecipe.RecipeElement[]
            {
                new ComplexRecipe.RecipeElement(RubberBootsConfig.ID, 1f, ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature, false)
            };
            ComplexRecipe complexRecipe = new ComplexRecipe(ComplexRecipeManager.MakeRecipeID("SewingMachine", array7, array8), array7, array8);
            complexRecipe.time = TUNING.EQUIPMENT.SHOES.BOOTS_FABRICATIONTIME;
            complexRecipe.description = string.Format(STRINGS.BUILDINGS.PREFABS.CRAFTINGTABLE.RECIPE_DESCRIPTION, ELEMENTS.RUBBER.NAME, STRINGS.EQUIPMENT.PREFABS.RUBBERBOOTS.NAME);
            complexRecipe.nameDisplay = ComplexRecipe.RecipeNameDisplay.Result;
            complexRecipe.fabricators = new List<Tag>
            {
                "SewingMachine"
            };
            complexRecipe.requiredTech = Db.Get().TechItems.rubberBoots.parentTechId;
            complexRecipe.sortOrder = 2;
        }
    }

    // Token: 0x06000158 RID: 344 RVA: 0x0000A72D File Offset: 0x0000892D
    public override void DoPostConfigureComplete(GameObject go)
    {
        go.GetComponent<KPrefabID>().prefabSpawnFn += delegate (GameObject game_object)
        {
            ComplexFabricatorWorkable component = game_object.GetComponent<ComplexFabricatorWorkable>();
            component.WorkerStatusItem = Db.Get().DuplicantStatusItems.Fabricating;
            component.AttributeConverter = Db.Get().AttributeConverters.MachinerySpeed;
            component.AttributeExperienceMultiplier = DUPLICANTSTATS.ATTRIBUTE_LEVELING.PART_DAY_EXPERIENCE;
            component.SkillExperienceSkillGroup = Db.Get().SkillGroups.Technicals.Id;
            component.SkillExperienceMultiplier = SKILLS.PART_DAY_EXPERIENCE;
        };
    }
    public const string ID = "SewingMachine";
}



namespace 新建建筑
{
    [HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
    public class GeneratedBuildings_LoadGeneratedBuildings
    {
        public static void Prefix()
        {
            Strings.Add(new string[] { "STRINGS.BUILDINGS.PREFABS.SEWINGMACHINE.NAME", "缝纫机" });
            Strings.Add(new string[] { "STRINGS.BUILDINGS.PREFABS.SEWINGMACHINE.EFFECT", "在通往时尚的道路上也是一种时尚" });
            Strings.Add(new string[] { "STRINGS.BUILDINGS.PREFABS.SEWINGMACHINE.DESC", "可以通过相应原料生产服装" });
            ModUtil.AddBuildingToPlanScreen("Furniture", "SewingMachine");
        }
    }
}

namespace 独立建筑
{
    // 注册衣柜名称、描述，并将它加入家具分类的建造菜单。
    [HarmonyPatch(typeof(GeneratedBuildings), "LoadGeneratedBuildings")]
    public class StandaloneBuilding_LoadGeneratedBuildings
    {
        public static void Prefix()
        {
            Strings.Add(new string[] { "STRINGS.BUILDINGS.PREFABS.STANDALONEBUILDING.NAME", "衣柜" });
            Strings.Add(new string[] { "STRINGS.BUILDINGS.PREFABS.STANDALONEBUILDING.EFFECT", "用于存放复制人的衣物" });
            Strings.Add(new string[] { "STRINGS.BUILDINGS.PREFABS.STANDALONEBUILDING.DESC", "只能存放衣物，容量为 600 千克。" });
            // 与上方 ID 保持一致；更换 ID 时也需要同步本地化键名。
            ModUtil.AddBuildingToPlanScreen("Furniture", StandaloneBuildingConfig.ID);
        }
    }
}
public class MyModStrings
{
    public class UI
    {
        public class BUILDINGCATEGORIES
        {
            // 变量名必须是【分类名全大写】_NAME
            public static LocString MYCUSTOMCATEGORY_NAME = "我的专属建筑";
            public static LocString MYCUSTOMCATEGORY_TOOLTIP = "这里存放着我制作的自定义建筑。";
        }
    }
}
