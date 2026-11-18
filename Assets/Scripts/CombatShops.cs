using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    public partial class CombatPlayground
    {
        public static readonly Vector3[] ShopLocations={new Vector3(12,0,17),new Vector3(-147,-4,410),new Vector3(25,8,650)};
        public static readonly UpgradeRarity[] ShopTiers={UpgradeRarity.Uncommon,UpgradeRarity.Rare,UpgradeRarity.Epic};
        public static int ShopCost(int shop) => ShopTiers[shop]==UpgradeRarity.Uncommon ? 15 : ShopTiers[shop]==UpgradeRarity.Rare ? 30 : 75;
        public const float ShopInteractRange=3.5f;

        [Serializable] public class ShopVisitState
        {
            public bool rolled,purchased;
            public int revision;
            public List<BoltUpgrade> offers=new List<BoltUpgrade>();
        }
        [Serializable] public class ShopPlayerState
        {
            public int opened=-1,response;
            public string message="";
            public ShopVisitState[] visits={new ShopVisitState(),new ShopVisitState(),new ShopVisitState()};
        }
        [Serializable] class ShopCommand
        {
            public string action;
            public int run,request,shop,offer,revision;
        }
        readonly Dictionary<ulong,ShopPlayerState> shopPlayers=new Dictionary<ulong,ShopPlayerState>();
        readonly Material[] shopMaterials=new Material[3];
        ShopPlayerState remoteShopState=new ShopPlayerState();
        int browsingShop=-1,shopRequest,shopRevision;
        bool shopBuyPending;
        public bool ShopOpen => browsingShop>=0;
        public int BrowsingShop => browsingShop;
        public ShopPlayerState LocalShopState => IsCoopClient ? remoteShopState : ShopStateFor(LocalShopId);
        ulong LocalShopId => NetworkActive ? NetworkManager.Singleton.LocalClientId : 0;

        ShopPlayerState ShopStateFor(ulong id)
        {
            if(!shopPlayers.TryGetValue(id,out var state)) { state=new ShopPlayerState();shopPlayers.Add(id,state); }
            return state;
        }
        ShopPlayerState ShopSnapshotFor(ulong id) => ShopStateFor(id);
        void AcceptShopSnapshot(ShopPlayerState state)
        {
            if(state==null) return;
            remoteShopState=state;
            if(state.response<shopRequest) return;
            shopBuyPending=false;
            bool wasOpen=ShopOpen;
            browsingShop=state.opened;
            if(ShopOpen) ReleaseMouse();
            else if(wasOpen && !ChoosingUpgrade && !LocalMenuOpen && Health>0 && !Won) CaptureMouse();
        }
        void ResetShops()
        {
            shopPlayers.Clear();remoteShopState=new ShopPlayerState();
            browsingShop=-1;shopRequest=shopRevision=0;shopBuyPending=false;
        }
        void CreateShops()
        {
            if(!root || IsCoopClient) return;
            for(int i=0;i<ShopLocations.Length;i++)
            {
                if(!shopMaterials[i]) shopMaterials[i]=MakeMaterial(RarityColor(ShopTiers[i]));
                ShopNpcPart("Shopkeeper / "+ShopTiers[i],PrimitiveType.Capsule,ShopLocations[i]+Vector3.up*.9f,new Vector3(.8f,.9f,.8f),shopMaterials[i]);
                ShopNpcPart("Shopkeeper / counter",PrimitiveType.Cube,ShopLocations[i]+new Vector3(0,.5f,-1),new Vector3(1.8f,1,.65f),shopMaterials[i]);
            }
        }
        void ShopNpcPart(string name,PrimitiveType shape,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(shape);part.name=name;part.layer=2;
            part.transform.SetParent(root);part.transform.position=position;part.transform.localScale=scale;
            Destroy(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;
        }
        void DisposeShops() { foreach(var material in shopMaterials) if(material) Destroy(material); }

        int NearestShop(float radius)
        {
            if(!player) return -1;
            int nearest=-1;float best=radius*radius;
            for(int i=0;i<ShopLocations.Length;i++)
            {
                float distance=(player.transform.position-(ShopLocations[i]+Vector3.up)).sqrMagnitude;
                if(distance<best) {best=distance;nearest=i;}
            }
            return nearest;
        }
        bool ShopInteractionAllowed => Running && !Won && !PartyDefeated && !ChoosingUpgrade && !PartyPaused && !LocalMenuOpen && Health>0 && player && !player.IsZiplining;
        bool WithinShop(int index) => index>=0 && index<ShopLocations.Length && player &&
            Vector3.Distance(player.transform.position,ShopLocations[index]+Vector3.up)<=ShopInteractRange &&
            !Physics.Linecast(player.transform.position,ShopLocations[index]+Vector3.up,WorldMask);

        public bool TryOpenShop(int shop=-1)
        {
            if(!ShopInteractionAllowed) return false;
            if(shop<0) shop=NearestShop(ShopInteractRange);
            if(!WithinShop(shop)) return false;
            if(LocalShopState.visits[shop].purchased) {Notice="You already bought from this shop.";return false;}
            browsingShop=shop;shopBuyPending=false;
            ReleaseMouse();IssueShopCommand("open",shop,-1,0);
            return true;
        }
        public void CloseShop()
        {
            if(!ShopOpen) return;
            int shop=browsingShop;browsingShop=-1;shopBuyPending=false;
            if(Running) IssueShopCommand("close",shop,-1,0);
            if(!ChoosingUpgrade && !LocalMenuOpen && Health>0 && !Won) CaptureMouse();
        }
        public void BuyShopOffer(int offer)
        {
            if(!ShopOpen || shopBuyPending) return;
            var state=LocalShopState;
            if(state.visits==null || browsingShop>=state.visits.Length) return;
            var visit=state.visits[browsingShop];
            if(!visit.rolled || visit.purchased || offer<0 || offer>=visit.offers.Count) return;
            shopBuyPending=IsCoopClient;
            IssueShopCommand("buy",browsingShop,offer,visit.revision);
        }
        void IssueShopCommand(string action,int shop,int offer,int revision)
        {
            var command=new ShopCommand {action=action,shop=shop,offer=offer,revision=revision,run=runId,request=++shopRequest};
            if(IsCoopClient) SendJson("swarm.shop.command",NetworkManager.ServerClientId,JsonUtility.ToJson(command));
            else ProcessShopCommand(LocalShopId,command);
        }
        void ReceiveShopCommand(ulong sender,FastBufferReader reader)
        {
            if(!CoopAuthority || !party.ContainsKey(sender)) return;
            var command=JsonUtility.FromJson<ShopCommand>(ReadJson(reader));
            if(command==null || command.run!=runId) return;
            ulong original=contextId;SwitchContext(sender);
            ProcessShopCommand(sender,command);
            SwitchContext(original);snapshotClock=0;
        }
        void ProcessShopCommand(ulong sender,ShopCommand command)
        {
            if(!Running || command.run!=runId) return;
            var state=ShopStateFor(sender);
            if(command.request<=state.response) return;
            state.response=command.request;
            if(command.action=="close") {state.opened=-1;return;}
            if(!ShopInteractionAllowed || !WithinShop(command.shop)) {state.opened=-1;state.message="Shop unavailable or out of reach.";return;}
            var visit=state.visits[command.shop];
            if(command.action=="open")
            {
                if(!visit.rolled)
                {
                    visit.rolled=true;visit.revision=++shopRevision;
                    var pool=ShopEligiblePool();
                    while(visit.offers.Count<3 && pool.Count>0)
                    {int pick=UnityEngine.Random.Range(0,pool.Count);visit.offers.Add(pool[pick]);pool.RemoveAt(pick);}
                }
                state.opened=visit.purchased ? -1 : command.shop;state.message=visit.purchased ? "You already bought from this shop." : "Choose one personal upgrade. Combat continues.";
            }
            else if(command.action=="buy")
            {
                if(state.opened!=command.shop || visit.purchased || !visit.rolled || command.revision!=visit.revision || command.offer<0 || command.offer>=visit.offers.Count) return;
                BoltUpgrade offer=visit.offers[command.offer];
                if(!ShopEligiblePool().Contains(offer)) {state.message="This offer no longer fits your build.";return;}
                int price=ShopCost(command.shop);
                if(SharedMoney<price) {state.message="Not enough party money.";return;}
                // Claim before applying to prevent duplicate requests charging twice.
                visit.purchased=true;visit.revision=++shopRevision;
                SharedMoney-=price;ApplyTieredUpgrade(offer,ShopTiers[command.shop]);UpgradeCount++;
                state.opened=-1;state.message="Personal upgrade purchased.";
            }
            if(!IsCoopClient && sender==LocalShopId)
            {
                browsingShop=state.opened;
                if(!ShopOpen && !ChoosingUpgrade && !LocalMenuOpen && Health>0) CaptureMouse();
            }
        }
        List<BoltUpgrade> ShopEligiblePool()
        {
            var pool=new List<BoltUpgrade> {BoltUpgrade.HeavyBolts};
            if(FireInterval>.08f) pool.Add(BoltUpgrade.RapidFire);
            if(ProjectileCount<6) pool.Add(BoltUpgrade.MoreBolts);
            if(RicochetRank<3) pool.Add(BoltUpgrade.Ricochet);
            if(PierceRank<3) pool.Add(BoltUpgrade.Piercing);
            if(BlastRank<3) pool.Add(BoltUpgrade.ExplosiveHits);
            AddLoadoutOffers(pool);return pool;
        }
        void TickShops()
        {
            if(ShopOpen && (!Running || Won || Health<=0 || ChoosingUpgrade || PartyPaused || LocalMenuOpen || !WithinShop(browsingShop))) CloseShop();
            if(CoopAuthority)
            {
                SaveContext();
                foreach(var entry in shopPlayers)
                {
                    if(!party.TryGetValue(entry.Key,out var owner)) {entry.Value.opened=-1;continue;}
                    var motor=MotorFor(entry.Key);int shop=entry.Value.opened;
                    if(shop<0) continue;
                    if(!motor || owner.health<=0 || owner.choosing || PartyPaused || Won ||
                        Vector3.Distance(motor.transform.position,ShopLocations[shop]+Vector3.up)>ShopInteractRange)
                        entry.Value.opened=-1;
                }
            }
            if(ShopOpen && !LocalMenuOpen && !ChoosingUpgrade)
                for(int i=0;i<3;i++) if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))) BuyShopOffer(i);
        }
        public void DrawShopGui(float width,float height)
        {
            if(!Running || ChoosingUpgrade || LocalMenuOpen || Won || Health<=0) return;
            if(!ShopOpen)
            {
                int nearby=NearestShop(7);
                if(nearby>=0)
                {
                    var visit=LocalShopState.visits[nearby];
                    HudLabel(new Rect(width/2-250,height-210,500,28),$"{ShopTiers[nearby]} SHOP  /  "+(visit.purchased ? "PURCHASED" : $"E: browse  /  {ShopCost(nearby)} party money"),hudSmall,RarityColor(ShopTiers[nearby]));
                }
                return;
            }
            int index=browsingShop;var state=LocalShopState;var row=state.visits[index];Color color=RarityColor(ShopTiers[index]);
            var panel=new Rect(width/2-510,height/2-225,1020,450);HudPanelBox(panel,color);
            HudLabel(new Rect(panel.x+24,panel.y+18,970,40),$"{ShopTiers[index].ToString().ToUpperInvariant()} SHOP  /  PERSONAL UPGRADE",hudCardTitle);
            HudLabel(new Rect(panel.x+24,panel.y+62,970,48),$"Combat continues. One purchase per player. Party money: {SharedMoney}\n{state.message}",hudSmall,HudMuted);
            if(!row.rolled) HudLabel(new Rect(panel.x+24,panel.y+125,950,40),"Waiting for shop offers...",hudText);
            for(int i=0;i<row.offers.Count;i++)
            {
                var card=new Rect(panel.x+24+i*326,panel.y+122,312,250);HudPanelBox(card,color);
                var offer=row.offers[i];HudLabel(new Rect(card.x+14,card.y+12,284,45),CardTitle(offer),hudCardTitle);
                HudLabel(new Rect(card.x+14,card.y+65,284,120),TieredDetail(offer,ShopTiers[index]),hudSmall,HudMuted);
                bool old=GUI.enabled;GUI.enabled=!row.purchased && !shopBuyPending && SharedMoney>=ShopCost(index) && ShopEligiblePool().Contains(offer);
                if(GUI.Button(new Rect(card.x+14,card.y+190,284,45),$"{i+1}  BUY / {ShopCost(index)} MONEY",hudButton)) BuyShopOffer(i);
                GUI.enabled=old;
            }
            if(GUI.Button(new Rect(panel.x+690,panel.y+389,305,40),"CLOSE SHOP / ESC",hudButton)) CloseShop();
        }
    }
}
