using KSerialization;
using UnityEngine;

namespace BrewingSystem
{
    /// <summary>
    /// 环境检测酿造组件。
    /// - 每 1000ms（ISim1000ms 由 SimAndRenderScheduler 自动调度）检测一次所在网格。
    /// - 泡在液体里进度上涨，离开液体后进度缓慢回退。
    /// - progress 通过 [Serialize] 写入存档，存档/读档不会丢进度。
    /// </summary>
    public class BrewingComponent : KMonoBehaviour, ISim1000ms
    {
        // 只有标了 [Serialize] 的成员会进入存档。
        // KMonoBehaviour 自身声明了 [SerializationConfig(MemberSerialization.OptIn)]，
        // 所以 private 字段只要打标就能存，public 字段不打标就不会被存。
        [Serialize]
        private float progress;

        [Serialize]
        private bool lastSubmerged;

        [Serialize]
        private float consumedWaterMass = 0f;

        [Serialize]
        private float timeSinceLastProgressPopup;

        private bool completed;

        // 可调参数：没有 [Serialize]，读档后保持这里的默认值。
        public float progressPerSecond = 1f;
        public float targetProgress = 200f;

        public float Progress
        {
            get { return progress; }
        }

        public bool IsSubmerged
        {
            get { return lastSubmerged; }
        }

        public float ProgressRatio
        {
            get { return targetProgress <= 0f ? 1f : Mathf.Clamp01(progress / targetProgress); }
        }

        public void Sim1000ms(float dt)
        {
            // dt 是距上次 tick 的模拟时间（秒），正常情况下约为 1。
            if (dt <= 0f)
            {
                dt = 1f;
            }

            int cell = Grid.PosToCell(gameObject);

            // PosToCell 不做越界检查，物体在网格外（未落位、被塞进奇怪容器）时会返回非法格。
            bool inLiquid = Grid.IsValidCell(cell) && Grid.IsSubstantialLiquid(cell);
            lastSubmerged = inLiquid;

            bool fermenting = false;
            if (inLiquid)
            {
                float cellMass = Grid.Mass[cell];
                float waterToConsume = 0.5f * dt;
                float availableMass = cellMass - 0.5f;
                if (availableMass >= waterToConsume)
                {
                    SimMessages.AddRemoveSubstance(
                        cell,
                        Grid.Element[cell].id,
                        default(CellAddRemoveSubstanceEvent),
                        -waterToConsume,
                        Grid.Temperature[cell],
                        byte.MaxValue,
                        0);
                    consumedWaterMass += waterToConsume;
                    progress += progressPerSecond * dt;
                    fermenting = true;
                }
            }

            progress = Mathf.Clamp(progress, 0f, targetProgress);

            if (fermenting)
            {
                timeSinceLastProgressPopup += dt;
                if (timeSinceLastProgressPopup >= 20f && progress < targetProgress)
                {
                    float percentage = targetProgress <= 0f ? 100f : progress / targetProgress * 100f;
                    PopFXManager.Instance.SpawnFX(
                        PopFXManager.Instance.sprite_Resource,
                        string.Format("发酵中... ({0:0}%)", percentage),
                        transform,
                        new Vector3(0f, 0.8f, 0f),
                        1.5f,
                        true);
                    timeSinceLastProgressPopup = 0f;
                }
            }

            if (progress >= targetProgress && !completed)
            {
                int productCell = Grid.PosToCell(gameObject);
                if (!Grid.IsValidCell(productCell))
                {
                    return;
                }

                completed = true;
                PopFXManager.Instance.SpawnFX(
                    PopFXManager.Instance.sprite_Resource,
                    "发酵完成！",
                    transform,
                    new Vector3(0f, 0.8f, 0f),
                    1.5f,
                    true);
                Game.Instance.SpawnFX(SpawnFXHashes.OxygenEmissionBubbles, productCell, 0f);

                PrimaryElement primaryElement = GetComponent<PrimaryElement>();
                float productMass = (primaryElement != null ? primaryElement.Mass : 1f) + consumedWaterMass;
                float productTemperature = primaryElement != null
                    ? primaryElement.Temperature
                    : Grid.Temperature[productCell];

                SimMessages.AddRemoveSubstance(
                    productCell,
                    SimHashes.Ethanol,
                    default(CellAddRemoveSubstanceEvent),
                    productMass,
                    productTemperature,
                    byte.MaxValue,
                    0);
                UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
