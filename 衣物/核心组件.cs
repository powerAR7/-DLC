using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using PeterHan.PLib.Core;
using UnityEngine;

namespace MyMod.Clothing
{
    internal static class ClothingMod
    {
        private const string HuLaTangAnimationDirectory =
            @"C:\Users\m1567\Documents\Klei\OxygenNotIncluded\mods\Dev\缝纫机\anim\hulatang";

        public static string Namespace { get; private set; }
        internal static KAnimFile X001GG1X1Anim { get; private set; }
        internal static KAnimFile X001GG1X2Anim { get; private set; }
        internal static KAnimFile HuLaTangAnim { get; private set; }
        private static bool x001GG1Registered;

        public static void Initialize()
        {
            Namespace = typeof(ClothingMod).Namespace;
            PUtil.InitLibrary(true);
        }

        internal static void LoadAnimations()
        {
            string animationDirectory = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(typeof(ClothingMod).Assembly.Location),
                "anim"));
            if (X001GG1X1Anim == null)
                X001GG1X1Anim = LoadAnimation("X001GG1X1_kanim", animationDirectory);
            if (X001GG1X2Anim == null)
                X001GG1X2Anim = LoadAnimation("X001GG1X2_kanim", animationDirectory);
        }

        internal static void LoadHuLaTangAnimation()
        {
            if (HuLaTangAnim == null)
                HuLaTangAnim = LoadAnimation("hulatang", HuLaTangAnimationDirectory);
        }

        private static KAnimFile LoadAnimation(string animationName, string animationDirectory)
        {
            string texturePath = Path.Combine(animationDirectory, animationName + "_0.png");
            string animationPath = Path.Combine(animationDirectory, animationName + "_anim.bytes");
            string buildPath = Path.Combine(animationDirectory, animationName + "_build.bytes");
            foreach (string assetPath in new[] { texturePath, animationPath, buildPath })
            {
                if (!File.Exists(assetPath))
                    throw new FileNotFoundException("Missing clothing animation asset.", assetPath);
            }

            KAnimFile.Mod animationMod = new KAnimFile.Mod
            {
                anim = File.ReadAllBytes(animationPath),
                build = File.ReadAllBytes(buildPath)
            };
            byte[] textureBytes = File.ReadAllBytes(texturePath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Type imageConversion = Type.GetType(
                "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule",
                true);
            MethodInfo loadImage = imageConversion.GetMethod(
                "LoadImage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                null);
            if (loadImage == null)
                throw new MissingMethodException(imageConversion.FullName, "LoadImage");
            if (!(bool)loadImage.Invoke(null, new object[] { texture, textureBytes, false }))
                throw new InvalidDataException("Failed to load clothing animation texture: " + texturePath);

            animationMod.textures.Add(texture);
            if (!animationMod.IsValid())
                throw new InvalidDataException("The clothing animation assets are incomplete in " + animationDirectory);

            KAnimFile animation = ModUtil.AddKAnimMod(animationName, animationMod);
            if (animation == null)
                throw new InvalidDataException("Failed to register clothing animation: " + animationName);
            return animation;
        }

        [HarmonyPatch(typeof(GeneratedEquipment))]
        [HarmonyPatch("LoadGeneratedEquipment")]
        public class 添加服装ID_1
        {
            public static void Prefix()
            {
                // 先注册本地化字符串
                ClothingMod.RegisterTypeStrings(typeof(X001GG1));
            }

            public static void Postfix()
            {
                if (ClothingMod.x001GG1Registered)
                    return;

                ClothingMod.LoadAnimations();
                EquipmentConfigManager.Instance.RegisterEquipment(new X001GG1());
                ClothingMod.x001GG1Registered = true;
            }
        }

        [HarmonyPatch(typeof(SewingMachineConfig))]
        [HarmonyPatch("ConfigureRecipes")]
        public class 添加服装ID_2
        {
            public static void Postfix()
            {
                X001GG1.ConfigureRecipe();
            }
        }

        internal static void RegisterTypeStrings(Type type)
        {
            // 注意：字段名必须与 X001GG1 里的字段名完全一致（Id 是小写 d）
            string id = (string)type.GetField("Id").GetValue(null);
            string displayName = (string)type.GetField("DisplayName").GetValue(null);
            string genericName = (string)type.GetField("GenericName").GetValue(null);
            string description = (string)type.GetField("Description").GetValue(null);
            string recipeDescription = (string)type.GetField("RecipeDescription")?.GetValue(null);

            string key = "STRINGS.EQUIPMENT.PREFABS." + id.ToUpperInvariant();

            Strings.Add(new string[] { key + ".NAME", displayName });
            Strings.Add(new string[] { key + ".GENERICNAME", genericName });
            Strings.Add(new string[] { key + ".DESC", description });
            if (!string.IsNullOrEmpty(recipeDescription))
            {
                Strings.Add(new string[] { key + ".RECIPE_DESC", recipeDescription });
            }
        }

        [HarmonyPatch(typeof(KAnimGroupFile), nameof(KAnimGroupFile.LoadGroupResourceFile))]
        internal static class ClothingAnimationRegistrationPatch
        {
            private static void Postfix()
            {
                ClothingMod.LoadAnimations();
                ClothingMod.LoadHuLaTangAnimation();
            }
        }
    }
}
