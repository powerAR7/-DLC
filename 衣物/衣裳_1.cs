using System;
using System.Collections.Generic;
using Database;
using Klei.AI;
using STRINGS;
using TUNING;
using UnityEngine;

namespace MyMod.Clothing{
    public class X001GG1 : IEquipmentConfig
    {
        // ===== 常量 / 字段（ClothingMod 通过反射读取 Id / DisplayName / GenericName / Description）=====
        public const string Id = "X001GG1";                   // 定义并赋值服装ID（注意小写 d，与 ClothingMod 反射一致）
        public const string DisplayName = "服装1";         // 定义并赋值服装显示名称
        public const string GenericName = "服装2";         // 定义并赋值服装通用名称
        public const string RecipeDescription = "服装3";   // 定义并赋值配方描述
        public static string Description = "服装4";        // 定义并赋值服装描述（制造配方界面使用的描述）

        public static ComplexRecipe Recipe;

        // ===== 服装属性 =====（制定装备服装后的效果）
        public static int DecorModifier = 10;                 // 装饰度效果
        public static float ConductivityModifier = ClothingWearer.ClothingInfo.BASIC_CLOTHING.conductivityMod;
        //↑定义服装的导热相关属性（此处未使用自定义效果，而是“定位→读取→套用”原版服装的效果）
        public static float HomeostasisEfficiencyModifier = ClothingWearer.ClothingInfo.COOL_CLOTHING.homeostasisEfficiencyMultiplier;
        //↑定义服装的体温调节效率属性（未使用自定义效果，而是“定位→读取→套用”原版服装的效果）

        //↓将服装属性打包（id、装饰度、隔热、导热）
        public static readonly ClothingWearer.ClothingInfo NEW_CLOTHING = new ClothingWearer.ClothingInfo
            (
                "X001GG1",                                          //服装ID
                X001GG1.DecorModifier,                              //服装装饰度效果
                X001GG1.ConductivityModifier,                       //服装隔热效果
                X001GG1.HomeostasisEfficiencyModifier
            );             //服装导热效果

        // ===== 定义（赋值）配方 =====
        public static readonly ComplexRecipe.RecipeElement[] ingredients = new ComplexRecipe.RecipeElement[]
        {
            new ComplexRecipe.RecipeElement("BasicFabric".ToTag(), 3f)
        };
        //定义并赋值输入词段：消耗"BasicFabric"，3kg

        public static readonly ComplexRecipe.RecipeElement[] results = new ComplexRecipe.RecipeElement[]
        {
            new ComplexRecipe.RecipeElement("X001GG1".ToTag(), 1f)
        };
        //定义并赋值输输出词段：产生"X001GG1"，1单位

        // ===== 配方实现方法 =====
        public string[] GetDlcIds()
        {
            return null;
        }

        public static void ConfigureRecipe()//创建配方的函数（通过什么建筑来消耗什么，然后输出什么）
        {
            ComplexRecipe complexRecipe = new ComplexRecipe(
                ComplexRecipeManager.MakeRecipeID("ClothingFabricator", X001GG1.ingredients, X001GG1.results),
                //↑创建一个配方ID（什么建筑，输入参数，输出参数）
                X001GG1.ingredients,
                X001GG1.results);
            //↑实际创建配方对象
            complexRecipe.time = TUNING.EQUIPMENT.VESTS.FUNKY_VEST_FABTIME;//设置服装制造的消耗时间
            complexRecipe.description = "服装示意图作用";
            //↑服装描述（制造配方界面使用的描述）                                            ？又一次？
            complexRecipe.nameDisplay = ComplexRecipe.RecipeNameDisplay.Result;
            //↑服装显示名称                                                                   ？又一次？
            complexRecipe.fabricators = new List<Tag> { "ClothingFabricator" };
            //↑该配方归属于哪个制造器械"ClothingFabricator"（缝纫机）
            //↑进一步理解为：该配方允许在哪个建筑中出现
            complexRecipe.sortOrder = 1;
            X001GG1.Recipe = complexRecipe;
        }

        public EquipmentDef CreateEquipmentDef()
        {
            // 使用自定义属性 NEW_CLOTHING，而不是原版的 CUSTOM_CLOTHING
            ClothingWearer.ClothingInfo clothingInfo = X001GG1.NEW_CLOTHING;
            List<AttributeModifier> attributeModifiers = new List<AttributeModifier>();

            EquipmentDef equipmentDef = EquipmentTemplates.CreateEquipmentDef(
                "X001GG1",
                TUNING.EQUIPMENT.CLOTHING.SLOT,
                SimHashes.Carbon,
                (float)TUNING.EQUIPMENT.VESTS.FUNKY_VEST_MASS,
                "X001GG1X1_kanim",              // 服装示意图(服装本体/示意相关动画)
                TUNING.EQUIPMENT.VESTS.SNAPON0,
                "X001GG1X2_kanim",              // 服装装板图(装板/穿戴显示相关动画)
                4,
                attributeModifiers,
                TUNING.EQUIPMENT.VESTS.SNAPON1,
                true,
                EntityTemplates.CollisionShape.RECTANGLE,
                0.75f,
                0.4f,
                new Tag[] { GameTags.Clothes, GameTags.PedestalDisplayable },//物品标签（服装，可展示物品）
                null);
            //↑创建服装定义（ID，装备槽，元素，质量，世界动画，，穿着动画......）


            Descriptor item = new Descriptor(
                string.Format("{0}: {1}", DUPLICANTS.ATTRIBUTES.THERMALCONDUCTIVITYBARRIER.NAME, GameUtil.GetFormattedDistance(clothingInfo.conductivityMod)),
                string.Format("{0}: {1}", DUPLICANTS.ATTRIBUTES.THERMALCONDUCTIVITYBARRIER.NAME, GameUtil.GetFormattedDistance(clothingInfo.conductivityMod)),
                Descriptor.DescriptorType.Effect, false);
            //↑信息面板属性说明
            Descriptor item2 = new Descriptor(
                string.Format("{0}: {1}", DUPLICANTS.ATTRIBUTES.DECOR.NAME, clothingInfo.decorMod),
                string.Format("{0}: {1}", DUPLICANTS.ATTRIBUTES.DECOR.NAME, clothingInfo.decorMod),
                Descriptor.DescriptorType.Effect, false);
            //↑信息面板属性说明2
            equipmentDef.additionalDescriptors.Add(item);
            equipmentDef.additionalDescriptors.Add(item2);
            //↑↑最终效果：给服装的信息面板增加两个属性说明（显示属性说明）

            // 穿上/脱下回调，让属性真正生效
            equipmentDef.OnEquipCallBack = delegate (Equippable eq){
                ClothingWearer.ClothingInfo.OnEquipVest(eq, clothingInfo);};
            //↑穿上服装→属性生效
            equipmentDef.OnUnequipCallBack = new Action<Equippable>(ClothingWearer.ClothingInfo.OnUnequipVest);
            //↑脱下服装→属性撤销

            equipmentDef.RecipeDescription = X001GG1.RecipeDescription;

            foreach (EquippableFacadeResource equippableFacadeResource in Db.GetEquippableFacades().resources){
                if (equippableFacadeResource.DefID == "X001GG1"){
                    TagManager.Create(
                        equippableFacadeResource.Id,
                        EquippableFacade.GetNameOverride("X001GG1", equippableFacadeResource.Id));}
            }

            return equipmentDef;
        }

        public static void SetupVest(GameObject go)
        { 
            Equippable equippable = go.GetComponent<Equippable>();
            //↑从这个游戏对象上找 Equippable 组件
            if (equippable == null){
                equippable = go.AddComponent<Equippable>();}
            //↑如果游戏对象上没有 Equippable 组件就添加一个

            equippable.SetQuality(global::QualityLevel.Good);
            //↑设置服装品质为 Good   
            go.GetComponent<KBatchedAnimController>().sceneLayer = Grid.SceneLayer.BuildingBack;
            //↑动画渲染层（不懂） 
        }

        public void DoPostConfigure(GameObject go)
        {
            X001GG1.SetupVest(go);
        }
    }
}
        //↑将函数接入游戏
