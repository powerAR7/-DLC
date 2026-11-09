using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FandC
{
    [HarmonyPatch(typeof(ProgressBarSideScreen), "RefreshBar")]
    public static class ProgressBarColorPatch
    {
        public static void Postfix(ProgressBarSideScreen __instance)
        {
            if (__instance.targetObject == null ||
                __instance.progressBar == null ||
                __instance.progressBar.fill == null)
            {
                return;
            }

            BrewingBarrel barrel = __instance.targetObject as BrewingBarrel;
            if (barrel == null)
            {
                return;
            }

            Color targetColor = barrel.GetCurrentProgressColor();
            if (__instance.progressBar.fill.color != targetColor)
            {
                __instance.progressBar.fill.color = targetColor;
            }
        }
    }
}
