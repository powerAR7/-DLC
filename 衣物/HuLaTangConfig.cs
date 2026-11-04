using System.Collections.Generic;
using HarmonyLib;
using Klei.AI;
using UnityEngine;

public abstract class DlcRestrictedEntityConfig : IEntityConfig, IHasDlcRestrictions
{
    public abstract GameObject CreatePrefab();

    public abstract void OnPrefabInit(GameObject inst);

    public abstract void OnSpawn(GameObject inst);

    public abstract string[] GetRequiredDlcIds();

    public virtual string[] GetForbiddenDlcIds()
    {
        return new string[0];
    }

    string[] IEntityConfig.GetDlcIds()
    {
        return GetRequiredDlcIds();
    }

    public string[] GetAnyRequiredDlcIds()
    {
        return null;
    }
}

public class HuLaTangConfig : MyBaseFoodConfig
{
    public const string ID = "HuLaTang";

    private static readonly string[] BuffIds = { HuLaTangAthleticsBuff.ID };

    protected override string FoodId
    {
        get { return ID; }
    }

    protected override string FoodName
    {
        get { return "胡辣汤"; }
    }

    protected override string FoodDescription
    {
        get { return "一碗热腾腾的胡辣汤。"; }
    }

    protected override KAnimFile FoodAnimation
    {
        get { return MyMod.Clothing.ClothingMod.HuLaTangAnim; }
    }

    protected override IReadOnlyList<string> FoodBuffIds
    {
        get { return BuffIds; }
    }

    protected override void LoadFoodAssets()
    {
        MyMod.Clothing.ClothingMod.LoadHuLaTangAnimation();
    }

    public override GameObject CreatePrefab()
    {
        GameObject go = base.CreatePrefab();
        go.AddOrGet<BrewingSystem.BrewingComponent>();
        return go;
    }

    public override void OnPrefabInit(GameObject inst)
    {
    }

    public override void OnSpawn(GameObject inst)
    {
    }

    public override string[] GetRequiredDlcIds()
    {
        return DlcManager.DLC5;
    }

    public override string[] GetForbiddenDlcIds()
    {
        return new string[0];
    }
}

internal static class HuLaTangAthleticsBuff
{
    internal const string ID = "HuLaTangAthletics";

    internal static void Register()
    {
        if (Db.Get().effects.TryGet(ID) != null)
            return;

        Db.Get().effects.Add(new Effect(
            ID,
            "胡辣汤提神",
            "运动能力 +2",
            300f,
            true,
            false,
            false)
        {
            SelfModifiers = new List<AttributeModifier>
            {
                new AttributeModifier(
                    Db.Get().Attributes.Athletics.Id,
                    2f,
                    "胡辣汤提神")
            }
        });
    }
}

[HarmonyPatch(typeof(Db), nameof(Db.Initialize))]
public static class HuLaTangAthleticsBuffRegistrationPatch
{
    [HarmonyPostfix]
    public static void Postfix(Db __instance)
    {
        HuLaTangAthleticsBuff.Register();
    }
}
