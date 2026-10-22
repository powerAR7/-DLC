using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// 保存工作前的 Z 坐标
internal static class WorkerLayerState
{
    internal static readonly Dictionary<int, float> OriginalZ = new Dictionary<int, float>();

    internal static bool IsTarget(SimpleLayeredAnimWork.Instance smi)
    {
        GameObject provider = smi.WorkProvider;
        if (provider == null)
            return false;

        KPrefabID prefabID = provider.GetComponent<KPrefabID>();
        // 衣柜是储物建筑，没有工作动画；此补丁只处理缝纫机。
        return prefabID != null && prefabID.PrefabTag == "SewingMachine".ToTag();
    }
}

// 工作开始：把人物主体移到建筑后层
[HarmonyPatch(typeof(SimpleLayeredAnimWork.Instance),nameof(SimpleLayeredAnimWork.Instance.SetForegroundLayer))]
internal static class WorkerBehindBuildingStartPatch
{
    private static void Prefix(SimpleLayeredAnimWork.Instance __instance,KBatchedAnimController ___animController)
    {
        if (!WorkerLayerState.IsTarget(__instance))
            return;

        Transform t = ___animController.transform;
        int key = t.GetInstanceID();

        if (!WorkerLayerState.OriginalZ.ContainsKey(key))
            WorkerLayerState.OriginalZ[key] = t.position.z;

        Vector3 pos = t.position;
        pos.z = Grid.GetLayerZ(Grid.SceneLayer.BuildingBack);
        t.position = pos;
    }
}

// 工作结束：恢复人物原来的 Z 坐标
[HarmonyPatch(typeof(SimpleLayeredAnimWork.Instance),nameof(SimpleLayeredAnimWork.Instance.ClearForegroundLayer))]
internal static class WorkerBehindBuildingEndPatch
{
    private static void Prefix( KBatchedAnimController ___animController)
    {
        if (___animController == null)
            return;

        Transform t = ___animController.transform;
        int key = t.GetInstanceID();

        if (!WorkerLayerState.OriginalZ.TryGetValue(key, out float originalZ))
            return;

        Vector3 pos = t.position;
        pos.z = originalZ;
        t.position = pos;

        WorkerLayerState.OriginalZ.Remove(key);
    }
}