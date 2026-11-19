using System;
using System.Collections.Generic;
using UnityEngine;

namespace Swarm
{
    public partial class CombatPlayground
    {
        const int MaxOrdinaryFreeChests=128, MaxActiveOrdinaryFreeChests=32, ReservedChallengeChests=3;
        const int MaxFreeChestRecords=MaxOrdinaryFreeChests+ReservedChallengeChests;
        [Range(0,1)] public float EliteChestDropChance=.2f;
        class FreeChest
        {
            public Vector3 position;
            public UpgradeRarity tier;
            public bool opened,guaranteed;
            public GameObject body;
        }
        [Serializable] public class FreeChestSnapshot
        {
            public Vector3[] positions;
            public int[] tiers;
            public bool[] opened;
        }
        readonly List<FreeChest> freeChests=new List<FreeChest>();
        readonly Dictionary<UpgradeRarity,Material> freeChestMaterials=new Dictionary<UpgradeRarity,Material>();
        int ordinaryFreeChestRecords,guaranteedFreeChestRecords;
        public int FreeChestCount=>freeChests.Count;
        public bool FreeChestOpened(int index)=>index>=0 && index<freeChests.Count && freeChests[index].opened;
        public Vector3 FreeChestPosition(int index)=>freeChests[index].position+Vector3.up*.65f;
        public UpgradeRarity FreeChestTier(int index)=>freeChests[index].tier;

        // IDs are append-only for a run. Late duplicate commands can never claim
        // a replacement chest at an old index. Three challenge rewards have their
        // own reserved capacity and cannot be crowded out by ordinary elite drops.
        public int SpawnFreeRewardChest(Vector3 position,UpgradeRarity tier,bool guaranteed=false)
        {
            if (!Running || IsCoopClient || Won || !root || (int)tier<0 || (int)tier>4 || !Finite(position)) return -1;
            if (guaranteed)
            {
                if (guaranteedFreeChestRecords>=ReservedChallengeChests) return -1;
            }
            else
            {
                int active=0;
                foreach (var chest in freeChests) if (!chest.guaranteed && !chest.opened) active++;
                if (ordinaryFreeChestRecords>=MaxOrdinaryFreeChests || active>=MaxActiveOrdinaryFreeChests) return -1;
            }
            if (Physics.Raycast(position+Vector3.up*3,Vector3.down,out var floor,8,WorldMask)) position=floor.point;
            var state=new FreeChest {position=position,tier=tier,guaranteed=guaranteed};
            int index=freeChests.Count;
            freeChests.Add(state);
            if (guaranteed) guaranteedFreeChestRecords++; else ordinaryFreeChestRecords++;
            if (!freeChestMaterials.TryGetValue(tier,out var material))
            {
                material=MakeMaterial(RarityColor(tier));freeChestMaterials.Add(tier,material);
            }
            state.body=GameObject.CreatePrimitive(PrimitiveType.Cube);
            state.body.name=$"FREE {tier} reward chest {index}";
            state.body.layer=2;
            state.body.transform.SetParent(root);
            state.body.transform.position=FreeChestPosition(index);
            state.body.transform.localScale=new Vector3(1.5f,1,1.2f);
            Destroy(state.body.GetComponent<Collider>());
            state.body.GetComponent<Renderer>().sharedMaterial=material;
            return index;
        }

        static bool Finite(Vector3 value)=>!float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        void MaybeDropEliteChest(Enemy enemy)
        {
            if (!enemy.eliteVariant || enemy.elite || enemy.challengeElite || !enemy.body) return;
            float chance=Mathf.Clamp01(EliteChestDropChance);
            if (chance<=0 || chance<1 && UnityEngine.Random.value>=chance) return;
            SpawnFreeRewardChest(enemy.body.transform.position,UpgradeRarity.Rare);
        }

        int NearestFreeChest(float radius)
        {
            if (!player) return -1;
            int nearest=-1;float best=radius*radius;
            for (int i=0;i<freeChests.Count;i++)
            {
                if (freeChests[i].opened) continue;
                float distance=(player.transform.position-FreeChestPosition(i)).sqrMagnitude;
                if (distance<best) {nearest=i;best=distance;}
            }
            return nearest;
        }
        public bool TryFreeChest(int index=-1)
        {
            if (!Running || Won || ChoosingUpgrade || PartyPaused || LocalMenuOpen || Health<=0 || !player || player.IsZiplining) return false;
            if (index<0) index=NearestFreeChest(2.8f);
            if (index<0 || index>=freeChests.Count || freeChests[index].opened ||
                Vector3.Distance(player.transform.position,FreeChestPosition(index))>2.8f) return false;
            if (IsCoopClient) {SendCombatCommand("free-chest",index);return true;}
            var chest=freeChests[index];
            chest.opened=true;
            if (chest.body) {chest.body.SetActive(false);Destroy(chest.body);chest.body=null;}
            if (CoopAuthority) GivePartyDownloadReward(chest.tier); else QueueBonusUpgrade((int)chest.tier);
            Notice=$"FREE {chest.tier} chest opened. Everyone receives an upgrade choice.";
            return true;
        }

        public void ResetFreeChests()
        {
            foreach (var chest in freeChests)
                if (chest.body) {chest.body.SetActive(false);Destroy(chest.body);}
            freeChests.Clear();ordinaryFreeChestRecords=guaranteedFreeChestRecords=0;
            foreach (var material in freeChestMaterials.Values) if (material) Destroy(material);
            freeChestMaterials.Clear();
        }

        public FreeChestSnapshot BuildFreeChestSnapshot()
        {
            int count=freeChests.Count;
            var packet=new FreeChestSnapshot {positions=new Vector3[count],tiers=new int[count],opened=new bool[count]};
            for (int i=0;i<count;i++)
            {packet.positions[i]=freeChests[i].position;packet.tiers[i]=(int)freeChests[i].tier;packet.opened[i]=freeChests[i].opened;}
            return packet;
        }
        public void AcceptFreeChestSnapshot(FreeChestSnapshot packet)
        {
            if (!IsCoopClient || packet==null || packet.positions==null || packet.tiers==null || packet.opened==null ||
                packet.positions.Length>MaxFreeChestRecords || packet.positions.Length!=packet.tiers.Length || packet.positions.Length!=packet.opened.Length) return;
            for (int i=0;i<packet.positions.Length;i++)
                if (!Finite(packet.positions[i]) || packet.tiers[i]<0 || packet.tiers[i]>4) return;
            // Client bodies already come through visual replication. This list is
            // interaction/UI state only; do not create duplicate local cubes.
            freeChests.Clear();
            for (int i=0;i<packet.positions.Length;i++)
                freeChests.Add(new FreeChest {position=packet.positions[i],tier=(UpgradeRarity)packet.tiers[i],opened=packet.opened[i]});
        }

        public bool ShowFreeChestObjective=>Running && NearestFreeChest(5)>=0;
        public void DrawFreeChestObjective(float width)
        {
            int index=NearestFreeChest(5);if (index<0) return;
            var chest=freeChests[index];var rect=new Rect(width-330,22,308,143);Color color=RarityColor(chest.tier);
            HudPanelBox(rect,color);
            HudLabel(new Rect(rect.x+16,35,276,26),$"FREE {chest.tier.ToString().ToUpperInvariant()} CHEST",hudSmall,color);
            HudLabel(new Rect(rect.x+16,70,276,54),"No money needed. An upgrade choice for every teammate.",hudText);
            HudLabel(new Rect(rect.x+16,130,276,24),Vector3.Distance(player.transform.position,FreeChestPosition(index))>2.8f ? "Move closer" : "E: Open for the whole party",hudSmall,color);
        }
    }
}
