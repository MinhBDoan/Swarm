using UnityEngine;
namespace Swarm
{
    public partial class CombatPlayground
    {
        public static readonly Vector3[] MoneyCrateFloors={new Vector3(-22,5.5f,26),new Vector3(-15,-2,322),new Vector3(23,7,38),CanyonLayout.StationPoint(36,13.5f,39)};
        public static readonly Vector3[] SalvageFloors={new Vector3(-10,0,-9),new Vector3(16,0,17),new Vector3(-25,5.5f,29),new Vector3(-30,-2,330),CanyonLayout.StationPoint(21,12,40),CanyonLayout.StationPoint(39,13.5f,45)};
        public int SharedMoney {get;private set;}
        public int CratesPurchased {get;private set;}
        readonly bool[] moneyCratesOpened=new bool[4],salvageBroken=new bool[6];
        readonly GameObject[] supplyBodies=new GameObject[10];
        Material rareSupplyMaterial,epicSupplyMaterial,salvageSupplyMaterial;
        public bool MoneyCrateOpened(int index)=>moneyCratesOpened[index];
        public bool SalvageBroken(int index)=>salvageBroken[index];
        public static int MoneyCrateCost(int index)=>index<2 ? 30 : 75;
        public static UpgradeRarity MoneyCrateTier(int index)=>index<2 ? UpgradeRarity.Rare : UpgradeRarity.Epic;
        public static Vector3 SupplyPosition(int index)=>(index<4 ? MoneyCrateFloors[index] : SalvageFloors[index-4])+Vector3.up*.65f;
        void ResetEconomy()
        {
            SharedMoney=CratesPurchased=0;
            for(int i=0;i<4;i++) moneyCratesOpened[i]=false;
            for(int i=0;i<6;i++) salvageBroken[i]=false;
            for(int i=0;i<10;i++) supplyBodies[i]=null;
        }
        void CreateSupplies()
        {
            if(!rareSupplyMaterial)
            {
                rareSupplyMaterial=MakeMaterial(RarityColor(UpgradeRarity.Rare)); epicSupplyMaterial=MakeMaterial(RarityColor(UpgradeRarity.Epic)); salvageSupplyMaterial=MakeMaterial(new Color(.9f,.7f,.25f));
            }
            for(int i=0;i<10;i++)
            {
                var body=GameObject.CreatePrimitive(i<4 ? PrimitiveType.Cube : PrimitiveType.Cylinder); body.name=i<4 ? MoneyCrateTier(i)+" paid supply crate" : "Breakable salvage container";
                body.layer=2;body.transform.SetParent(root);body.transform.position=SupplyPosition(i);
                body.transform.localScale=i<4 ? new Vector3(1.6f,1,1.3f) : new Vector3(.9f,.6f,.9f);
                Destroy(body.GetComponent<Collider>());body.GetComponent<Renderer>().sharedMaterial=i<2 ? rareSupplyMaterial : i<4 ? epicSupplyMaterial : salvageSupplyMaterial;
                supplyBodies[i]=body;
            }
        }
        bool SupplyAvailable(int index)=>index<4 ? !moneyCratesOpened[index] : !salvageBroken[index-4];
        int NearestSupply(float radius)
        {
            if(!player)return -1;
            int nearest=-1;float best=radius*radius;
            for(int i=0;i<10;i++)
            {
                if(!SupplyAvailable(i))continue;float distance=(player.transform.position-SupplyPosition(i)).sqrMagnitude;
                if(distance<best){best=distance;nearest=i;}
            }
            return nearest;
        }
        public bool TrySupplyInteraction(int index=-1)
        {
            if(!Running || ChoosingUpgrade || Health<=0 || Won || player.IsZiplining)return false;
            if(index<0)index=NearestSupply(2.8f);
            if(index<0 || index>=10 || !SupplyAvailable(index) || Vector3.Distance(player.transform.position,SupplyPosition(index))>2.8f)return false;
            if(IsCoopClient){SendCombatCommand("supply",index);return true;}
            if(index>=4)
            {
                salvageBroken[index-4]=true;SharedMoney+=10;
                Spark(SupplyPosition(index),.3f,1.3f,.3f,blastMaterial);
                if(supplyBodies[index]){supplyBodies[index].SetActive(false);Destroy(supplyBodies[index]);}
                Notice="Salvage broken: +10 party money.";return true;
            }
            int cost=MoneyCrateCost(index);
            if(SharedMoney<cost){Notice=$"Need {cost-SharedMoney} more party money for this crate.";return true;}
            moneyCratesOpened[index]=true;SharedMoney-=cost;CratesPurchased++;
            if(supplyBodies[index])
            {
                supplyBodies[index].transform.localScale=new Vector3(1.6f,.35f,1.3f);
                var tint=new MaterialPropertyBlock();tint.SetColor("_Color",Color.gray);supplyBodies[index].GetComponent<Renderer>().SetPropertyBlock(tint);
            }
            var tier=MoneyCrateTier(index);
            if(CoopAuthority)GivePartyDownloadReward(tier);else QueueBonusUpgrade((int)tier);
            Notice=tier+" crate purchased. Everyone receives an upgrade choice.";return true;
        }
        bool ShowSupplyObjective=>Running && NearestSupply(5)>=0;
        void DrawSupplyObjective(float width)
        {
            int index=NearestSupply(5);if(index<0)return;
            var rect=new Rect(width-330,22,308,143);Color color=index<4 ? RarityColor(MoneyCrateTier(index)) : new Color(1,.8f,.3f);
            HudPanelBox(rect,color);HudLabel(new Rect(rect.x+16,35,276,26),index<4 ? MoneyCrateTier(index).ToString().ToUpperInvariant()+" SUPPLY CRATE" : "BREAKABLE SALVAGE",hudSmall,color);
            string text=index<4 ? $"{MoneyCrateCost(index)} party money / matching-tier choice for everyone" : "Break this container for +10 party money.";
            HudLabel(new Rect(rect.x+16,70,276,54),text,hudText);
            HudLabel(new Rect(rect.x+16,130,276,24),Vector3.Distance(player.transform.position,SupplyPosition(index))>2.8f ? "Move closer" : index>=4 ? "E: Break container" : SharedMoney>=MoneyCrateCost(index) ? "E: Buy for the party" : $"Need {MoneyCrateCost(index)-SharedMoney} more money",hudSmall,color);
        }
        void DrawSupplyLabels()
        {
            if(!Running || !Camera.main || !player)return;
            int index=NearestSupply(14);if(index<0)return;Vector3 anchor=SupplyPosition(index)+Vector3.up;
            Vector3 point=Camera.main.WorldToScreenPoint(anchor);var rect=new Rect(point.x-140,Screen.height-point.y,280,30);
            if(point.z<=0 || WorldLabelBlocked(rect) || Physics.Linecast(Camera.main.transform.position,anchor,WorldMask))return;
            GUI.Label(rect,index<4 ? $"{MoneyCrateTier(index)} CRATE / {MoneyCrateCost(index)} money" : "SALVAGE / E: break for 10 money");
        }
        void DestroySupplyMaterials(){Destroy(rareSupplyMaterial);Destroy(epicSupplyMaterial);Destroy(salvageSupplyMaterial);}
    }
}
