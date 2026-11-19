using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Swarm
{
    // The host owns combat simulation. Movement remains owner-authoritative.
    // Each participant has independent build/health/choice state; shared world state
    // is simulated once. Explicit context switches reuse the solo weapon simulation.
    public partial class CombatPlayground
    {
        [Serializable]
        public class PartyPlayerState
        {
            public ulong id;
            public float health=100, damage=15, interval=.5f, fire, hurt, power=1,crit,regenAdjust;
            public int shockRank,shockCharge,critHits,shockHits,knockbackSerial;
            public Vector3 knockback;
            public List<UpgradeRarity> rarities=new List<UpgradeRarity>();
            public float[] rarityPower={1,1,1,1};
            public int level=1, xp, totalXP, shots, upgrades, projectiles=1, ricochet, pierce, blast, rerolls=3, recovery, version;
            public int direct, ricochetHits, pierceHits, explosionHits;
            public bool choosing, bonus; public int pending,bonusTier=-1; public List<int> pendingTiers=new List<int>();
            public List<BoltUpgrade> offers = new List<BoltUpgrade>();
            public int[] ranks = new int[4];
            public float[] clocks = new float[4];
        }
        [Serializable] class CombatCommand { public string action; public int choice, version, run; }
        [Serializable] class VisualState
        {
            public int id, shape; public Vector3 position, scale; public Quaternion rotation;
            public Color color; public string label; public Vector3[] points; public float width,trailTime,trailWidth; public bool loop,localOnly; public ulong owner;
        }
        [Serializable] class PartyMemberInfo { public ulong id; public float health; public bool choosing; }
        [Serializable] class PartySnapshot
        {
            public int run, wave, kills, totalEnemies, orbs, cache, guards, spawned, selectedCache;
            public int[] caches,routeStates,routeCounts,routeTotals;
            public float[] routeClocks;
            public int downloadState,downloadPlayers,downloadSpawns,downloadIndex;
            public int money,purchases,bossPhase,bossReady,bossEligible;
            public float bossClock; public bool bossVoted; public bool[] openedCrates,brokenSalvage;
            public int[] downloadStates; public float[] downloadPercents;
            public float downloadProgress,downloadEmpty;
            public bool running, won, resting, paused, defeated;
            public float rest, elapsed, cacheSeconds;
            public string notice,bossMove;
            public RunLayoutData layout;
            public ShopPlayerState shop;
            public EliteChallengeSnapshot challenge;
            public FreeChestSnapshot freeChests;
            public PartyPlayerState player;
            public VisualState[] visuals;
            public int revives,menuPauseCount; public PartyMemberInfo[] roster;
        }
        class ProxyVisual { public GameObject body; public Vector3 position; public Quaternion rotation; public string label; public bool ownRange; }
        readonly Dictionary<ulong,PartyPlayerState> party = new Dictionary<ulong,PartyPlayerState>();
        readonly Dictionary<int,ProxyVisual> proxies = new Dictionary<int,ProxyVisual>();
        NetworkManager boundManager;
        GameObject proxyRoot;
        readonly Dictionary<int,Material> proxyTints=new Dictionary<int,Material>();
        ulong contextId;
        int seenKnockback;
        int choiceVersion, runId, remoteEnemyCount, remoteOrbCount, remoteCacheGuards, reviveSerial, seenRevive;
        float snapshotClock;
        bool partyWasNetworked;
        PartyMemberInfo[] remoteRoster=new PartyMemberInfo[0];
        public bool PartyPaused { get; private set; }
        public bool PartyDefeated { get; private set; }
        public bool IsCoopClient => NetworkActive && !NetworkManager.Singleton.IsServer;
        bool CoopAuthority => NetworkActive && NetworkManager.Singleton.IsServer && party.Count > 0;
        public int PartySize => party.Count;
        public int ReceivedCombatSnapshots { get; private set; }
        public int ReplicatedVisualCount => proxies.Count;
        public int ReplicatedOwnRangeCount
        {
            get { int count=0; foreach (var proxy in proxies.Values) if (proxy.ownRange) count++; return count; }
        }
        public int ReplicatedWeaponShapeMask { get; private set; }
        public PartyPlayerState PlayerState(ulong id) => party.TryGetValue(id,out var state) ? state : null;

        void ResolveLocalPlayer()
        {
            if (!NetworkActive)
            {
                if (!player || !player.gameObject.activeInHierarchy) player = FindObjectOfType<MomentumMotor>();
                return;
            }
            if (!NetworkManager.Singleton.IsConnectedClient) return;
            var local = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (local) player = local.GetComponent<MomentumMotor>();
        }
        MomentumMotor MotorFor(ulong id)
        {
            var manager = NetworkManager.Singleton;
            if (!manager || !manager.ConnectedClients.TryGetValue(id,out var client) || !client.PlayerObject) return null;
            return client.PlayerObject.GetComponent<MomentumMotor>();
        }
        void SaveContext()
        {
            if (!party.TryGetValue(contextId,out var s)) return;
            s.health=Health; s.damage=Damage; s.interval=FireInterval; s.fire=fireClock; s.hurt=hurtClock; s.power=powerMultiplier;
            s.level=Level; s.xp=xp; s.totalXP=TotalXP; s.shots=ShotsFired; s.upgrades=UpgradeCount; s.projectiles=ProjectileCount;
            s.ricochet=RicochetRank; s.pierce=PierceRank; s.blast=BlastRank; s.rerolls=RerollsRemaining; s.recovery=recoveryRank;
            s.direct=DirectHits; s.ricochetHits=RicochetHits; s.pierceHits=PierceHits; s.explosionHits=ExplosionHits;
            s.choosing=ChoosingUpgrade; s.bonus=BonusUpgrade; s.pending=bonusPending; s.bonusTier=currentBonusTier; s.pendingTiers.Clear(); s.pendingTiers.AddRange(bonusTiers); s.version=choiceVersion;
            s.crit=criticalChance; s.regenAdjust=rarityRecoveryAdjustment; s.shockRank=shockwaveRank; s.shockCharge=shockwaveCharge;
            s.critHits=CriticalHits; s.shockHits=ShockwavesTriggered;
            s.rarities.Clear(); s.rarities.AddRange(choiceRarities); System.Array.Copy(rarityWeaponPower,s.rarityPower,4);
            s.offers.Clear(); s.offers.AddRange(choices);
            for (int i=0;i<ExtraWeapons.Length;i++)
            { s.ranks[i]=WeaponRank(ExtraWeapons[i]); s.clocks[i]=weaponClocks.TryGetValue(ExtraWeapons[i],out var clock) ? clock : 0; }
        }
        void LoadState(PartyPlayerState s)
        {
            Health=s.health; Damage=s.damage; FireInterval=s.interval; fireClock=s.fire; hurtClock=s.hurt; powerMultiplier=s.power;
            Level=s.level; xp=s.xp; TotalXP=s.totalXP; ShotsFired=s.shots; UpgradeCount=s.upgrades; ProjectileCount=s.projectiles;
            RicochetRank=s.ricochet; PierceRank=s.pierce; BlastRank=s.blast; RerollsRemaining=s.rerolls; recoveryRank=s.recovery;
            DirectHits=s.direct; RicochetHits=s.ricochetHits; PierceHits=s.pierceHits; ExplosionHits=s.explosionHits;
            ChoosingUpgrade=s.choosing; BonusUpgrade=s.bonus; bonusPending=s.pending; currentBonusTier=s.bonusTier; bonusTiers.Clear(); bonusTiers.AddRange(s.pendingTiers); choiceVersion=s.version;
            criticalChance=s.crit; rarityRecoveryAdjustment=s.regenAdjust; shockwaveRank=s.shockRank; shockwaveCharge=s.shockCharge;
            CriticalHits=s.critHits; ShockwavesTriggered=s.shockHits;
            choiceRarities.Clear(); choiceRarities.AddRange(s.rarities); System.Array.Copy(s.rarityPower,rarityWeaponPower,4);
            choices.Clear(); choices.AddRange(s.offers); weapons.Clear(); weaponClocks.Clear();
            for (int i=0;i<ExtraWeapons.Length;i++)
                if (s.ranks[i]>0) { weapons.Add(ExtraWeapons[i],s.ranks[i]); weaponClocks.Add(ExtraWeapons[i],s.clocks[i]); }
        }
        void SwitchContext(ulong id)
        {
            SaveContext();
            if (!party.TryGetValue(id,out var s)) return;
            contextId=id; var motor=MotorFor(id); if (motor) player=motor; LoadState(s);
        }
        void StartParty()
        {
            runId++; reviveSerial=0;
            contextId=NetworkManager.Singleton.LocalClientId;
            foreach (ulong id in NetworkManager.Singleton.ConnectedClientsIds) party.Add(id,new PartyPlayerState { id=id });
            SaveContext(); snapshotClock=0; partyWasNetworked=true;
        }
        void ClearParty()
        {
            party.Clear(); ResetMenuPauseState(); PartyPaused=PartyDefeated=false;
            if (proxyRoot) { proxyRoot.SetActive(false); Destroy(proxyRoot); }
            proxies.Clear(); remoteRoster=new PartyMemberInfo[0]; remoteEnemyCount=remoteOrbCount=remoteCacheGuards=0;
        }
        void TickConnection()
        {
            if (NetworkActive && NetworkManager.Singleton.CustomMessagingManager != null)
            {
                if (boundManager != NetworkManager.Singleton)
                {
                    boundManager=NetworkManager.Singleton;
                    boundManager.CustomMessagingManager.RegisterNamedMessageHandler("swarm.combat.command",ReceiveCommand);
                    boundManager.CustomMessagingManager.RegisterNamedMessageHandler("swarm.combat.state",ReceiveSnapshot);
                    boundManager.CustomMessagingManager.RegisterNamedMessageHandler("swarm.drill.state",ReceiveDrillState);
                    boundManager.CustomMessagingManager.RegisterNamedMessageHandler("swarm.shop.command",ReceiveShopCommand);
                }
                ResolveLocalPlayer(); partyWasNetworked=true;
            }
            else if (partyWasNetworked)
            {
                StopCombat(); partyWasNetworked=false; boundManager=null; player=null;
                ResolveLocalPlayer(); Notice="Disconnected — offline movement practice.";
            }
            if (IsCoopClient && !PartyPaused && !LocalMenuOpen)
                foreach (var visual in proxies.Values)
                    if (visual.body)
                    {
                        visual.body.transform.position=Vector3.Lerp(visual.body.transform.position,visual.position,1-Mathf.Exp(-20*Time.deltaTime));
                        visual.body.transform.rotation=Quaternion.Slerp(visual.body.transform.rotation,visual.rotation,1-Mathf.Exp(-20*Time.deltaTime));
                    }
        }
        void HandleChoiceKeys()
        {
            if (!ChoosingUpgrade || Health<=0 || Won || LocalMenuOpen) return;
            for (int i=0;i<choices.Count;i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))) { ChooseUpgrade(i); return; }
            if (Input.GetKeyDown(KeyCode.T)) RerollUpgrades();
            if (Input.GetKeyDown(KeyCode.X)) SkipUpgrade();
        }
        void UpdateParty()
        {
            HandleChoiceKeys();
            if (IsCoopClient) return;
            SaveContext();
            var departed=new List<ulong>();
            foreach (var entry in party) if (!MotorFor(entry.Key)) departed.Add(entry.Key);
            foreach (ulong id in departed) { party.Remove(id); menuPausers.Remove(id); }
            PrunePulseIndicators();
            RefreshPartyPause();
            PartyDefeated=party.Count>0;
            foreach (var state in party.Values) PartyDefeated &= state.health<=0;
            float dt=Mathf.Min(Time.deltaTime,.05f);
            if (!PartyPaused && !PartyDefeated && !Won)
            {
                elapsed+=dt; spawnClock-=dt;
                SwitchToNearest(CanyonLayout.BasinEncounterPosition);
                TickEffects(dt);
                TickBoss(dt);
                if (AutoSpawn) { if (bossPhase != BossPhase.Fighting) TickRouteEncounters(dt); TickWaves(dt); }
                TickEnemies(dt); TickEnemyShots(dt);
                foreach (ulong id in new List<ulong>(party.Keys))
                {
                    SwitchContext(id);
                    if (Health<=0) { SetPulseVisible(player,false); continue; }
                    fireClock-=dt; hurtClock-=dt;
                    if (recoveryRank>0) Health=Mathf.Min(100,Health+RecoveryPerSecond*dt);
                    if (AutoFire && fireClock<=0) Fire();
                    if (AutoFire) TickLoadout(dt);
                }
                SwitchContext(NetworkManager.Singleton.LocalClientId);
                TickBullets(dt); TickOrbs(dt); TickCache(dt); TickDownload(dt); TickEliteChallenges(dt);
            }
            else PauseAgents();
            SwitchContext(NetworkManager.Singleton.LocalClientId); SaveContext();
            snapshotClock-=dt;
            if (snapshotClock<=0) { PublishSnapshot(); snapshotClock=.1f; }
        }
        MomentumMotor NearestLiving(Vector3 point)
        {
            SaveContext(); MomentumMotor nearest=null; float distance=float.MaxValue;
            foreach (var entry in party)
            {
                var motor=MotorFor(entry.Key);
                if (!motor || entry.Value.health<=0) continue;
                float next=(motor.transform.position-point).sqrMagnitude;
                if (next<distance) { distance=next; nearest=motor; }
            }
            return nearest;
        }
        void SwitchToNearest(Vector3 point)
        {
            var nearest=NearestLiving(point);
            if (nearest) SwitchContext(nearest.GetComponent<NetworkAvatar>().OwnerClientId);
        }
        void GivePartyXP(int amount)
        {
            ulong original=contextId;
            foreach (ulong id in new List<ulong>(party.Keys))
            {
                SwitchContext(id); xp+=amount; TotalXP+=amount;
                if (!ChoosingUpgrade && xp>=RequiredXP && Health>0) OpenUpgrade();
            }
            SwitchContext(original);
        }
        void GivePartyCacheReward()
        {
            ulong original=contextId;
            foreach (ulong id in new List<ulong>(party.Keys))
            { SwitchContext(id); QueueBonusUpgrade(); }
            SwitchContext(original);
        }
        void ReviveParty()
        {
            SaveContext(); ulong original=contextId;
            var revived=new List<ulong>();
            foreach (var state in party.Values)
                if (state.health<=0)
                {
                    state.health=50; state.hurt=2; revived.Add(state.id);
                    if (state.id==NetworkManager.Singleton.LocalClientId) MotorFor(state.id).Respawn();
                }
            // Reload before switching so the outgoing context cannot overwrite its revive.
            LoadState(party[contextId]);
            foreach (ulong id in revived)
            {
                SwitchContext(id);
                if (xp>=RequiredXP) OpenUpgrade();
                else if (bonusPending>0) OpenBonusUpgrade();
            }
            SwitchContext(original);
            if (revived.Count>0) reviveSerial++;
        }
        void HitPartyWithSlam(Vector3 center) => HitPartyWithSlamScaled(center,25);

        void HitPartyWithSlamScaled(Vector3 center,float damage)
        {
            ulong original=contextId;
            foreach (ulong id in new List<ulong>(party.Keys))
            {
                SwitchContext(id);
                if (Health<=0) continue;
                Vector3 delta=player.transform.position-center;
                if (new Vector2(delta.x,delta.z).magnitude<4 && delta.y<2.4f && delta.y> -2 && hurtClock<=0)
                { Health=Mathf.Max(0,Health-damage); hurtClock=.75f; }
            }
            SwitchContext(original);
        }
        bool HitPartyWithShot(EnemyShot shot,float dt)
        {
            ulong original=contextId;
            Vector3 step=shot.velocity*dt;
            float wallDistance=Physics.Raycast(shot.body.transform.position,step.normalized,out var wall,step.magnitude,WorldMask) ? wall.distance : step.magnitude;
            PartyPlayerState target=null; float best=float.MaxValue;
            SaveContext();
            foreach (var state in party.Values)
            {
                var motor=MotorFor(state.id); if (!motor || state.health<=0) continue;
                Vector3 delta=motor.transform.position-shot.body.transform.position;
                float t=Mathf.Clamp(Vector3.Dot(delta,step)/Mathf.Max(.0001f,step.sqrMagnitude),0,1);
                Vector3 closest=shot.body.transform.position+step*t;
                Vector3 relative=closest-motor.transform.position;
                if (new Vector2(relative.x,relative.z).sqrMagnitude>.7f*.7f || Mathf.Abs(relative.y)>1.2f || t*step.magnitude>wallDistance || t>=best) continue;
                target=state; best=t;
            }
            if (target==null) return false;
            SwitchContext(target.id);
            if (hurtClock<=0) { Health=Mathf.Max(0,Health-shot.damage); hurtClock=.75f; EnemyProjectileHits++; }
            SwitchContext(original); return true;
        }
        void SendCombatCommand(string action,int choice=0)
        {
            if (!IsCoopClient || boundManager==null) return;
            SendJson("swarm.combat.command",NetworkManager.ServerClientId,JsonUtility.ToJson(new CombatCommand { action=action,choice=choice,version=choiceVersion,run=runId }));
        }
        void ReceiveCommand(ulong sender,FastBufferReader reader)
        {
            if (!CoopAuthority || !party.ContainsKey(sender)) return;
            var command=JsonUtility.FromJson<CombatCommand>(ReadJson(reader));
            if (command==null || command.run!=runId) return;
            ulong original=contextId; SwitchContext(sender);
            if(command.action=="menu" && (command.choice==0 || command.choice==1)) ApplyMenuPauseCommand(sender,command.choice!=0);
            else if(command.action=="supply")TrySupplyInteraction(command.choice);
            else if (command.action=="free-chest") TryFreeChest(command.choice);
            else if (command.action=="elite-challenge") TryEliteChallenge(command.choice);
            else if (command.action=="boss-console") TryBossConsole();
            else if (command.action=="boss-ready") ReadyForBoss();
            else if (command.action=="cache") StartCacheChallenge();
            else if (ChoosingUpgrade && choiceVersion==command.version)
            {
                if (command.action=="choose") ChooseUpgrade(command.choice);
                else if (command.action=="skip") SkipUpgrade();
                else if (command.action=="reroll") RerollUpgrades();
            }
            SwitchContext(original); snapshotClock=0;
        }
        void SendJson(string name,ulong recipient,string json)
        {
            int capacity=System.Text.Encoding.UTF8.GetByteCount(json)+16;
            using (var writer=new FastBufferWriter(capacity,Allocator.Temp,capacity))
            {
                // UTF-8 byte payload avoids allocating twice the JSON size in NGO strings.
                byte[] bytes=System.Text.Encoding.UTF8.GetBytes(json);
                writer.WriteValueSafe(bytes.Length); writer.WriteBytesSafe(bytes);
                boundManager.CustomMessagingManager.SendNamedMessage(name,recipient,writer,NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
        string ReadJson(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int length);
            if (length<0 || length>1000000) return "{}";
            byte[] bytes=new byte[length]; reader.ReadBytesSafe(ref bytes,length);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        VisualState[] CaptureVisuals()
        {
            var visuals=new List<VisualState>();
            if (!root) return visuals.ToArray();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is TrailRenderer) continue;
                var line=renderer as LineRenderer;
                var filter=renderer.GetComponent<MeshFilter>();
                if (!line && !filter) continue;
                var t=renderer.transform;
                var visual=new VisualState { id=renderer.GetInstanceID(),position=t.position,rotation=t.rotation,scale=t.lossyScale,
                    color=renderer.sharedMaterial ? renderer.sharedMaterial.color : Color.white,shape=0 };
                if (renderer.HasPropertyBlock())
                {
                    var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); if (block.HasColor("_Color")) visual.color=block.GetColor("_Color");
                }
                var trail=renderer.GetComponent<TrailRenderer>();
                if (trail) { visual.trailTime=trail.time; visual.trailWidth=trail.startWidth; }
                if (line)
                {
                    visual.shape=3; visual.width=line.startWidth; visual.loop=line.loop;
                    foreach (var range in pulseIndicators)
                        if (range.Key && (range.Value[0]==line || range.Value[1]==line))
                        {
                            var avatar=range.Key.GetComponent<NetworkAvatar>();
                            visual.localOnly=true; visual.owner=avatar ? avatar.OwnerClientId : 0;
                            break;
                        }
                    visual.points=new Vector3[line.positionCount]; line.GetPositions(visual.points);
                    if (!line.useWorldSpace) for (int i=0;i<visual.points.Length;i++) visual.points[i]=t.TransformPoint(visual.points[i]);
                }
                else
                {
                    string mesh=filter.sharedMesh ? filter.sharedMesh.name : "Sphere";
                    visual.shape=mesh=="SwarmBoomerang" ? 5 : mesh=="SwarmArrow" ? 6 : mesh=="SwarmRocket" ? 7 : mesh.Contains("Cube") ? 1 : mesh.Contains("Capsule") ? 2 : mesh.Contains("Cylinder") ? 4 : 0;
                    var agent=renderer.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
                    if (agent && renderer.gameObject.name=="Visual") visual.label=agent.name;
                }
                visuals.Add(visual);
            }
            return visuals.ToArray();
        }
        void PublishSnapshot()
        {
            if (!boundManager || !boundManager.IsServer) return;
            SaveContext(); var visuals=CaptureVisuals();
            var roster=new List<PartyMemberInfo>();
            foreach (var state in party.Values) roster.Add(new PartyMemberInfo { id=state.id,health=state.health,choosing=state.choosing });
            foreach (var entry in party)
            {
                if (entry.Key==boundManager.LocalClientId) continue;
                var packet=new PartySnapshot { run=runId,running=Running,won=Won,paused=PartyPaused,defeated=PartyDefeated,
                    wave=Wave,kills=Kills,totalEnemies=enemies.Count,orbs=orbs.Count,cache=(int)CacheState,guards=CacheGuardsRemaining,
                    selectedCache=cacheIndex,caches=System.Array.ConvertAll(cacheStates,value=>(int)value),
                    routeStates=System.Array.ConvertAll(routeStages,value=>(int)value),routeCounts=new[]{RouteEnemiesRemaining(0),RouteEnemiesRemaining(1)},routeClocks=routeTimers,routeTotals=routeSpawned,
                    bossPhase=(int)bossPhase,bossReady=bossReady,bossEligible=bossEligible,bossClock=bossClock,
                    bossVoted=bossVotes.Contains(entry.Key),
                    money=SharedMoney,purchases=CratesPurchased,openedCrates=moneyCratesOpened,brokenSalvage=salvageBroken,
                    downloadIndex=downloadIndex,downloadStates=System.Array.ConvertAll(downloadStates,value=>(int)value),downloadPercents=downloadPercents,
                    downloadState=(int)DownloadState,downloadPlayers=DownloadOccupants,downloadSpawns=DownloadEnemiesSpawned,
                    downloadProgress=DownloadProgress,downloadEmpty=downloadEmptyClock,
                    spawned=waveSpawned,rest=restClock,resting=Resting,elapsed=elapsed,cacheSeconds=CacheSecondsRemaining,
                    notice=Notice,bossMove=SentinelAttackText,player=entry.Value,visuals=visuals,revives=reviveSerial,
                    layout=ActiveRunLayout,shop=ShopSnapshotFor(entry.Key),
                    challenge=BuildChallengeSnapshot(),freeChests=BuildFreeChestSnapshot(),
                    menuPauseCount=MenuPauseCount,roster=roster.ToArray() };
                SendJson("swarm.combat.state",entry.Key,JsonUtility.ToJson(packet));
            }
        }
        void ReceiveSnapshot(ulong sender,FastBufferReader reader)
        {
            if (!IsCoopClient || sender!=NetworkManager.ServerClientId) return;
            var packet=JsonUtility.FromJson<PartySnapshot>(ReadJson(reader));
            if (packet==null || packet.player==null || packet.player.id!=NetworkManager.Singleton.LocalClientId) return;
            bool newRun=packet.run!=runId, wasChoosing=ChoosingUpgrade;
            if (newRun) { ResetLoadout(); ResetShops(); ResetEliteChallenges(); ResetFreeChests(); ResetMenuPauseState(); seenRevive=0; seenKnockback=0; ReplicatedWeaponShapeMask=0; }
            if (newRun && player) player.Respawn();
            if (packet.revives!=seenRevive && Health<=0 && packet.player.health>0 && player) player.Respawn();
            seenRevive=packet.revives; runId=packet.run; contextId=packet.player.id;
            if (packet.running && packet.player.knockbackSerial!=seenKnockback && player)
            {
                seenKnockback=packet.player.knockbackSerial;
                player.ApplyImpulse(packet.player.knockback);
            }
            LoadState(packet.player); Running=packet.running; Won=packet.won; PartyPaused=packet.paused; PartyDefeated=packet.defeated;
            ApplyRunLayout(packet.layout);
            AcceptShopSnapshot(packet.shop);
            AcceptChallengeSnapshot(packet.challenge);
            AcceptFreeChestSnapshot(packet.freeChests);
            AcceptMenuPauseSnapshot(packet.menuPauseCount);
            Wave=packet.wave; Kills=packet.kills; remoteEnemyCount=packet.totalEnemies; remoteOrbCount=packet.orbs;
            cacheIndex=Mathf.Clamp(packet.selectedCache,0,cacheStates.Length-1);
            if (packet.caches!=null && packet.caches.Length==cacheStates.Length)
                for (int i=0;i<cacheStates.Length;i++) cacheStates[i]=(CacheChallengeState)packet.caches[i];
            if (packet.routeStates!=null && packet.routeStates.Length==2 && packet.routeCounts!=null && packet.routeCounts.Length==2 && packet.routeClocks!=null && packet.routeClocks.Length==2)
                for (int i=0;i<2;i++) { routeStages[i]=(RouteStage)packet.routeStates[i]; remoteRouteRemaining[i]=packet.routeCounts[i]; routeTimers[i]=packet.routeClocks[i]; if (packet.routeTotals!=null && packet.routeTotals.Length==2) routeSpawned[i]=packet.routeTotals[i]; }
            bossPhase=(BossPhase)packet.bossPhase; bossReady=packet.bossReady; bossEligible=packet.bossEligible;
            remoteSentinelMove=packet.bossMove ?? "";
            bossClock=packet.bossClock; localBossReady=packet.bossVoted;
            SharedMoney=packet.money;CratesPurchased=packet.purchases;
            if(packet.openedCrates!=null && packet.openedCrates.Length==moneyCratesOpened.Length)System.Array.Copy(packet.openedCrates,moneyCratesOpened,moneyCratesOpened.Length);
            if(packet.brokenSalvage!=null && packet.brokenSalvage.Length==salvageBroken.Length)System.Array.Copy(packet.brokenSalvage,salvageBroken,salvageBroken.Length);
            downloadIndex=Mathf.Clamp(packet.downloadIndex,0,CanyonLayout.DownloadCount-1);
            if(packet.downloadStates!=null && packet.downloadStates.Length==CanyonLayout.DownloadCount && packet.downloadPercents!=null && packet.downloadPercents.Length==CanyonLayout.DownloadCount)
                for(int i=0;i<CanyonLayout.DownloadCount;i++) {downloadStates[i]=(DownloadPhase)packet.downloadStates[i];downloadPercents[i]=packet.downloadPercents[i];}
            DownloadState=(DownloadPhase)packet.downloadState; DownloadProgress=packet.downloadProgress;
            DownloadOccupants=packet.downloadPlayers; DownloadEnemiesSpawned=packet.downloadSpawns; downloadEmptyClock=packet.downloadEmpty;
            CacheState=(CacheChallengeState)packet.cache; remoteCacheGuards=packet.guards; waveSpawned=packet.spawned; restClock=packet.rest;
            Resting=packet.resting; elapsed=packet.elapsed; CacheSecondsRemaining=packet.cacheSeconds; Notice=packet.notice;
            remoteRoster=packet.roster ?? new PartyMemberInfo[0];
            WeaponStatus="Host simulates shared combat";
            if (ChoosingUpgrade || Won || PartyDefeated || LocalMenuOpen) ReleaseMouse();
            else if (wasChoosing || newRun) CaptureMouse();
            ApplyVisuals(packet.visuals); ReceivedCombatSnapshots++;
            if (!Running) { ClearParty(); ResetShops(); ResetEliteChallenges(); ResetFreeChests(); RestoreRunLayout(); }
        }
        void ApplyVisuals(VisualState[] visuals)
        {
            if (!proxyRoot) proxyRoot=new GameObject("Shared combat visuals");
            var seen=new HashSet<int>();
            foreach (var state in visuals)
            {
                if (state.localOnly && state.owner!=NetworkManager.Singleton.LocalClientId) continue;
                seen.Add(state.id);
                if (state.shape>=5 && state.shape<=7) ReplicatedWeaponShapeMask |= 1 << (state.shape-5);
                if (!proxies.TryGetValue(state.id,out var proxy))
                {
                    var body=state.shape==3 ? new GameObject("Shared line",typeof(LineRenderer)) : state.shape>=5 ? new GameObject("Shared weapon",typeof(MeshFilter),typeof(MeshRenderer)) : GameObject.CreatePrimitive(
                        state.shape==1 ? PrimitiveType.Cube : state.shape==2 ? PrimitiveType.Capsule : state.shape==4 ? PrimitiveType.Cylinder : PrimitiveType.Sphere);
                    if (state.shape>=5) body.GetComponent<MeshFilter>().sharedMesh=WeaponMesh(state.shape);
                    body.layer=2; body.transform.SetParent(proxyRoot.transform);
                    if (body.GetComponent<Collider>()) Destroy(body.GetComponent<Collider>());
                    body.GetComponent<Renderer>().sharedMaterial=ProxyTint(state.color,state.shape==3 || state.shape>=5);
                    body.transform.position=state.position; body.transform.rotation=state.rotation;
                    if (state.trailTime>0)
                    {
                        var trail=body.AddComponent<TrailRenderer>(); trail.time=state.trailTime;
                        trail.startWidth=state.trailWidth; trail.endWidth=.01f; trail.minVertexDistance=.12f; trail.sharedMaterial=ProxyTint(state.color,true);
                    }
                    proxy=new ProxyVisual { body=body }; proxies.Add(state.id,proxy);
                }
                proxy.position=state.position; proxy.rotation=state.rotation; proxy.label=state.label; proxy.ownRange=state.localOnly;
                proxy.body.transform.localScale=state.scale;
                proxy.body.GetComponent<Renderer>().sharedMaterial=ProxyTint(state.color,state.shape==3 || state.shape>=5);
                var proxyTrail=proxy.body.GetComponent<TrailRenderer>(); if (proxyTrail) proxyTrail.sharedMaterial=ProxyTint(state.color,true);
                var line=proxy.body.GetComponent<LineRenderer>();
                if (line)
                {
                    line.useWorldSpace=true; line.loop=state.loop; line.positionCount=state.points.Length;
                    line.SetPositions(state.points); line.startWidth=line.endWidth=state.width; line.startColor=line.endColor=Color.white;
                }
            }
            foreach (int id in new List<int>(proxies.Keys))
                if (!seen.Contains(id)) { Destroy(proxies[id].body); proxies.Remove(id); }
        }
        Material ProxyTint(Color color,bool glow)
        {
            Color32 packed=color;
            int key=(packed.r<<16)|(packed.g<<8)|packed.b|(glow ? 1<<24 : 0);
            if (!proxyTints.TryGetValue(key,out var material))
            {
                material=MakeMaterial(color);
                if (glow) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",color*.65f); }
                proxyTints.Add(key,material);
            }
            return material;
        }
        void DrawPartyStatus()
        {
            var roster=new List<PartyMemberInfo>();
            if (IsCoopClient) roster.AddRange(remoteRoster);
            else
                foreach (var state in party.Values) roster.Add(new PartyMemberInfo { id=state.id,health=state.health,choosing=state.choosing });
            if (roster.Count==0) return;
            GUILayout.BeginArea(new Rect(18,238,245,32+roster.Count*22),GUI.skin.box);
            GUILayout.Label("PARTY / SHARED XP");
            foreach (var state in roster)
                GUILayout.Label($"Player {state.id+1}: "+(state.health<=0 ? "DOWNED" : $"{state.health:0} HP")+(state.choosing ? " / choosing" : ""));
            GUILayout.EndArea();
        }
        void DrawSharedLabels()
        {
            if (!ShowEnemyLabels || !Camera.main) return;
            foreach (var visual in proxies.Values)
            {
                if (string.IsNullOrEmpty(visual.label) || !visual.body) continue;
                Vector3 anchor=visual.body.transform.position+Vector3.up*1.5f;
                Vector3 screen=Camera.main.WorldToScreenPoint(anchor);
                if (screen.z<=0 || Vector3.Distance(Camera.main.transform.position,anchor)>35 ||
                    Physics.Linecast(Camera.main.transform.position,anchor,WorldMask)) continue;
                GUI.Label(new Rect(screen.x-70,Screen.height-screen.y,160,24),visual.label);
            }
        }
        void DisposeCoop()
        {
            if (boundManager && boundManager.CustomMessagingManager!=null)
            {
                boundManager.CustomMessagingManager.UnregisterNamedMessageHandler("swarm.combat.command");
                boundManager.CustomMessagingManager.UnregisterNamedMessageHandler("swarm.combat.state");
                boundManager.CustomMessagingManager.UnregisterNamedMessageHandler("swarm.drill.state");
                boundManager.CustomMessagingManager.UnregisterNamedMessageHandler("swarm.shop.command");
            }
            if (proxyRoot) Destroy(proxyRoot);
            foreach (var material in proxyTints.Values) Destroy(material);
            proxyTints.Clear();
        }
    }
}
