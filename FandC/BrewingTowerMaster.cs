using System.Collections.Generic;
using UnityEngine;

namespace FandC
{
    // 主控层：监听附着网络，向上层输送水，并将回收的乙醇硬排放到所在格。
    // 主控层沿硬点网络收集其上所有发酵层，不设置层数上限。
    public class BrewingTowerMaster : KMonoBehaviour, ISim1000ms
    {
        private const float ETHANOL_TRANSFER_RATE = 50f;

        public float waterPerSecond = 5f;

        private AttachableBuilding attachable;
        private Storage masterStorage;
        private List<GameObject> above = new List<GameObject>();   // 不能 readonly，ref 传参
        private readonly List<Storage> aboveStorages = new List<Storage>();

        protected override void OnSpawn()
        {
            base.OnSpawn();
            attachable = GetComponent<AttachableBuilding>();
            masterStorage = GetComponent<Storage>();
            if (attachable != null) attachable.onAttachmentNetworkChanged += OnNetworkChanged;
            RebuildAbove();
        }

        protected override void OnCleanUp()
        {
            if (attachable != null) attachable.onAttachmentNetworkChanged -= OnNetworkChanged;
            base.OnCleanUp();
        }

        private void OnNetworkChanged(object who)
        {
            RebuildAbove();
        }

        private void RebuildAbove()
        {
            above.Clear();
            aboveStorages.Clear();
            if (attachable == null) return;
            AttachableBuilding.GetAttachedAbove(attachable, ref above);
            for (int i = 0; i < above.Count; i++)
            {
                Storage storage = above[i].GetComponent<Storage>();
                if (storage != null) aboveStorages.Add(storage);
            }
        }

        public void Sim1000ms(float dt)
        {
            if (masterStorage == null || dt <= 0f) return;

            int cell = Grid.PosToCell(gameObject);
            if (!Grid.IsValidCell(cell)) return;

            for (int i = 0; i < aboveStorages.Count; i++)
            {
                Storage layer = aboveStorages[i];
                masterStorage.Transfer(layer, GameTags.Water, waterPerSecond * dt, false, false);
                SpawnStoredEthanol(layer, cell, ETHANOL_TRANSFER_RATE * dt);
            }

            SpawnStoredEthanol(masterStorage, cell, float.MaxValue);
        }

        private static void SpawnStoredEthanol(Storage source, int cell, float maxMass)
        {
            Tag ethanolTag = SimHashes.Ethanol.CreateTag();
            GameObject ethanol = source.FindFirst(ethanolTag);
            PrimaryElement primaryElement = ethanol != null
                ? ethanol.GetComponent<PrimaryElement>()
                : null;
            if (primaryElement == null || primaryElement.Mass <= 0f) return;

            float mass = Mathf.Min(primaryElement.Mass, maxMass);
            float temperature = primaryElement.Temperature;
            source.ConsumeIgnoringDisease(ethanolTag, mass);
            SimMessages.AddRemoveSubstance(
                cell,
                SimHashes.Ethanol,
                default(CellAddRemoveSubstanceEvent),
                mass,
                temperature,
                byte.MaxValue,
                0);
        }
    }
}
