using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

namespace Swarm
{
    // Shared combat simulation with offline and host-authoritative co-op paths.
    // Upgrade selection pauses combat without changing Time.timeScale.
    public partial class CombatPlayground : MonoBehaviour
    {
        public enum CacheChallengeState { Available, Active, Completed, Failed }
        readonly CacheChallengeState[] cacheStates=new CacheChallengeState[CanyonLayout.CachePositions.Length];
        int cacheIndex;
        public CacheChallengeState CacheState { get => cacheStates[cacheIndex]; private set => cacheStates[cacheIndex]=value; }
        public Vector3 CachePosition => CanyonLayout.CachePositions[cacheIndex];
        public float CacheSecondsRemaining { get; private set; }
        public int CacheGuardsRemaining
        {
            get { if (IsCoopClient) return remoteCacheGuards; int count = 0; foreach (var guard in cacheGuards) if (enemies.Contains(guard)) count++; return count; }
        }
        public bool BonusUpgrade { get; private set; }
        public static CombatPlayground Instance { get; private set; }
        public static bool BlocksInput => Instance && Instance.Running && (Instance.ChoosingUpgrade || Instance.Health <= 0 || Instance.Won || Instance.PartyPaused);
        public bool Won { get; private set; }
        public int Wave { get; private set; }
        public bool Resting { get; private set; }
        public bool Running { get; private set; }
        public bool ChoosingUpgrade { get; private set; }
        public float Health { get; private set; } = 100;
        public int Kills { get; private set; }
        public int Level { get; private set; } = 1;
        public int TotalXP { get; private set; }
        public int ShotsFired { get; private set; }
        public int UpgradeCount { get; private set; }
        public int EnemyCount => IsCoopClient ? remoteEnemyCount : enemies.Count;
        public int OrbCount => IsCoopClient ? remoteOrbCount : orbs.Count;
        public float Damage { get; private set; } = 15;
        public float FireInterval { get; private set; } = .5f;
        public int ProjectileCount { get; private set; } = 1;
        public int RicochetRank { get; private set; }
        public int PierceRank { get; private set; }
        public int BlastRank { get; private set; }
        public int DirectHits { get; private set; }
        public int RicochetHits { get; private set; }
        public int PierceHits { get; private set; }
        public int ExplosionHits { get; private set; }
        public int EnemyShotsFired { get; private set; }
        public int EnemyProjectileHits { get; private set; }
        public int RunnersSpawned { get; private set; }
        public int SpittersSpawned { get; private set; }
        public int EnemyProjectileCount => enemyShots.Count;
        public bool ShowEnemyLabels = true;
        public IReadOnlyList<BoltUpgrade> UpgradeChoices => choices;
        public bool AutoFire = true;
        public bool AutoSpawn = true;
        public string WeaponStatus { get; private set; } = "Waiting for combat";
        public string Notice { get; private set; } = "Try the first combat loop here.";

        class Enemy { public GameObject body; public NavMeshAgent agent; public Renderer visual; public float health, maxHealth, flash, damageScale=1, overtimeHealth=1, overtimeDamage=1, attackClock = 3; public bool elite; public GameObject warning; public EnemyKind kind; public Vector3 aim;
            public ChargePhase chargePhase;
            public SentinelPhase sentinelPhase;
            public int sentinelMove;
            public float sentinelDistance,sentinelRadius;
            public readonly HashSet<ulong> chargeVictims=new HashSet<ulong>();
            public readonly List<Collider> ignoredChargeControllers=new List<Collider>(); }
        class EnemyShot { public GameObject body; public Vector3 velocity; public float life = 4, damage=12; }
        class Bullet
        {
            public GameObject body; public Vector3 velocity; public float life, damage;
            public int pierces, bounces, blast; public bool hasBounced, hasPierced;
            public bool rocket, boomerang, returning, arrow; public float age, returnAge, returnDuration; public Vector3 returnStart, returnForward; public MomentumMotor owner;
            public readonly HashSet<int> hitEnemies = new HashSet<int>();
        }
        class Orb { public GameObject body; public int value; }
        class Effect { public GameObject body; public float remaining, duration, from, to; }
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Bullet> bullets = new List<Bullet>();
        readonly List<Orb> orbs = new List<Orb>();
        readonly List<Effect> effects = new List<Effect>();
        readonly List<BoltUpgrade> choices = new List<BoltUpgrade>();
        readonly List<EnemyShot> enemyShots = new List<EnemyShot>();
        readonly List<Enemy> cacheGuards = new List<Enemy>();
        readonly GameObject[] cacheMarkers=new GameObject[CanyonLayout.CachePositions.Length];
        GameObject cacheMarker => cacheMarkers[cacheIndex];
        int bonusPending;
        MaterialPropertyBlock flashProperties;
        MomentumMotor player;
        Transform root;
        Material enemyMaterial, bulletMaterial, xpMaterial, eliteMaterial, sparkMaterial, blastMaterial, runnerMaterial, spitterMaterial, chargerMaterial;
        GUIStyle enemyLabel;
        NavMeshData navData;
        NavMeshDataInstance navInstance;
        float fireClock, spawnClock, hurtClock, elapsed;
        int xp;
        int waveSpawned;
        Vector3 packApproach;
        float restClock;
        int RequiredXP => 5 + Level * 3;
        const int WorldMask = 1 << 0, EnemyLayer = 8;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!FindObjectOfType<CombatPlayground>() && FindObjectOfType<MomentumMotor>())
                new GameObject("Combat Playground").AddComponent<CombatPlayground>();
        }
        void Awake()
        {
            Instance = this;
            flashProperties = new MaterialPropertyBlock();
            player = FindObjectOfType<MomentumMotor>();
            enemyMaterial = MakeMaterial(new Color(.85f,.18f,.25f));
            bulletMaterial = MakeMaterial(new Color(1f,.85f,.15f));
            xpMaterial = MakeMaterial(new Color(.15f,1f,.7f));
            eliteMaterial = MakeMaterial(new Color(1,.45f,.08f));
            runnerMaterial = MakeMaterial(new Color(1,.85f,.12f));
            spitterMaterial = MakeMaterial(new Color(.65f,.2f,.95f));
            chargerMaterial = MakeMaterial(new Color(1,.4f,.08f));
            sparkMaterial = MakeMaterial(Color.white);
            blastMaterial = MakeMaterial(new Color(1,.6f,.05f));
            foreach (var material in new[] { bulletMaterial, sparkMaterial, blastMaterial })
            { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",material.color*2); }
        }
        Material MakeMaterial(Color color)
        {
            var material = new Material(Shader.Find("Standard")); material.color = color;
            return material;
        }
        bool NetworkActive => NetworkManager.Singleton && NetworkManager.Singleton.IsListening;

        public void Begin()
        {
            if (NetworkActive && !NetworkManager.Singleton.IsServer) return;
            ResolveLocalPlayer();
            StopCombat();
            if (navInstance.valid) navInstance.Remove();
            if (navData) Destroy(navData);
            if (!BuildNavigation()) return;
            root = new GameObject("Combat Entities").transform;
            player.Respawn();
            Health = 100; Kills = xp = TotalXP = ShotsFired = UpgradeCount = 0; Level = 1;
            Damage = 15; FireInterval = .5f; ProjectileCount = 1;
            RicochetRank = PierceRank = BlastRank = DirectHits = RicochetHits = PierceHits = ExplosionHits = 0;
            EnemyShotsFired = EnemyProjectileHits = RunnersSpawned = SpittersSpawned = 0;
            choices.Clear(); ResetLoadout();
            elapsed = fireClock = spawnClock = hurtClock = 0;
            ChoosingUpgrade = false; Running = true;
            Won = Resting = false; Wave = 1; waveSpawned = 0; restClock = 0;
            cacheIndex=0;
            for (int i=0;i<cacheStates.Length;i++) cacheStates[i]=CacheChallengeState.Available;
            CacheSecondsRemaining = 0;
            BonusUpgrade=false; bonusPending=0;
            ResetRouteEncounters();
            ResetDownload();
            ResetBoss();
            ResetEconomy();
            CreateCache(); CreateSupplies();
            WeaponStatus = "Searching for a visible enemy";
            Notice = "Weapon fires automatically. Keep moving and collect green XP.";
            if (NetworkActive) StartParty();
            // An opening pack directly ahead makes combat immediately visible.
            if (AutoSpawn)
            {
                for (int i=0; i<3; i++)
                    if (SpawnAroundPlayer()) waveSpawned++;
                spawnClock = PackRecovery;
            }
            CaptureMouse();
        }
        bool BuildNavigation()
        {
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(null, WorldMask, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .55f; settings.agentHeight = 1.8f;
            settings.agentClimb = .4f; settings.agentSlope = 45;
            navData = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new Bounds(new Vector3(-145,15,340), new Vector3(490,100,780)), Vector3.zero, Quaternion.identity);
            if (!navData) { Notice = "Could not build enemy navigation."; return false; }
            navInstance = NavMesh.AddNavMeshData(navData);
            return navInstance.valid;
        }
        public void StopCombat()
        {
            Running = ChoosingUpgrade = false;
            if (CoopAuthority) PublishSnapshot();
            ClearParty();
            ClearWeaponPresentation();
            if (root) { root.gameObject.SetActive(false); Destroy(root.gameObject); }
            enemies.Clear(); bullets.Clear(); orbs.Clear(); effects.Clear(); choices.Clear(); enemyShots.Clear(); cacheGuards.Clear();
            ResetRouteEncounters();
            ResetDownload();
            ResetBoss();
            ResetEconomy();
            bonusPending=0; BonusUpgrade=false; bonusTiers.Clear(); currentBonusTier=-1;
        }
        void Update()
        {
            TickConnection();
            if (Input.GetKeyDown(KeyCode.F8)) ToggleCombatHud();
            if (Input.GetKeyDown(KeyCode.N)) ShowEnemyLabels = !ShowEnemyLabels;
            if (!Running && (!NetworkActive || NetworkManager.Singleton.IsServer) && Input.GetKeyDown(KeyCode.F)) Begin();
            if (!Running) return;
            if (Input.GetKeyDown(KeyCode.Y)) ReadyForBoss();
            if (NetworkActive) { UpdateParty(); return; }
            if (ChoosingUpgrade && Health > 0 && !Won)
            {
                for (int i=0; i<choices.Count; i++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))) { ChooseUpgrade(i); return; }
                if (Input.GetKeyDown(KeyCode.T)) RerollUpgrades();
                if (Input.GetKeyDown(KeyCode.X)) SkipUpgrade();
                return;
            }
            if (Health <= 0 || Won) return;
            // Escape is a solo pause, so opening menus cannot get the player killed.
            if (Cursor.lockState != CursorLockMode.Locked && !Application.isBatchMode) return;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            elapsed += dt; fireClock -= dt; spawnClock -= dt; hurtClock -= dt;
            if (recoveryRank > 0) Health = Mathf.Min(100,Health+RecoveryPerSecond*dt);
            TickEffects(dt);
            TickBoss(dt);
            if (AutoSpawn) { if (bossPhase != BossPhase.Fighting) TickRouteEncounters(dt); TickWaves(dt); }
            TickEnemies(dt);
            TickEnemyShots(dt);
            if (AutoFire && fireClock <= 0) Fire();
            if (AutoFire) TickLoadout(dt);
            TickBullets(dt);
            TickOrbs(dt);
            TickCache(dt); TickDownload(dt);
            if (Health <= 0 || ChoosingUpgrade || Won) PauseAgents();
        }
        void CreateCache()
        {
            for (int i=0;i<cacheMarkers.Length;i++)
            {
                var marker=GameObject.CreatePrimitive(PrimitiveType.Cube); cacheMarkers[i]=marker;
                marker.name=i==0 ? "Optional supply cache" : "Station supply cache"; marker.layer=2;
                marker.transform.SetParent(root); marker.transform.position=CanyonLayout.CachePositions[i];
                marker.transform.localScale=new Vector3(1.3f,.8f,1.3f);
                Destroy(marker.GetComponent<Collider>()); marker.GetComponent<Renderer>().sharedMaterial=blastMaterial;
                var beacon=new GameObject("Cache beacon",typeof(LineRenderer)); beacon.transform.SetParent(marker.transform,false);
                var line=beacon.GetComponent<LineRenderer>(); line.useWorldSpace=false; line.positionCount=2;
                line.SetPosition(0,Vector3.up); line.SetPosition(1,Vector3.up*6);
                line.startWidth=.12f; line.endWidth=.03f; line.sharedMaterial=blastMaterial;
            }
        }
        int NearestCacheIndex()
        {
            int nearest=0; float distance=float.MaxValue;
            for (int i=0;i<CanyonLayout.CachePositions.Length;i++)
            {
                float d=(player.transform.position-CanyonLayout.CachePositions[i]).sqrMagnitude;
                if (d<distance) { nearest=i; distance=d; }
            }
            return nearest;
        }
        public bool StartCacheChallenge()
        {
            if (!Running || CacheState==CacheChallengeState.Active) return false;
            int nearby=NearestCacheIndex();
            if (cacheStates[nearby]!=CacheChallengeState.Available) return false;
            // Only the host commits the selection; clients request their nearby cache.
            if (!IsCoopClient) cacheIndex=nearby;
            if (IsCoopClient)
            {
                if (!Running || ChoosingUpgrade || Health<=0 || Won || bossPhase==BossPhase.Fighting ||
                    player.IsZiplining || Vector3.Distance(player.transform.position,CanyonLayout.CachePositions[nearby])>3.5f) return false;
                SendCombatCommand("cache"); return true;
            }
            if (!Running || player.IsZiplining || ChoosingUpgrade || Health <= 0 || Won || bossPhase==BossPhase.Fighting ||
                CacheState != CacheChallengeState.Available || Vector3.Distance(player.transform.position,CachePosition) > 3.5f)
                return false;
            cacheGuards.Clear();
            Vector3[] offsets = { Vector3.left*6, Vector3.forward*7, Vector3.right*6, Vector3.back*7 };
            for (int i=0; i<offsets.Length; i++)
                if (SpawnEnemy(CachePosition+offsets[i],false,i == 3 ? EnemyKind.Spitter : i < 2 ? EnemyKind.Runner : EnemyKind.Chaser))
                    cacheGuards.Add(enemies[enemies.Count-1]);
            if (cacheGuards.Count == 0) { Notice = "Cache guards could not find a route. Try activating again."; return false; }
            CacheState = CacheChallengeState.Active; CacheSecondsRemaining = 35;
            Notice = "Cache challenge! Defeat the marked guards in 35 seconds for an extra upgrade.";
            return true;
        }
        void TickCache(float dt)
        {
            foreach (var marker in cacheMarkers) if (marker) marker.transform.Rotate(0,25*dt,0);
            if (CacheState != CacheChallengeState.Active) return;
            CacheSecondsRemaining = Mathf.Max(0,CacheSecondsRemaining-dt);
            if (CacheGuardsRemaining == 0 && (CoopAuthority ? !PartyDefeated : Health > 0))
            {
                CacheState = CacheChallengeState.Completed;
                cacheMarker.GetComponent<Renderer>().sharedMaterial = xpMaterial;
                Notice = "Cache cleared! Take an extra upgrade choice.";
                if (CoopAuthority) GivePartyCacheReward();
                else QueueBonusUpgrade();
            }
            else if (CacheSecondsRemaining <= 0)
            {
                CacheState = CacheChallengeState.Failed;
                cacheMarker.GetComponent<Renderer>().sharedMaterial = enemyMaterial;
                Notice = "Cache timed out. Its guards remain, but the main run continues.";
            }
        }
        void OpenBonusUpgrade()
        {
            currentBonusTier=bonusTiers.Count>0 ? bonusTiers[0] : -1;
            if(bonusTiers.Count>0) bonusTiers.RemoveAt(0);
            bonusPending=Mathf.Max(0,bonusPending-1); BonusUpgrade = true; ChoosingUpgrade = true;
            RollUpgradeChoices();
        }
        string CacheStatus()
        {
            if (CacheState==CacheChallengeState.Active) return $"{CanyonLayout.CacheNames[cacheIndex]}: {CacheGuardsRemaining} guards / {Mathf.CeilToInt(CacheSecondsRemaining)}s";
            int nearby=NearestCacheIndex(); string name=CanyonLayout.CacheNames[nearby];
            if (cacheStates[nearby]==CacheChallengeState.Completed) return name+" CLEARED — bonus earned";
            if (cacheStates[nearby]==CacheChallengeState.Failed) return name+" TIMED OUT — reward lost";
            if (bossPhase==BossPhase.Fighting) return "Caches closed for the sentinel encounter";
            float distance=Vector3.Distance(player.transform.position,CanyonLayout.CachePositions[nearby]);
            return distance<=3.5f ? "E: Open cache — 35s guard challenge" : $"{name}: {distance:0}m — optional upgrade";
        }
        void TickWaves(float dt)
        {
            if (Won) return;
            int nextWave=1+Mathf.FloorToInt(elapsed/60);
            if (nextWave>Wave)
            {
                Wave=nextWave;
                waveSpawned=0;
                Resting=!IsAreaOvertime && bossPhase!=BossPhase.Fighting;
                restClock=Resting ? 5 : 0;
                if (CoopAuthority) ReviveParty();
                Notice=IsAreaOvertime ? "OVERTIME: overwhelming waves keep growing. Defeat the Sentinel!" : "Swarm pressure rising. Regroup for the next wave.";
                if (IsAreaOvertime) spawnClock=0;
            }
            if (Resting)
            {
                restClock-=dt;
                if (restClock>0) return;
                Resting=false;
            }
            if (spawnClock>0 || enemies.Count>=SwarmEnemyLimit) return;

            ulong original=contextId;
            int spawned=0;
            int count=Mathf.Min(SwarmPackSize,SwarmEnemyLimit-enemies.Count);
            if (CoopAuthority) SwitchToSwarmTarget();
            for (int i=0;i<count;i++)
            {
                if (!SpawnAroundPlayer()) continue;
                waveSpawned++;
                spawned++;
            }
            if (CoopAuthority) SwitchContext(original);
            spawnClock=spawned>0 ? PackRecovery : .5f;
        }
        void PauseAgents()
        {
            foreach (var enemy in enemies)
                if (enemy.agent && enemy.agent.isOnNavMesh) enemy.agent.isStopped = true;
        }
        void LateUpdate()
        {
            UpdatePersistentHud();
            if (!NetworkActive && Running && (Health <= 0 || ChoosingUpgrade || Won || (Cursor.lockState != CursorLockMode.Locked && !Application.isBatchMode)))
                PauseAgents();
        }
        float RunMinutes => Mathf.Max(0,elapsed)/60;
        int ExtraPartyMembers => CoopAuthority ? Mathf.Max(0,party.Count-1) : 0;
        void SwitchToSwarmTarget()
        {
            SaveContext();
            var living=new List<ulong>();
            foreach (var member in party.Values)
                if (member.health>0 && MotorFor(member.id)) living.Add(member.id);
            if (living.Count>0) SwitchContext(living[Random.Range(0,living.Count)]);
        }

        bool FindPlayerNavigation(out NavMeshHit destination)
        {
            // Sample the supporting floor first, so nearby upper platforms do not steal the target.
            if (Physics.Raycast(player.transform.position+Vector3.up*.2f,Vector3.down,out var floor,6,WorldMask) &&
                NavMesh.SamplePosition(floor.point,out destination,1.5f,NavMesh.AllAreas)) return true;
            return NavMesh.SamplePosition(player.transform.position,out destination,12,NavMesh.AllAreas);
        }
        bool SpawnAroundPlayer(bool elite = false)
        {
            // Keep the sentinel at the current POI; passage combat uses the nearby fallback.
            if (elite && player.transform.position.x> -45 && player.transform.position.z<=76 && SpawnEnemy(CanyonLayout.BasinEncounterPosition,true)) return true;
            if (elite && player.transform.position.x> -45 && player.transform.position.z>615 && SpawnEnemy(CanyonLayout.StationPoint(0,4,18),true)) return true;
            if (waveSpawned % 3 == 0)
            {
                Vector3 travel = Vector3.ProjectOnPlane(player.Velocity,Vector3.up);
                if (travel.sqrMagnitude < 4) travel = Vector3.ProjectOnPlane(player.view.forward,Vector3.up);
                // Pick a new approach for each pack, with extra room directly ahead.
                float turn = Random.Range(45f,135f)*(Random.value < .5f ? -1 : 1);
                packApproach = Quaternion.Euler(0,turn,0)*travel.normalized;
                if (packApproach.sqrMagnitude < .1f) packApproach = Vector3.forward;
            }
            EnemyKind kind = elite ? EnemyKind.Chaser : KindForWave();
            var candidates = new List<KeyValuePair<float,Vector3>>();
            for (int attempt = 0; attempt < 28; attempt++)
            {
                float angle = Random.Range(0, Mathf.PI * 2);
                float radius = Random.Range(10f,17f);
                Vector3 point = player.transform.position + new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                if (!NavMesh.SamplePosition(point, out var hit, 3, NavMesh.AllAreas)) continue;
                Vector3 offset = hit.position-player.transform.position;
                float horizontalDistance = Vector3.ProjectOnPlane(offset,Vector3.up).magnitude;
                if (horizontalDistance>18 || PlayerNearRouteSpawn(hit.position,9)) continue;
                float alignment = Vector3.Dot(offset.normalized,packApproach);
                // Melee arrive together; ranged pressure comes from another angle.
                float score = (kind == EnemyKind.Spitter ? -alignment*4 : alignment*6) + Random.Range(0f,5f);
                if ((kind == EnemyKind.Spitter || Wave==1 && waveSpawned<3) && !Physics.Linecast(hit.position+Vector3.up*1.3f,player.transform.position,WorldMask))
                    score += 12;
                foreach (var enemy in enemies)
                    if ((enemy.body.transform.position-hit.position).sqrMagnitude < 9) score -= 8;
                candidates.Add(new KeyValuePair<float,Vector3>(score,hit.position));
            }
            candidates.Sort((a,b) => b.Key.CompareTo(a.Key));
            foreach (var candidate in candidates)
                if (SpawnEnemy(candidate.Value,elite,kind)) return true;
            return false;
        }
        EnemyKind KindForWave()
        {
            int chargerFrequency=RunMinutes>=5 ? 6 : 8;
            if (RunMinutes>=2 && waveSpawned%chargerFrequency==chargerFrequency-1) return EnemyKind.Charger;
            if (Wave == 1) return waveSpawned%3 == 2 ? EnemyKind.Runner : EnemyKind.Chaser;
            if (Wave == 2) return waveSpawned%4 == 0 ? EnemyKind.Spitter : waveSpawned%4 == 1 ? EnemyKind.Runner : EnemyKind.Chaser;
            return waveSpawned%4 < 2 ? EnemyKind.Runner : waveSpawned%4 == 2 ? EnemyKind.Spitter : EnemyKind.Chaser;
        }
        public bool SpawnEnemy(Vector3 position, bool elite = false, EnemyKind kind = EnemyKind.Chaser)
        {
            if (!Running || !NavMesh.SamplePosition(position, out var hit, 6, NavMesh.AllAreas)) return false;
            // Upper ledges can be separate navmesh islands. Do not strand a wave
            // enemy somewhere it cannot navigate from to the player's area.
            var path = new NavMeshPath();
            if (!FindPlayerNavigation(out var destination) ||
                !NavMesh.CalculatePath(hit.position, destination.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete) return false;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            if (elite) kind = EnemyKind.Chaser;
            bool runner = kind == EnemyKind.Runner, spitter = kind == EnemyKind.Spitter, charger = kind == EnemyKind.Charger;
            body.name = elite ? "Canyon Sentinel" : kind.ToString(); body.layer = EnemyLayer; body.transform.SetParent(root);
            body.transform.position = hit.position;
            // The agent origin is at its feet; lift only the visual/collider center.
            var collider = body.GetComponent<CapsuleCollider>(); collider.center = Vector3.up * (elite ? 1.6f : 1);
            if (elite) { collider.height = 3.2f; collider.radius = .8f; }
            if (charger) { collider.center=Vector3.up*1.1f; collider.height=2.2f; collider.radius=.65f; }
            if (runner) { collider.center = Vector3.up*.75f; collider.height = 1.5f; collider.radius = .3f; }
            var mesh = body.GetComponent<MeshFilter>();
            var visual = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(body.transform, false); visual.transform.localPosition = collider.center;
            if (elite) visual.transform.localScale = Vector3.one * 1.6f;
            if (runner) { visual.transform.localScale = new Vector3(.6f,.75f,.6f); visual.transform.localRotation = Quaternion.Euler(18,0,0); }
            if (charger) visual.transform.localScale = new Vector3(1.35f,1.1f,1.35f);
            if (spitter) visual.transform.localScale = new Vector3(1.2f,.85f,1.2f);
            visual.GetComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            visual.GetComponent<Renderer>().sharedMaterial = elite ? eliteMaterial : runner ? runnerMaterial : spitter ? spitterMaterial : charger ? chargerMaterial : enemyMaterial;
            if (spitter)
            {
                var muzzle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                muzzle.name = "Spitter muzzle"; muzzle.layer = 2; muzzle.transform.SetParent(body.transform,false);
                muzzle.transform.localPosition = new Vector3(0,1.4f,.65f); muzzle.transform.localScale = Vector3.one*.5f;
                Destroy(muzzle.GetComponent<Collider>()); muzzle.GetComponent<Renderer>().sharedMaterial = spitterMaterial;
            }
            Destroy(body.GetComponent<Renderer>()); Destroy(mesh);
            var agent = body.AddComponent<NavMeshAgent>();
            agent.baseOffset=0;
            agent.radius = elite ? .8f : .5f; agent.height = elite ? 3.2f : 2; agent.speed = elite ? 5.5f : 4.5f;
            if (runner) { agent.radius = .3f; agent.height = 1.5f; agent.speed = 7.2f; }
            if (spitter) agent.speed = 3.5f;
            if (charger) { agent.radius=.65f; agent.height=2.2f; agent.speed=3.8f; }
            agent.acceleration = 18; agent.angularSpeed = 360; agent.stoppingDistance = charger ? 5 : .8f;
            float minutes=RunMinutes;
            float health=elite ? 2200*(1+.22f*minutes+.035f*minutes*minutes) :
                (runner ? 18 : spitter ? 35 : charger ? 90 : 30)*EnemyHealthScale;
            health*=1+(elite ? .75f : .35f)*ExtraPartyMembers;
            agent.speed*=1+Mathf.Min(.25f,minutes*.02f);
            enemies.Add(new Enemy { body=body, agent=agent, visual=visual.GetComponent<Renderer>(),
                health=health,maxHealth=health,damageScale=EnemyDamageScale,
                overtimeHealth=OvertimeHealthMultiplier,overtimeDamage=OvertimeDamageMultiplier,elite=elite,kind=kind,
                attackClock=charger ? .8f : spitter ? 2.2f : 3 });
            if (runner) RunnersSpawned++;
            if (spitter) SpittersSpawned++;
            return true;
        }
        void TickEnemies(float dt)
        {
            ulong original = contextId;
            foreach (var enemy in enemies)
            {
                ApplyOvertimePressure(enemy);
                if (CoopAuthority) SwitchToNearest(enemy.body.transform.position);
                bool found = FindPlayerNavigation(out var destination);
                if (enemy.flash > 0)
                {
                    enemy.flash -= dt;
                    if (enemy.flash <= 0) enemy.visual.SetPropertyBlock(null);
                }
                if (enemy.elite)
                {
                    TickSentinel(enemy,dt,found,destination.position);
                    continue;
                }
                if (enemy.kind==EnemyKind.Charger)
                {
                    TickCharger(enemy,dt,found,destination.position);
                    continue;
                }
                if (enemy.kind == EnemyKind.Spitter) TickSpitter(enemy,dt,found,destination.position);
                else if (found && enemy.agent.isOnNavMesh)
                { enemy.agent.isStopped = enemy.elite && enemy.attackClock <= 1.1f; enemy.agent.SetDestination(destination.position); }
                if (hurtClock <= 0 && Vector3.Distance(enemy.body.transform.position + Vector3.up, player.transform.position) < 1.65f)
                {
                    Health = Mathf.Max(0, Health - 10*enemy.damageScale); hurtClock = .75f;
                    if (Health <= 0) { Notice = "Overwhelmed. Try another line through the canyon."; ReleaseMouse(); }
                }
            }
            if (CoopAuthority) SwitchContext(original);
            if (Health <= 0 && !CoopAuthority) { Notice = "Overwhelmed. Try another line through the canyon."; ReleaseMouse(); }
        }
        void TickSpitter(Enemy enemy, float dt, bool found, Vector3 destination)
        {
            if (!enemy.agent.isOnNavMesh) return;
            Vector3 origin = enemy.body.transform.position+Vector3.up*1.3f;
            Vector3 delta = player.transform.position-origin;
            bool canShoot = delta.magnitude < 18 && !Physics.Raycast(origin,delta.normalized,delta.magnitude,WorldMask);
            if (!canShoot)
            {
                if (enemy.warning) { Destroy(enemy.warning); enemy.attackClock = 1.4f; }
                enemy.agent.isStopped = !found;
                if (found) enemy.agent.SetDestination(destination);
                return;
            }
            enemy.attackClock -= dt;
            if (enemy.attackClock <= .8f)
            {
                enemy.agent.isStopped = true;
                if (!enemy.warning)
                {
                    enemy.aim = player.transform.position+Vector3.up*.15f;
                    enemy.warning = new GameObject("Spitter aim warning",typeof(LineRenderer));
                    enemy.warning.transform.SetParent(enemy.body.transform);
                    var line = enemy.warning.GetComponent<LineRenderer>();
                    line.positionCount = 2; line.startWidth = line.endWidth = .07f;
                    line.sharedMaterial = spitterMaterial;
                    line.SetPosition(0,origin); line.SetPosition(1,enemy.aim);
                    Vector3 facing = Vector3.ProjectOnPlane(delta,Vector3.up);
                    if (facing.sqrMagnitude > .01f) enemy.body.transform.rotation = Quaternion.LookRotation(facing);
                }
                if (enemy.attackClock <= 0)
                {
                    var shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    shot.name = "Spitter shot"; shot.layer = 2; shot.transform.SetParent(root);
                    shot.transform.position = origin; shot.transform.localScale = Vector3.one*.5f;
                    Destroy(shot.GetComponent<Collider>()); shot.GetComponent<Renderer>().sharedMaterial = spitterMaterial;
                    enemyShots.Add(new EnemyShot { body = shot, velocity = (enemy.aim-origin).normalized*12, damage=12*enemy.damageScale });
                    EnemyShotsFired++; Destroy(enemy.warning); enemy.attackClock = 3.2f;
                }
            }
            else if (delta.magnitude < 7)
            {
                Vector3 away = Vector3.ProjectOnPlane(-delta,Vector3.up).normalized;
                if (NavMesh.SamplePosition(enemy.body.transform.position+away*4,out var retreat,3,NavMesh.AllAreas))
                { enemy.agent.isStopped = false; enemy.agent.SetDestination(retreat.position); }
                else enemy.agent.isStopped = true;
            }
            else
            {
                enemy.agent.isStopped = delta.magnitude < 14;
                if (!enemy.agent.isStopped && found) enemy.agent.SetDestination(destination);
            }
        }
        void TickEnemyShots(float dt)
        {
            for (int i=enemyShots.Count-1; i>=0; i--)
            {
                var shot = enemyShots[i]; shot.life -= dt;
                if (CoopAuthority && HitPartyWithShot(shot,dt)) { Destroy(shot.body); enemyShots.RemoveAt(i); continue; }
                Vector3 step = shot.velocity*dt;
                var hits = Physics.SphereCastAll(shot.body.transform.position,.22f,step.normalized,
                    step.magnitude,WorldMask | (1 << 2),QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits,(a,b) => a.distance.CompareTo(b.distance));
                bool finished = false;
                foreach (var hit in hits)
                {
                    if (hit.collider.gameObject.layer == 0) { finished = true; break; }
                    if (CoopAuthority || hit.collider.gameObject != player.gameObject) continue;
                    finished = true;
                    if (hurtClock <= 0)
                    { Health = Mathf.Max(0,Health-shot.damage); hurtClock = .75f; EnemyProjectileHits++; }
                    break;
                }
                if (finished || shot.life <= 0) { Destroy(shot.body); enemyShots.RemoveAt(i); }
                else shot.body.transform.position += step;
            }
            if (Health <= 0) { Notice = "Overwhelmed. Try another line through the canyon."; ReleaseMouse(); }
        }
        void Fire()
        {
            Vector3 origin = player.transform.position + Vector3.up * .3f;
            Enemy target = null; float best = 26*26;
            foreach (var enemy in enemies)
            {
                Vector3 delta = enemy.body.transform.position + Vector3.up - origin;
                if (delta.sqrMagnitude >= best || Physics.Raycast(origin, delta.normalized, delta.magnitude, WorldMask)) continue;
                best = delta.sqrMagnitude; target = enemy;
            }
            if (target == null) { WeaponStatus = enemies.Count == 0 ? "Waiting for enemies" : "No visible enemy within 26m"; return; }
            WeaponStatus = "Auto-firing";
            Vector3 direction = (target.body.transform.position + Vector3.up - origin).normalized;
            for (int i = 0; i < ProjectileCount; i++)
            {
                // Keep one bolt on target even with an even projectile count.
                float spread = i == 0 ? 0 : ((i+1)/2)*6*(i%2 == 1 ? 1 : -1);
                var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "Auto bolt"; body.layer = 2; body.transform.SetParent(root);
                body.transform.position = origin; body.transform.localScale = Vector3.one*.3f;
                Destroy(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = bulletMaterial;
                var trail = body.AddComponent<TrailRenderer>();
                trail.time = .1f; trail.startWidth = .15f; trail.endWidth = .02f;
                trail.sharedMaterial = bulletMaterial;
                bullets.Add(new Bullet { owner = player, body = body, velocity = Quaternion.AngleAxis(spread,Vector3.up)*direction*42,
                    life = 1.4f, damage = Damage, pierces = PierceRank, bounces = RicochetRank, blast = BlastRank });
                ShotsFired++;
            }
            fireClock = FireInterval;
        }
        void TickBullets(float dt)
        {
            ulong original=contextId;
            for (int i = bullets.Count-1; i >= 0; i--)
            {
                var bullet = bullets[i];
                if (CoopAuthority && bullet.owner && bullet.owner.GetComponent<NetworkAvatar>())
                    SwitchContext(bullet.owner.GetComponent<NetworkAvatar>().OwnerClientId);
                bullet.life -= dt; bullet.age += dt;
                if (bullet.boomerang)
                {
                    if (!bullet.returning && bullet.age >= .65f) BeginReturn(bullet);
                    if (bullet.returning)
                    {
                        Vector3 home = (bullet.owner ? bullet.owner.transform.position : player.transform.position)+Vector3.up*.3f;
                        if (Vector3.Distance(bullet.body.transform.position,home) < .8f)
                        { Destroy(bullet.body); bullets.RemoveAt(i); continue; }
                        SteerReturningBoomerang(bullet,home,dt);
                    }
                    bullet.body.transform.Rotate(0,720*dt,0);
                }
                Vector3 step = bullet.velocity*dt;
                var hits = Physics.SphereCastAll(bullet.body.transform.position, bullet.boomerang ? .2f : .12f, step.normalized,
                    step.magnitude, WorldMask | (1 << EnemyLayer), QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits,(a,b) => a.distance.CompareTo(b.distance));
                bool finished = false, redirected = false;
                foreach (var hit in hits)
                {
                    if (hit.collider.gameObject.layer == 0)
                    {
                        if (bullet.rocket) Explode(bullet.body.transform.position+step.normalized*Mathf.Max(0,hit.distance-.15f),bullet.damage,bullet.blast,null);
                        if (bullet.boomerang && !bullet.returning) { BeginReturn(bullet,true); redirected = true; }
                        else finished = true;
                        break;
                    }
                    Enemy enemy = enemies.Find(e => e.body == hit.collider.gameObject);
                    if (enemy == null || !bullet.hitEnemies.Add(enemy.body.GetInstanceID())) continue;
                    Vector3 impact = bullet.body.transform.position + step.normalized*hit.distance;
                    DirectHits++;
                    if (bullet.hasBounced) RicochetHits++;
                    if (bullet.hasPierced) PierceHits++;
                    WeaponHit(enemy,bullet.damage);
                    Spark(impact,.1f,bullet.rocket ? .55f : .3f,.14f,BulletFeedbackMaterial(bullet));
                    if (bullet.arrow) ArrowImpact(impact,bullet.velocity.normalized);
                    if (bullet.blast > 0) Explode(impact,bullet.damage,bullet.blast,enemy);
                    if (bullet.boomerang) continue;
                    if (bullet.pierces > 0) { bullet.pierces--; bullet.hasPierced = true; continue; }
                    Enemy next = null; float nearest = 10*10;
                    if (bullet.bounces > 0)
                        foreach (var candidate in enemies)
                        {
                            if (bullet.hitEnemies.Contains(candidate.body.GetInstanceID())) continue;
                            Vector3 delta = candidate.body.transform.position + Vector3.up - impact;
                            if (delta.sqrMagnitude >= nearest || Physics.Raycast(impact,delta.normalized,delta.magnitude,WorldMask)) continue;
                            next = candidate; nearest = delta.sqrMagnitude;
                        }
                    if (next == null) { finished = true; break; }
                    bullet.bounces--; bullet.hasBounced = true;
                    bullet.velocity = (next.body.transform.position+Vector3.up-impact).normalized*42;
                    bullet.body.transform.position = impact + bullet.velocity.normalized*.03f;
                    bullet.life = Mathf.Max(bullet.life,.4f);
                    redirected = true; break;
                }
                if (finished || bullet.life <= 0) { Destroy(bullet.body); bullets.RemoveAt(i); }
                else if (!redirected) bullet.body.transform.position += step;
            }
            if (CoopAuthority) SwitchContext(original);
        }
        void Hit(Enemy enemy, float damage)
        {
            int index = enemies.IndexOf(enemy);
            if (index < 0) return;
            enemy.health -= damage; enemy.flash = .09f;
            flashProperties.SetColor("_Color",Color.white); enemy.visual.SetPropertyBlock(flashProperties);
            if (enemy.health <= 0) Kill(index);
        }
        void Explode(Vector3 position, float damage, int rank, Enemy primary)
        {
            float radius = 2.5f + .6f*(rank-1);
            Ring(position,radius);
            Spark(position,.22f,.9f,.18f,blastMaterial);
            // Secondary damage never triggers more explosions or ricochets.
            foreach (var enemy in enemies.ToArray())
            {
                if (enemy == primary) continue;
                Vector3 delta = enemy.body.transform.position+Vector3.up-position;
                if (delta.sqrMagnitude > radius*radius || Physics.Raycast(position,delta.normalized,delta.magnitude,WorldMask)) continue;
                ExplosionHits++; Hit(enemy,damage*.5f);
            }
        }
        void Spark(Vector3 position, float from, float to, float duration, Material material)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Impact burst"; body.layer = 2; body.transform.SetParent(root);
            body.transform.position = position; body.transform.localScale = Vector3.one*from;
            Destroy(body.GetComponent<Collider>()); body.GetComponent<Renderer>().sharedMaterial = material;
            AddEffect(body,from,to,duration);
        }
        void Ring(Vector3 position, float radius, Material material = null)
        {
            var body = new GameObject("Explosion ring",typeof(LineRenderer));
            body.transform.SetParent(root); body.transform.position = position;
            var line = body.GetComponent<LineRenderer>();
            line.useWorldSpace = false; line.loop = true; line.positionCount = 40;
            line.startWidth = line.endWidth = .1f; line.sharedMaterial = material ? material : blastMaterial;
            for (int i=0; i<40; i++)
            { float angle = i*Mathf.PI*2/40; line.SetPosition(i,new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius); }
            body.transform.localScale = Vector3.one;
            AddEffect(body,1,1.025f,.32f);
        }
        void AddEffect(GameObject body, float from, float to, float duration)
        {
            if (effects.Count >= 96) { Destroy(effects[0].body); effects.RemoveAt(0); }
            effects.Add(new Effect { body = body, from = from, to = to, duration = duration, remaining = duration });
        }
        void TickEffects(float dt)
        {
            for (int i=effects.Count-1; i>=0; i--)
            {
                var effect = effects[i]; effect.remaining -= dt;
                if (effect.remaining <= 0) { Destroy(effect.body); effects.RemoveAt(i); }
                else effect.body.transform.localScale = Vector3.one*Mathf.Lerp(effect.from,effect.to,1-effect.remaining/effect.duration);
            }
        }
        void Kill(int index)
        {
            var enemy = enemies[index];
            Vector3 position = enemy.body.transform.position + Vector3.up*.5f;
            Spark(enemy.body.transform.position+Vector3.up,.4f,1.3f,.2f,enemy.elite ? blastMaterial : sparkMaterial);
            enemy.body.SetActive(false); Destroy(enemy.body); enemies.RemoveAt(index); Kills++; SharedMoney+=enemy.elite ? 40 : 5;
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "XP +3"; body.layer = 2; body.transform.SetParent(root);
            body.transform.position = position; body.transform.localScale = Vector3.one*.4f;
            Destroy(body.GetComponent<Collider>()); body.GetComponent<Renderer>().sharedMaterial = xpMaterial;
            orbs.Add(new Orb { body = body, value = enemy.elite ? 12 : 3 });
            if (enemy == consoleSentinel)
            {
                Won = true;
                Notice = "Canyon cleared! Your build survived the Sentinel.";
                ReleaseMouse();
            }
        }
        void TickOrbs(float dt)
        {
            for (int i = orbs.Count-1; i >= 0; i--)
            {
                var orb = orbs[i];
                var collector = CoopAuthority ? NearestLiving(orb.body.transform.position) : player;
                if (!collector) continue;
                Vector3 delta = collector.transform.position - orb.body.transform.position;
                // Walls block attraction and collection; collectibles stay on reachable ground.
                if (Physics.Raycast(orb.body.transform.position, delta.normalized, delta.magnitude, WorldMask)) continue;
                if (delta.magnitude < 2)
                {
                    if (CoopAuthority) GivePartyXP(orb.value);
                    else { xp += orb.value; TotalXP += orb.value; }
                    Destroy(orb.body); orbs.RemoveAt(i);
                    if (!CoopAuthority && xp >= RequiredXP) { OpenUpgrade(); return; }
                }
                else if (delta.magnitude < 5)
                    orb.body.transform.position = Vector3.MoveTowards(orb.body.transform.position, collector.transform.position, 14*dt);
            }
        }
        public void ChooseUpgrade(int choice)
        {
            if (IsCoopClient) { SendCombatCommand("choose",choice); return; }
            if (!ChoosingUpgrade || choice < 0 || choice >= choices.Count) return;
            ApplyTieredUpgrade(choices[choice],ChoiceRarity(choice));
            Health = Mathf.Min(100, Health + 20); UpgradeCount++;
            FinishUpgrade();
        }
        void FinishUpgrade()
        {
            ChoosingUpgrade = false; choices.Clear(); choiceRarities.Clear();
            BonusUpgrade = false;
            if (xp >= RequiredXP) OpenUpgrade();
            else if (bonusPending>0) OpenBonusUpgrade();
            else CaptureMouse();
        }
        void ApplyUpgrade(BoltUpgrade upgrade)
        {
            if (ApplyLoadoutUpgrade(upgrade)) return;
            switch (upgrade)
            {
                case BoltUpgrade.HeavyBolts: Damage *= 1.35f; break;
                case BoltUpgrade.RapidFire: FireInterval = Mathf.Max(.08f,FireInterval/1.25f); break;
                case BoltUpgrade.MoreBolts: ProjectileCount = Mathf.Min(6,ProjectileCount+1); break;
                case BoltUpgrade.Ricochet: RicochetRank = Mathf.Min(3,RicochetRank+1); break;
                case BoltUpgrade.Piercing: PierceRank = Mathf.Min(3,PierceRank+1); break;
                case BoltUpgrade.ExplosiveHits: BlastRank = Mathf.Min(3,BlastRank+1); break;
            }
        }
        void OpenUpgrade()
        {
            xp -= RequiredXP; Level++; ChoosingUpgrade = true; BonusUpgrade = false; currentBonusTier=-1;
            RollUpgradeChoices();
        }
        void RollUpgradeChoices()
        {
            var previous = new List<BoltUpgrade>(choices);
            choiceVersion++;
            choices.Clear(); choiceRarities.Clear();
            var pool = new List<BoltUpgrade> { BoltUpgrade.HeavyBolts };
            if (FireInterval > .08f) pool.Add(BoltUpgrade.RapidFire);
            if (ProjectileCount < 6) pool.Add(BoltUpgrade.MoreBolts);
            if (RicochetRank < 3) pool.Add(BoltUpgrade.Ricochet);
            if (PierceRank < 3) pool.Add(BoltUpgrade.Piercing);
            if (BlastRank < 3) pool.Add(BoltUpgrade.ExplosiveHits);
            AddLoadoutOffers(pool);
            // Prefer a different set on reroll when the pool has enough alternatives.
            var alternatives = pool.FindAll(option => !previous.Contains(option));
            if (alternatives.Count >= 3) pool = alternatives;
            while (choices.Count < 3 && pool.Count > 0)
            {
                int index = Random.Range(0,pool.Count); choices.Add(pool[index]); choiceRarities.Add(BonusUpgrade && currentBonusTier>=0 ? (UpgradeRarity)currentBonusTier : RollRarity()); pool.RemoveAt(index);
            }
            ReleaseMouse();
        }
        string UpgradeDescription(BoltUpgrade upgrade)
        {
            string loadout = LoadoutDescription(upgrade);
            if (loadout != null) return loadout;
            switch (upgrade)
            {
                case BoltUpgrade.HeavyBolts: return $"Heavy bolts â€” +35% damage ({Damage:0} â†’ {Damage*1.35f:0})";
                case BoltUpgrade.RapidFire: return "Rapid fire â€” +25% firing rate";
                case BoltUpgrade.MoreBolts: return $"More bolts â€” {ProjectileCount+1} projectiles per shot";
                case BoltUpgrade.Ricochet: return $"Ricochet {RicochetRank+1}/3\nBolts jump to {RicochetRank+1} extra targets within 10m.";
                case BoltUpgrade.Piercing: return $"Piercing {PierceRank+1}/3\nPass through {PierceRank+1} extra enemies, then ricochet if available.";
                default: return $"Explosive hits {BlastRank+1}/3\nEach hit blasts nearby enemies for 50% damage. Radius {2.5f+.6f*BlastRank:0.0}m.";
            }
        }
        void CaptureMouse() { if (NetworkActive && contextId != NetworkManager.Singleton.LocalClientId) return; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        void ReleaseMouse() { if (NetworkActive && contextId != NetworkManager.Singleton.LocalClientId) return; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        void OnGUI()
        {
            GUI.depth=-100;
            DrawCombatGui();
        }
        void DrawCombatGui()
        {
            if (!Running) { DrawPracticeCombatMenu(); return; }
            // Draw the core combat HUD before optional world labels.
            DrawCombatHud();
            if (Camera.main && player)
            {
                int nearby=CacheState==CacheChallengeState.Active ? cacheIndex : NearestCacheIndex();
                Vector3 anchor=CanyonLayout.CachePositions[nearby]+Vector3.up*2;
                Vector3 position=Camera.main.WorldToScreenPoint(anchor);
                var labelRect=new Rect(position.x-130,Screen.height-position.y,260,32);
                if (position.z>0 && Vector3.Distance(player.transform.position,anchor)<35 &&
                    !WorldLabelBlocked(labelRect) && !Physics.Linecast(Camera.main.transform.position,anchor,WorldMask))
                    GUI.Label(labelRect,cacheStates[nearby]==CacheChallengeState.Available ? CanyonLayout.CacheNames[nearby]+" / E when near" : CacheStatus());
            }
            if (IsCoopClient) DrawSharedLabels();
            foreach (var enemy in enemies)
            {
                if ((!ShowEnemyLabels && !enemy.elite && !(CacheState == CacheChallengeState.Active && cacheGuards.Contains(enemy))) || !Camera.main) continue;
                Vector3 anchor = enemy.body.transform.position + Vector3.up*(enemy.elite ? 3.8f : enemy.kind == EnemyKind.Runner ? 1.9f : 2.5f);
                Vector3 label = Camera.main.WorldToScreenPoint(anchor);
                if (label.z <= 0 || label.x < 0 || label.x > Screen.width || label.y < 0 || label.y > Screen.height ||
                    Vector3.Distance(Camera.main.transform.position,anchor) > 35 || Physics.Linecast(Camera.main.transform.position,anchor,WorldMask)) continue;
                if (enemyLabel == null) enemyLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
                string title = enemy.elite ? $"SENTINEL {enemy.health:0}/{enemy.maxHealth:0}" : enemy.kind.ToString().ToUpperInvariant();
                if (enemy.elite) title += " / "+SentinelMoves[enemy.sentinelMove];
                if (enemy.kind==EnemyKind.Charger)
                    title += enemy.chargePhase==ChargePhase.WindingUp ? " / WIND-UP" :
                        enemy.chargePhase==ChargePhase.Rushing ? " / CHARGE" :
                        enemy.chargePhase==ChargePhase.Recovering ? " / STUNNED" : "";
                if (enemy.kind == EnemyKind.Spitter && enemy.warning) title += " / CHARGING";
                if (CacheState == CacheChallengeState.Active && cacheGuards.Contains(enemy)) title += " / CACHE GUARD";
                var rect = new Rect(label.x-110,Screen.height-label.y,220,25);
                enemyLabel.normal.textColor = Color.black; GUI.Label(new Rect(rect.x+1,rect.y+1,rect.width,rect.height),title,enemyLabel);
                enemyLabel.normal.textColor = Color.white; GUI.Label(rect,title,enemyLabel);
            }
            DrawSupplyLabels();
        }
        void OnDestroy()
        {
            DisposePersistentHud();
            DisposeHud();
            DisposeWeaponPresentation();
            DisposeCoop();
            if (navInstance.valid) navInstance.Remove();
            if (navData) Destroy(navData);
            DestroySupplyMaterials();
            Destroy(enemyMaterial); Destroy(bulletMaterial); Destroy(xpMaterial);
            Destroy(eliteMaterial);
            Destroy(sparkMaterial); Destroy(blastMaterial);
            Destroy(runnerMaterial); Destroy(spitterMaterial); Destroy(chargerMaterial);
            foreach (var material in weaponMaterials.Values) Destroy(material);
            if (Instance == this) Instance = null;
        }
    }
}
