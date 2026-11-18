#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    public class ShopSmokeProbe : MonoBehaviour
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        bool passed=true;
        CombatPlayground combat;
        MomentumMotor motor;
        void Check(bool ok,string text) {passed &= ok;Debug.Log($"SWARM_SHOP_STEP: {text}={ok}");}
        IEnumerator Wait(Func<bool> ready,string label)
        {
            float until=Time.realtimeSinceStartup+20;
            while(!ready() && Time.realtimeSinceStartup<until) yield return null;
            Check(ready(),label);
        }
        float Elapsed => (float)typeof(CombatPlayground).GetField("elapsed",Private).GetValue(combat);
        void Fund(int amount) => typeof(CombatPlayground).GetProperty("SharedMoney").SetValue(combat,amount);
        void WarpToShop(int shop) => motor.SetSpawn(CombatPlayground.ShopLocations[shop]+Vector3.up*1.1f);
        void ReplayBuy(int shop,int revision) => typeof(CombatPlayground).GetMethod("IssueShopCommand",Private).Invoke(combat,new object[]{"buy",shop,0,revision});
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-swarm-shop-test")>=0 || Array.IndexOf(args,"-swarm-shop-role")>=0)
                new GameObject("Personal shop probe").AddComponent<ShopSmokeProbe>();
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60;yield return null;
            var args=Environment.GetCommandLineArgs();int role=Array.IndexOf(args,"-swarm-shop-role");
            combat=CombatPlayground.Instance;
            if(role<0)
            {
                combat.AutoSpawn=combat.AutoFire=false;combat.Begin();motor=FindObjectOfType<MomentumMotor>();motor.enabled=false;
                WarpToShop(0);Check(combat.TryOpenShop(0),"solo browse opens");
                var first=combat.LocalShopState.visits[0];var offers=new List<BoltUpgrade>(first.offers);int revision=first.revision;
                Check(first.offers.Count==3 && new HashSet<BoltUpgrade>(first.offers).Count==3,"three distinct offers");
                Check(!combat.ChoosingUpgrade && !combat.PartyPaused && !combat.LocalMenuOpen,"browse is not group pause");
                float before=Elapsed;yield return new WaitForSecondsRealtime(.4f);
                Check(Elapsed>before+.1f,"clock advances while browsing");
                combat.BuyShopOffer(0);Check(combat.SharedMoney==0 && !first.purchased && combat.ShopOpen,"insufficient funds rejected");
                combat.CloseShop();combat.TryOpenShop(0);
                Check(first.revision==revision && offers.TrueForAll(x=>first.offers.Contains(x)),"reopen preserves catalog");
                Fund(300);int upgrades=combat.UpgradeCount;
                ReplayBuy(0,revision-1);
                Check(combat.SharedMoney==300 && !first.purchased,"stale catalog revision rejected");
                var originalOffer=first.offers[0];
                first.offers[0]=BoltUpgrade.MoreBolts;
                typeof(CombatPlayground).GetProperty("ProjectileCount").SetValue(combat,6);
                combat.BuyShopOffer(0);
                Check(combat.SharedMoney==300 && !first.purchased && combat.UpgradeCount==upgrades,"capped offer rejected without charge");
                first.offers[0]=originalOffer;
                typeof(CombatPlayground).GetProperty("ProjectileCount").SetValue(combat,1);
                combat.BuyShopOffer(0);
                Check(first.purchased && combat.UpgradeCount==upgrades+1 && combat.SharedMoney==300-CombatPlayground.ShopCost(0),"personal purchase applies once");
                Check(!combat.ShopOpen && !combat.ChoosingUpgrade && !combat.PartyPaused,"purchase closes without selection pause");
                int money=combat.SharedMoney;ReplayBuy(0,revision);
                Check(combat.SharedMoney==money && combat.UpgradeCount==upgrades+1 && !combat.TryOpenShop(0),"duplicate and reopening purchased shop rejected");
                WarpToShop(1);combat.TryOpenShop(1);motor.SetSpawn(new Vector3(0,2,-15));
                yield return null;yield return null;
                Check(!combat.ShopOpen,"walking out of range closes browser");
                WarpToShop(1);combat.TryOpenShop(1);
                typeof(CombatPlayground).GetProperty("Health").SetValue(combat,0f);
                yield return null;yield return null;Check(!combat.ShopOpen,"downed closes browser");
                combat.StopCombat();combat.Begin();WarpToShop(0);combat.TryOpenShop(0);
                Check(!combat.LocalShopState.visits[0].purchased && combat.LocalShopState.visits[0].rolled,"new run resets personal purchase");
                combat.StopCombat();
            }
            else
            {
                bool host=args[role+1]=="host";
                var session=FindObjectOfType<MultiplayerSession>();session.Begin(host);
                yield return Wait(()=>NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient.PlayerObject,"connected");
                motor=NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<MomentumMotor>();motor.enabled=false;
                if(host)
                {
                    yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==2,"two players");
                    combat.AutoSpawn=combat.AutoFire=false;combat.Begin();Fund(300);
                    yield return Wait(()=>combat.PlayerState(1).upgrades==1,"remote personal purchase applied");
                    Check(combat.UpgradeCount==0 && !combat.ChoosingUpgrade && !combat.PartyPaused,"other player receives no upgrade or pause");
                    Check(combat.SharedMoney==300-CombatPlayground.ShopCost(0),"shared wallet charged once");
                    WarpToShop(0);Check(combat.TryOpenShop(0),"same npc available to host");
                    Check(!combat.LocalShopState.visits[0].purchased,"host has independent purchase entitlement");
                    float before=Elapsed;yield return new WaitForSecondsRealtime(.4f);
                    Check(Elapsed>before+.1f,"host browse does not stop clock");
                    combat.CloseShop();
                    yield return new WaitForSecondsRealtime(.6f);combat.StopCombat();
                    yield return Wait(()=>NetworkManager.Singleton.ConnectedClientsIds.Count==1,"client left after stop");
                }
                else
                {
                    yield return Wait(()=>combat.Running && combat.SharedMoney==300,"funded run received");
                    WarpToShop(0);yield return new WaitForSecondsRealtime(.5f);
                    Check(combat.TryOpenShop(0),"remote browse requested");
                    yield return Wait(()=>combat.LocalShopState.visits[0].rolled && combat.LocalShopState.opened==0,"host catalog received");
                    var first=combat.LocalShopState.visits[0];int revision=first.revision;var offers=new List<BoltUpgrade>(first.offers);
                    Check(first.offers.Count==3 && !combat.PartyPaused,"remote catalog does not pause");
                    combat.CloseShop();yield return new WaitForSecondsRealtime(.3f);combat.TryOpenShop(0);
                    yield return Wait(()=>combat.LocalShopState.opened==0,"remote reopened");
                    Check(combat.LocalShopState.visits[0].revision==revision && offers.TrueForAll(x=>combat.LocalShopState.visits[0].offers.Contains(x)),"remote catalog stable");
                    combat.BuyShopOffer(0);ReplayBuy(0,revision);
                    yield return Wait(()=>combat.UpgradeCount==1 && combat.LocalShopState.visits[0].purchased,"remote purchase replicated");
                    Check(!combat.ChoosingUpgrade && !combat.ShopOpen && !combat.PartyPaused,"remote purchase needs no group choice");
                    Check(combat.SharedMoney==300-CombatPlayground.ShopCost(0),"duplicate remote request charged once");
                    yield return Wait(()=>!combat.Running,"run stopped");
                }
                session.Leave();
            }
            Debug.Log($"SWARM_SHOP_TEST: {(passed ? "PASS" : "FAIL")} role={(role<0 ? "solo" : args[role+1])}");
            Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
