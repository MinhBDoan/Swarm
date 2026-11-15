using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
namespace Swarm
{
    public partial class CombatPlayground
    {
        static readonly Color HudPanel=new Color(.035f,.055f,.085f,.96f);
        static readonly Color HudMuted=new Color(.66f,.73f,.8f);
        static readonly Color HudCyan=new Color(.25f,.85f,1);
        Texture2D hudRing;
        GUIStyle hudTitle,hudText,hudSmall,hudNumber,hudButton,hudCardTitle;
        public static float HudScaleFor(int width,int height) => Mathf.Min(width/1600f,height/640f);
        void EnsureHud()
        {
            if (hudTitle!=null) return;
            hudTitle=HudStyle(23,FontStyle.Bold); hudText=HudStyle(19); hudSmall=HudStyle(16);
            hudNumber=HudStyle(32,FontStyle.Bold); hudCardTitle=HudStyle(26,FontStyle.Bold);
            hudButton=new GUIStyle(GUI.skin.button) { fontSize=19,fontStyle=FontStyle.Bold,wordWrap=true,padding=new RectOffset(12,12,8,8) };
            hudRing=new Texture2D(64,64,TextureFormat.RGBA32,false);
            var pixels=new Color[64*64];
            for (int y=0;y<64;y++) for (int x=0;x<64;x++)
            {
                float distance=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f));
                pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(2-Mathf.Abs(distance-27)));
            }
            hudRing.SetPixels(pixels); hudRing.Apply();
        }
        GUIStyle HudStyle(int size,FontStyle weight=FontStyle.Normal)
        {
            var style=new GUIStyle(GUI.skin.label) { fontSize=size,fontStyle=weight,wordWrap=true,padding=new RectOffset(0,0,0,0) };
            style.normal.textColor=Color.white; return style;
        }
        void HudFill(Rect rect,Color color)
        { Color old=GUI.color; GUI.color=color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=old; }
        void HudLabel(Rect rect,string text,GUIStyle style,Color? color=null)
        { Color old=style.normal.textColor; style.normal.textColor=color ?? Color.white; GUI.Label(rect,text,style); style.normal.textColor=old; }
        void HudPanelBox(Rect rect,Color accent)
        { HudFill(rect,HudPanel); HudFill(new Rect(rect.x,rect.y,rect.width,3),accent); }
        void HudBar(Rect rect,float amount,Color color)
        { HudFill(rect,new Color(.12f,.16f,.2f)); HudFill(new Rect(rect.x,rect.y,rect.width*Mathf.Clamp01(amount),rect.height),color); }
        Color WeaponColor(BoltUpgrade weapon)
        {
            switch (weapon)
            {
                case BoltUpgrade.Boomerang:return new Color(.2f,.9f,1);
                case BoltUpgrade.PulseField:return new Color(.35f,1,.55f);
                case BoltUpgrade.RocketLauncher:return new Color(1,.5f,.2f);
                case BoltUpgrade.PiercingBow:return new Color(.8f,.6f,1);
                default:return new Color(1,.85f,.3f);
            }
        }
        void HudStroke(Vector2 start,Vector2 end,float width,Color color)
        {
            Matrix4x4 matrix=GUI.matrix;
            float angle=Mathf.Atan2(end.y-start.y,end.x-start.x)*Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle,start);
            HudFill(new Rect(start.x,start.y-width/2,Vector2.Distance(start,end),width),color);
            GUI.matrix=matrix;
        }
        void HudIcon(Rect rect,BoltUpgrade weapon,Color color)
        {
            Vector2 center=rect.center;
            float radius=rect.width*.4f;
            if (weapon==BoltUpgrade.PulseField)
            {
                Color old=GUI.color; GUI.color=color; GUI.DrawTexture(rect,hudRing); GUI.color=old;
                HudFill(new Rect(center.x-3,center.y-3,6,6),color);
            }
            else if (weapon==BoltUpgrade.Boomerang)
            {
                HudStroke(center+new Vector2(-radius,-radius*.4f),center+new Vector2(0,radius*.4f),5,color);
                HudStroke(center+new Vector2(0,radius*.4f),center+new Vector2(radius,-radius*.4f),5,color);
            }
            else if (weapon==BoltUpgrade.PiercingBow)
            {
                HudStroke(center+new Vector2(-radius,0),center+new Vector2(radius,0),3,color);
                HudStroke(center+new Vector2(radius,0),center+new Vector2(radius*.45f,-radius*.45f),3,color);
                HudStroke(center+new Vector2(radius,0),center+new Vector2(radius*.45f,radius*.45f),3,color);
                HudStroke(center+new Vector2(-radius*.6f,-radius),center+new Vector2(0,0),2,color);
                HudStroke(center+new Vector2(0,0),center+new Vector2(-radius*.6f,radius),2,color);
            }
            else if (weapon==BoltUpgrade.RocketLauncher)
            {
                HudFill(new Rect(center.x-radius*.65f,center.y-5,radius*1.2f,10),color);
                HudStroke(center+new Vector2(radius*.55f,0),center+new Vector2(radius,-5),4,color);
                HudStroke(center+new Vector2(-radius*.9f,-6),center+new Vector2(-radius*.9f,6),3,color);
            }
            else
            {
                HudFill(new Rect(center.x-radius,center.y-6,radius*1.6f,8),color);
                HudFill(new Rect(center.x-radius*.5f,center.y,6,radius*.7f),color);
                HudFill(new Rect(center.x+radius*.8f,center.y-4,5,4),color);
            }
        }
        float WeaponCooldown(BoltUpgrade weapon,int rank)
        { return weapon==BoltUpgrade.PulseField ? 1.4f : weapon==BoltUpgrade.RocketLauncher ? 2.8f : weapon==BoltUpgrade.Boomerang ? 2.4f-rank*.15f : 1.8f; }
        void DrawCombatHud()
        {
            EnsureHud();
            Matrix4x4 old=GUI.matrix;
            Color oldColor=GUI.color, oldContentColor=GUI.contentColor;
            bool oldEnabled=GUI.enabled;
            float scale=HudScaleFor(Screen.width,Screen.height);
            if (scale<=0) return;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            GUI.color=GUI.contentColor=Color.white;
            GUI.enabled=true;
            try
            {
                float width=Screen.width/scale,height=Screen.height/scale;
                if (!PersistentHudVisible)
                {
                    DrawWaveHeader(width);
                    DrawHealthPanel(height);
                    DrawWeaponSlots(width,height);
                }
                if (player && player.RecentSlideHop) HudLabel(new Rect(width/2-180,height-220,360,28),$"SLIDE HOP  +{player.SlideHopBonus:0.0} m/s",hudText,HudCyan);
                DrawReadableParty();
                if (!ChoosingUpgrade && !Won && !PartyDefeated && Health>0) DrawObjectivePanel(width);
                HudLabel(new Rect(width-282,height-42,260,25),"F8  Switch HUD    /    Esc  Menu",hudSmall,HudMuted);
                if (ChoosingUpgrade) DrawUpgradeCards(width,height);
                else if (Won || Health<=0 && (!NetworkActive || PartyDefeated)) DrawRunResult(width,height);
                else if (Health<=0) DrawCenteredStatus(width,height,"YOU ARE DOWN","Your team is still fighting. Return with 50 HP at the next wave break.",new Color(1,.5f,.4f));
                else if (PartyPaused) DrawCenteredStatus(width,height,"WAITING FOR TEAMMATES","Combat is paused while your party finishes choosing upgrades.",HudCyan);
                else if (Cursor.lockState!=CursorLockMode.Locked && !Application.isBatchMode) DrawPauseMenu(width,height);
                if (NetworkActive && Cursor.lockState!=CursorLockMode.Locked)
                {
                    float menuY=ChoosingUpgrade ? 22 : ShowDownloadObjective ? 230 : 184;
                    if (GUI.Button(new Rect(width-330,menuY,308,44),"LEAVE PARTY",hudButton)) FindObjectOfType<MultiplayerSession>().Leave();
                    if (NetworkManager.Singleton.IsServer && GUI.Button(new Rect(width-330,menuY+54,308,44),"STOP RUN",hudButton)) { StopCombat(); ReleaseMouse(); }
                }
            }
            finally
            {
                GUI.matrix=old;
                GUI.color=oldColor;
                GUI.contentColor=oldContentColor;
                GUI.enabled=oldEnabled;
            }
        }
        public static bool HudConsumesPointer
        {
            get
            {
                if (!Instance || !Instance.Running || Cursor.lockState==CursorLockMode.Locked) return false;
                float scale=HudScaleFor(Screen.width,Screen.height);
                Vector2 pointer=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y)/scale;
                float width=Screen.width/scale,height=Screen.height/scale;
                float menuY=Instance.ChoosingUpgrade ? 22 : Instance.ShowDownloadObjective ? 230 : 184;
                return new Rect(width/2-350,height/2-100,700,265).Contains(pointer) || new Rect(width-330,menuY,308,110).Contains(pointer);
            }
        }
        public static bool WorldLabelBlocked(Rect rect)
        {
            if (!Instance || !Instance.Running) return false;
            if (BlocksInput || Cursor.lockState!=CursorLockMode.Locked) return true;
            float scale=HudScaleFor(Screen.width,Screen.height),width=Screen.width/scale,height=Screen.height/scale;
            var logical=new Rect(rect.x/scale,rect.y/scale,rect.width/scale,rect.height/scale);
            return logical.Overlaps(new Rect(22,height-203,330,181)) || logical.Overlaps(new Rect(width/2-402,height-184,804,184)) ||
                logical.Overlaps(new Rect(width/2-230,22,460,122)) || logical.Overlaps(new Rect(width-330,22,308,Instance && Instance.ShowDownloadObjective ? 190 : 143)) ||
                Instance.NetworkActive && logical.Overlaps(new Rect(22,22,254,300));
        }
        string CombatWaveTitle() => Won ? "CANYON CLEARED" : Resting ? "WAVE CLEARED" :
            bossPhase==BossPhase.Fighting ? "CANYON SENTINEL" : $"SWARM {Wave}";

        string CombatWaveDetail() => Resting ?
            $"Next encounter in {Mathf.CeilToInt(restClock)}s  /  Collect XP and regroup" :
            $"{EnemyCount} enemies alive    /    {Kills} defeated    /    {Mathf.FloorToInt(elapsed/60):00}:{Mathf.FloorToInt(elapsed%60):00}";

        void DrawWaveHeader(float width)
        {
            var rect=new Rect(width/2-230,22,460,122); HudPanelBox(rect,HudCyan);
            HudLabel(new Rect(rect.x+18,rect.y+12,425,30),CombatWaveTitle(),hudTitle);
            HudLabel(new Rect(rect.x+18,rect.y+49,425,25),CombatWaveDetail(),hudSmall,HudMuted);
            HudLabel(new Rect(rect.x+18,rect.y+79,425,32),$"PARTY MONEY  {SharedMoney:N0}",hudTitle,new Color(1,.8f,.3f));
        }
        void DrawHealthPanel(float height)
        {


            var rect=new Rect(22,height-203,330,181); HudPanelBox(rect,Health>30 ? new Color(.35f,1,.55f) : new Color(1,.35f,.3f));
            HudLabel(new Rect(rect.x+16,rect.y+13,200,39),$"{Mathf.CeilToInt(Health)}  HP",hudNumber);
            HudLabel(new Rect(rect.x+226,rect.y+25,90,26),$"LEVEL {Level}",hudSmall,HudCyan);
            HudBar(new Rect(rect.x+16,rect.y+58,298,16),Health/100,Health>30 ? new Color(.35f,.9f,.5f) : new Color(1,.35f,.3f));
            HudLabel(new Rect(rect.x+16,rect.y+83,298,24),$"{(NetworkActive ? "Shared XP" : "XP")}  {xp} / {RequiredXP}",hudSmall,HudMuted);
            HudBar(new Rect(rect.x+16,rect.y+112,298,8),xp/(float)RequiredXP,HudCyan);
            float speed=player ? Vector3.ProjectOnPlane(player.Velocity,Vector3.up).magnitude : 0;
            HudLabel(new Rect(rect.x+16,rect.y+135,298,25),$"{speed:0.0} m/s    /    Power x{powerMultiplier:0.00}    /    Regen {RecoveryPerSecond:0.0}",hudSmall,HudMuted);
        }
        void DrawWeaponSlots(float width,float height)
        {
            float start=width/2-402,y=height-150;
            var equipped=new List<BoltUpgrade> { BoltUpgrade.HeavyBolts };
            foreach (var entry in weapons) equipped.Add(entry.Key);
            HudLabel(new Rect(start,y-34,804,26),$"YOUR WEAPONS   {EquippedWeaponCount} / 4",hudSmall,HudMuted);
            for (int i=0;i<4;i++)
            {
                var rect=new Rect(start+i*204,y,192,100);
                bool filled=i<equipped.Count;
                Color color=filled ? WeaponColor(equipped[i]) : new Color(.25f,.31f,.37f);
                HudPanelBox(rect,color);
                HudLabel(new Rect(rect.x+12,rect.y+12,24,22),(i+1).ToString("00"),hudSmall,HudMuted);
                if (!filled)
                {
                    HudLabel(new Rect(rect.x+12,rect.y+39,168,24),"Empty slot",hudText,HudMuted);
                    HudLabel(new Rect(rect.x+12,rect.y+71,168,20),"Find a new weapon",hudSmall,HudMuted); continue;
                }
                var weapon=equipped[i]; bool gun=i==0; int rank=gun ? 1 : WeaponRank(weapon);
                HudIcon(new Rect(rect.x+142,rect.y+12,36,36),weapon,color);
                HudLabel(new Rect(rect.x+12,rect.y+36,175,29),gun ? "Auto gun" : WeaponName(weapon),hudText);
                float clock=gun ? fireClock : weaponClocks.TryGetValue(weapon,out float cooldown) ? cooldown : 0;
                float duration=gun ? FireInterval : WeaponCooldown(weapon,rank);
                HudLabel(new Rect(rect.x+12,rect.y+67,168,22),gun ? $"{Damage:0} dmg / {ProjectileCount} bolts" : $"Level {rank}/5  /  "+(clock<=0 ? "Ready" : $"{clock:0.0}s"),hudSmall,HudMuted);
                HudBar(new Rect(rect.x,rect.y+96,rect.width,4),1-Mathf.Clamp01(clock/duration),color);
            }
            HudLabel(new Rect(start,height-34,804,23),$"Gun: Ricochet {RicochetRank} / Pierce {PierceRank} / Blast {BlastRank}"+(criticalChance>0 ? $" / Crit {criticalChance*100:0}%" : "")+(shockwaveRank>0 ? $" / Shockwave {shockwaveCharge}/{Mathf.Max(3,7-shockwaveRank)}" : ""),hudSmall,HudMuted);
        }
        void DrawReadableParty()
        {
            if (!NetworkActive) return;
            var roster=new List<PartyMemberInfo>();
            if (IsCoopClient) roster.AddRange(remoteRoster);
            else foreach (var state in party.Values) roster.Add(new PartyMemberInfo { id=state.id,health=state.health,choosing=state.choosing });
            roster.Sort((a,b)=>a.id.CompareTo(b.id));
            var rect=new Rect(22,22,254,46+roster.Count*40); HudPanelBox(rect,HudCyan);
            HudLabel(new Rect(38,34,222,26),"PARTY",hudSmall,HudCyan);
            for (int i=0;i<roster.Count;i++)
            {
                var state=roster[i]; float y=68+i*40;
                HudLabel(new Rect(38,y,222,24),$"Player {state.id+1}  /  "+(state.health<=0 ? "DOWN" : state.choosing ? "CHOOSING" : $"{state.health:0} HP"),hudSmall,state.health<=0 ? new Color(1,.5f,.4f) : Color.white);
                HudBar(new Rect(38,y+26,222,4),state.health/100,HudCyan);
            }
        }
        void DrawObjectivePanel(float width)
        {
            if (ShowBossObjective) { DrawBossObjective(width); return; }
            if(ShowSupplyObjective){DrawSupplyObjective(width);return;}
            if (ShowDownloadObjective)
            {
                var downloadRect=new Rect(width-330,22,308,190); var tier=(UpgradeRarity)DownloadViewIndex; var tierColor=RarityColor(tier); HudPanelBox(downloadRect,tierColor);
                HudLabel(new Rect(downloadRect.x+16,35,276,26),$"{tier.ToString().ToUpperInvariant()} DOWNLOAD / {CanyonLayout.DownloadSeconds[DownloadViewIndex]:0}s",hudSmall,tierColor);
                HudLabel(new Rect(downloadRect.x+16,72,276,55),DownloadStatus(),hudText);
                HudBar(new Rect(downloadRect.x+16,137,276,12),downloadPercents[DownloadViewIndex]/100,tierColor);
                HudLabel(new Rect(downloadRect.x+16,165,276,36),$"100%: {tier} choice for everyone",hudSmall,HudMuted);
                return;
            }
            var rect=new Rect(width-330,22,308,143); HudPanelBox(rect,new Color(1,.78f,.3f));
            if (player && CanyonLayout.InCentralFlats(player.transform.position))
            {
                HudLabel(new Rect(rect.x+16,35,276,26),"CENTRAL FLATS",hudSmall,HudCyan);
                bool showCache=CacheState==CacheChallengeState.Active ||
                    Vector3.Distance(player.transform.position,CanyonLayout.CachePositions[NearestCacheIndex()])<22;
                string detail=showCache ? CacheStatus() : "North: Drill Station\nWest: Pumping Yard\nSouth: Mining Basin";
                HudLabel(new Rect(rect.x+16,70,276,72),detail,hudSmall);
                return;
            }
            HudLabel(new Rect(rect.x+16,rect.y+13,276,26),DrillHazard.Instance && player && DrillHazard.Instance.Near(player.transform.position) ? "DRILL STATION" : player && player.transform.position.z>76 ? "CANYON PASSAGE" : "MINING BASIN",hudSmall,new Color(1,.78f,.3f));
            HudLabel(new Rect(rect.x+16,rect.y+48,276,53),CacheState==CacheChallengeState.Active || player && Vector3.Distance(player.transform.position,CanyonLayout.CachePositions[NearestCacheIndex()])<3.5f ? CacheStatus() : RouteEncounterBusy ? RouteEncounterStatus() : player && player.transform.position.z>76 && player.transform.position.z<615 ? RouteEncounterStatus() : DrillHazard.Instance && player && DrillHazard.Instance.Near(player.transform.position) ? DrillHazard.Instance.Status : CacheStatus(),hudText);
            HudLabel(new Rect(rect.x+16,rect.y+111,276,24),NetworkActive ? "Shared XP / Individual builds" : "Automatic aim and fire",hudSmall,HudMuted);
        }
        string CardTitle(BoltUpgrade upgrade)
        {
            switch (upgrade)
            {
                case BoltUpgrade.HeavyBolts:return "Heavy bolts";
                case BoltUpgrade.RapidFire:return "Rapid fire";
                case BoltUpgrade.MoreBolts:return "More bolts";
                case BoltUpgrade.Ricochet:return "Ricochet";
                case BoltUpgrade.Piercing:return "Piercing bolts";
                case BoltUpgrade.ExplosiveHits:return "Explosive hits";
                case BoltUpgrade.Power:return "Power";
                case BoltUpgrade.Recovery:return "Recovery";
                default:return WeaponName(upgrade);
            }
        }
        bool IsExtraWeapon(BoltUpgrade upgrade) => System.Array.IndexOf(ExtraWeapons,upgrade)>=0;
        string CardDetail(BoltUpgrade upgrade)
        {
            switch (upgrade)
            {
                case BoltUpgrade.HeavyBolts:return $"Gun damage +35%\n{Damage:0} to {Damage*1.35f:0} damage per bolt.";
                case BoltUpgrade.RapidFire:return "Gun fires 25% faster.\nMore bolts in the air, more often.";
                case BoltUpgrade.MoreBolts:return $"Fire {ProjectileCount+1} bolts per volley.\nKeeps a center shot on target.";
                case BoltUpgrade.Ricochet:return $"Rank {RicochetRank+1}/3\nBolts jump to extra targets within 10m after piercing.";
                case BoltUpgrade.Piercing:return $"Rank {PierceRank+1}/3\nBolts pass through extra enemies before ricocheting.";
                case BoltUpgrade.ExplosiveHits:return $"Rank {BlastRank+1}/3\nBolts splash nearby enemies for 50% damage.";
                case BoltUpgrade.Power:return "All weapon damage +15%.\nUses no weapon slot.";
                case BoltUpgrade.Recovery:return $"Regenerate {(recoveryRank+1)*.6f:0.0} HP per second.\nRank {recoveryRank+1}/5. Uses no weapon slot.";
                case BoltUpgrade.Boomerang:return "Move to steer the return.\nHits once each way.\nRanks add damage and throw rate.";
                case BoltUpgrade.PulseField:return "Damage pulses around you.\nSweep close to enemy packs.\nRanks add damage and range.";
                case BoltUpgrade.RocketLauncher:return "Rockets burst on impact.\nCluster packs for splash.\nRanks add damage and radius.";
                default:return "Strong, piercing arrows.\nLine up packs for heavy hits.\nRanks add damage and piercing.";
            }
        }
        void DrawUpgradeCards(float width,float height)
        {
            HudFill(new Rect(0,0,width,height),new Color(0,0,0,.79f));
            HudLabel(new Rect(width-290,height-66,268,24),"F8  Switch combat HUD",hudSmall,HudMuted);
            var panel=new Rect(width/2-510,height/2-282,1020,564); HudPanelBox(panel,HudCyan);
            HudLabel(new Rect(panel.x+28,panel.y+24,964,40),BonusUpgrade ? "BONUS UPGRADE" : $"LEVEL {Level}  /  CHOOSE AN UPGRADE",hudNumber);
            HudLabel(new Rect(panel.x+28,panel.y+76,964,30),"Pick with 1 / 2 / 3 or click. Every pick heals 20 HP. Four permanent weapon slots.",hudText,HudMuted);
            for (int i=0;i<choices.Count;i++)
            {
                var choice=choices[i]; bool extra=IsExtraWeapon(choice),passive=choice==BoltUpgrade.Power || choice==BoltUpgrade.Recovery;
                var rarity=ChoiceRarity(i); Color color=RarityColor(rarity);
                var card=new Rect(panel.x+28+i*326,panel.y+126,312,326); HudPanelBox(card,color);
                string category=extra ? WeaponRank(choice)==0 ? "NEW WEAPON" : $"WEAPON  /  RANK {WeaponRank(choice)+1}/5" : passive ? "PASSIVE  /  NO SLOT" : "AUTO GUN UPGRADE";
                HudLabel(new Rect(card.x+18,card.y+18,276,26),rarity.ToString().ToUpperInvariant(),hudSmall,color);
                HudLabel(new Rect(card.x+18,card.y+44,276,24),category,hudSmall,HudMuted);
                HudLabel(new Rect(card.x+18,card.y+73,276,44),CardTitle(choice),hudCardTitle);
                HudLabel(new Rect(card.x+18,card.y+124,276,130),TieredDetail(choice,rarity),hudSmall,HudMuted);
                string action=extra && WeaponRank(choice)==0 ? $"EQUIP  /  SLOT {EquippedWeaponCount+1} OF 4" : "UPGRADE";
                if (GUI.Button(new Rect(card.x+16,card.y+260,280,48),$"{i+1}   {action}",hudButton)) { ChooseUpgrade(i); break; }
            }
            GUI.enabled=RerollsRemaining>0;
            if (GUI.Button(new Rect(panel.x+28,panel.y+479,310,48),$"T   REROLL  /  {RerollsRemaining} LEFT",hudButton)) RerollUpgrades();
            GUI.enabled=true;
            if (GUI.Button(new Rect(panel.x+682,panel.y+479,310,48),"X   SKIP THIS REWARD",hudButton)) SkipUpgrade();
            HudLabel(new Rect(panel.x+354,panel.y+486,306,48),"Skipping forfeits this upgrade.\nCombat resumes after all choices.",hudSmall,HudMuted);
        }
        void DrawCenteredStatus(float width,float height,string title,string detail,Color color)
        {
            var rect=new Rect(width/2-330,height/2-83,660,166); HudPanelBox(rect,color);
            HudLabel(new Rect(rect.x+24,rect.y+26,612,45),title,hudCardTitle);
            HudLabel(new Rect(rect.x+24,rect.y+87,612,60),detail,hudText,HudMuted);
        }
        void DrawRunResult(float width,float height)
        {
            HudFill(new Rect(0,0,width,height),new Color(0,0,0,.72f));
            DrawCenteredStatus(width,height,Won ? "CANYON CLEARED" : "RUN ENDED",$"{Kills} enemies defeated / {TotalXP} XP collected",Won ? HudCyan : new Color(1,.45f,.35f));
            if (!NetworkActive || NetworkManager.Singleton.IsServer)
                if (GUI.Button(new Rect(width/2-150,height/2+101,300,48),"START A FRESH RUN",hudButton)) Begin();
        }
        void DrawPauseMenu(float width,float height)
        {
            DrawCenteredStatus(width,height,NetworkActive ? "MOUSE RELEASED" : "PAUSED",NetworkActive ? "The shared fight continues. Click the canyon to return." : "Click the canyon to resume.",HudCyan);
            if (!NetworkActive || NetworkManager.Singleton.IsServer)
                if (GUI.Button(new Rect(width/2-150,height/2+101,300,44),"STOP COMBAT",hudButton)) { StopCombat(); ReleaseMouse(); }
        }
        void DrawPracticeCombatMenu()
        {
            EnsureHud();
            GUILayout.BeginArea(new Rect(Screen.width-320,290,302,190),GUI.skin.box);
            GUILayout.Label(NetworkActive ? "CO-OP COMBAT / SHARED XP" : "COMBAT PRACTICE",hudText);
            GUILayout.Label(NetworkActive ? "Join everyone, then the host starts with F." : "Press F to start a run. Aim and fire are automatic.",hudSmall);
            GUI.enabled=!NetworkActive || NetworkManager.Singleton.IsServer;
            if (GUILayout.Button("Start combat",hudButton,GUILayout.Height(40))) Begin();
            GUI.enabled=true; GUILayout.EndArea();
        }
        void DisposeHud() { if (hudRing) Destroy(hudRing); }
    }
}
