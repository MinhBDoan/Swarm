using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Swarm
{
    public partial class CombatPlayground
    {
        UIDocument persistentHud;
        PanelSettings persistentHudSettings;
        VisualElement hudHeader, hudHealth, hudWeapons;
        Label hudWaveLine, hudWaveDetail, hudMoneyLine, hudHealthLine, hudXpLine, hudMovementLine, hudWeaponHeading, hudModifiers;
        VisualElement hudHpFill, hudXpFill;
        readonly Label[] hudWeaponNames = new Label[4];
        readonly Label[] hudWeaponDetails = new Label[4];
        readonly VisualElement[] hudWeaponCards = new VisualElement[4];
        readonly VisualElement[] hudCooldownFills = new VisualElement[4];
        Font persistentFont;
        const string HudModePreference = "Swarm.LegacyCombatHud";
        bool UseLegacyHud => PlayerPrefs.GetInt(HudModePreference,0)!=0;

        void ToggleCombatHud()
        {
            bool fallback = !UseLegacyHud;
            PlayerPrefs.SetInt(HudModePreference,fallback ? 1 : 0);
            PlayerPrefs.Save();
            if (persistentHud)
                persistentHud.rootVisualElement.style.display=Running && !fallback ? DisplayStyle.Flex : DisplayStyle.None;
            Debug.Log(fallback ? "Swarm HUD: classic renderer" : "Swarm HUD: screen panel renderer");
        }

        bool PersistentHudVisible => persistentHud && persistentHud.rootVisualElement.style.display.value==DisplayStyle.Flex;

        void UpdatePersistentHud()
        {
            if (SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) return;
            if (!Running || UseLegacyHud)
            {
                if (persistentHud) persistentHud.rootVisualElement.style.display=DisplayStyle.None;
                return;
            }
            if (!persistentHud && !CreatePersistentHud()) return;
            var root=persistentHud.rootVisualElement;
            root.style.display=DisplayStyle.Flex;
            float scale=HudScaleFor(Screen.width,Screen.height);
            if (scale<=0) return;
            persistentHudSettings.scale=scale;
            float width=Screen.width/scale, height=Screen.height/scale;
            hudHeader.style.left=width*.5f-230;
            hudHealth.style.top=height-203;
            hudWeapons.style.left=width*.5f-402;
            hudWeapons.style.top=height-184;

            hudWaveLine.text=CombatWaveTitle();
            hudWaveDetail.text=CombatWaveDetail();
            hudHeader.style.borderTopColor=IsAreaOvertime ? new Color(1,.35f,.18f) : HudCyan;
            hudWaveLine.style.color=IsAreaOvertime ? new Color(1,.5f,.3f) : Color.white;
            hudMoneyLine.text=$"PARTY MONEY  {SharedMoney:N0}";
            Color healthColor=Health>30 ? new Color(.35f,.9f,.5f) : new Color(1,.35f,.3f);
            hudHealthLine.text=$"{Mathf.CeilToInt(Health)} HP     LEVEL {Level}";
            hudHealth.style.borderTopColor=healthColor;
            hudHpFill.style.backgroundColor=healthColor;
            hudHpFill.style.width=Length.Percent(Mathf.Clamp01(Health/100)*100);
            hudXpLine.text=$"{(NetworkActive ? "Shared XP" : "XP")}  {xp} / {RequiredXP}";
            hudXpFill.style.width=Length.Percent(Mathf.Clamp01(xp/(float)RequiredXP)*100);
            float speed=player ? Vector3.ProjectOnPlane(player.Velocity,Vector3.up).magnitude : 0;
            hudMovementLine.text=$"{speed:0.0} m/s    /    Power x{powerMultiplier:0.00}    /    Regen {RecoveryPerSecond:0.0}";
            hudWeaponHeading.text=$"YOUR WEAPONS   {EquippedWeaponCount} / 4";
            SetPersistentWeapon(0,BoltUpgrade.HeavyBolts);
            int slot=1;
            foreach (var entry in weapons)
            {
                if (slot>=4) break;
                SetPersistentWeapon(slot++,entry.Key);
            }
            for (;slot<4;slot++)
            {
                hudWeaponCards[slot].style.borderTopColor=new Color(.25f,.31f,.37f);
                hudWeaponNames[slot].text="Empty slot";
                hudWeaponDetails[slot].text="Find a new weapon";
                hudCooldownFills[slot].style.width=Length.Percent(0);
            }
            hudModifiers.text=$"Gun: Ricochet {RicochetRank} / Pierce {PierceRank} / Blast {BlastRank}"+
                (criticalChance>0 ? $" / Crit {criticalChance*100:0}%" : "")+
                (shockwaveRank>0 ? $" / Shockwave {shockwaveCharge}/{Mathf.Max(3,7-shockwaveRank)}" : "");
        }

        void SetPersistentWeapon(int slot,BoltUpgrade weapon)
        {
            bool gun=slot==0;
            int rank=gun ? 1 : WeaponRank(weapon);
            float clock=gun ? fireClock : weaponClocks.TryGetValue(weapon,out float cooldown) ? cooldown : 0;
            float duration=gun ? FireInterval : WeaponCooldown(weapon,rank);
            Color color=WeaponColor(weapon);
            hudWeaponCards[slot].style.borderTopColor=color;
            hudWeaponNames[slot].text=gun ? "Auto gun" : WeaponName(weapon);
            hudWeaponDetails[slot].text=gun ? $"{Damage:0} dmg / {ProjectileCount} bolts" :
                 $"Level {rank}/5  /  "+(clock<=0 ? "Ready" : $"{clock:0.0}s");
            hudCooldownFills[slot].style.backgroundColor=color;
            hudCooldownFills[slot].style.width=Length.Percent((1-Mathf.Clamp01(clock/duration))*100);
        }

        bool CreatePersistentHud()
        {
            var template=Resources.Load<PanelSettings>("CombatHudPanel");
            if (!template) return false;
            persistentFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            persistentHudSettings=Instantiate(template);
            persistentHudSettings.scaleMode=PanelScaleMode.ConstantPixelSize;
            persistentHudSettings.clearColor=false;
            persistentHudSettings.clearDepthStencil=false;
            var obj=new GameObject("Combat HUD");
            obj.SetActive(false);
            obj.transform.SetParent(transform,false);
            persistentHud=obj.AddComponent<UIDocument>();
            persistentHud.panelSettings=persistentHudSettings;
            obj.SetActive(true);
            var root=persistentHud.rootVisualElement;
            root.pickingMode=PickingMode.Ignore;

            hudHeader=HudElement(root,0,22,460,122,HudPanel,HudCyan);
            hudWaveLine=HudText(hudHeader,18,12,425,30,23,Color.white,true);
            hudWaveDetail=HudText(hudHeader,18,49,425,25,16,HudMuted);
            hudMoneyLine=HudText(hudHeader,18,79,425,32,23,new Color(1,.8f,.3f),true);
            hudHealth=HudElement(root,22,0,330,181,HudPanel,HudCyan);
            hudHealthLine=HudText(hudHealth,16,13,298,39,28,Color.white,true);
            hudHpFill=HudBarElement(hudHealth,16,58,298,16,HudCyan);
            hudXpLine=HudText(hudHealth,16,83,298,24,16,HudMuted);
            hudXpFill=HudBarElement(hudHealth,16,112,298,8,HudCyan);
            hudMovementLine=HudText(hudHealth,16,135,298,25,16,HudMuted);

            hudWeapons=HudElement(root,0,0,804,184,Color.clear,Color.clear);
            hudWeaponHeading=HudText(hudWeapons,0,0,804,26,16,HudMuted);
            hudModifiers=HudText(hudWeapons,0,150,804,23,16,HudMuted);
            for (int slot=0;slot<4;slot++)
            {
                var card=HudElement(hudWeapons,slot*204,34,192,100,HudPanel,HudCyan);
                hudWeaponCards[slot]=card;
                HudText(card,12,12,26,22,16,HudMuted).text=(slot+1).ToString("00");
                hudWeaponNames[slot]=HudText(card,12,36,175,29,19,Color.white);
                hudWeaponDetails[slot]=HudText(card,12,67,175,22,16,HudMuted);
                hudCooldownFills[slot]=HudBarElement(card,0,93,192,4,HudCyan);
            }
            return true;
        }

        VisualElement HudElement(VisualElement parent,float x,float y,float width,float height,Color background,Color accent)
        {
            var element=new VisualElement { pickingMode=PickingMode.Ignore };
            PositionHudElement(element,x,y,width,height);
            element.style.backgroundColor=background;
            if (accent.a>0)
            {
                element.style.borderTopWidth=3;
                element.style.borderTopColor=accent;
            }
            parent.Add(element);
            return element;
        }

        Label HudText(VisualElement parent,float x,float y,float width,float height,int size,Color color,bool bold=false)
        {
            var text=new Label { pickingMode=PickingMode.Ignore };
            PositionHudElement(text,x,y,width,height);
            text.style.unityFont=persistentFont;
            text.style.fontSize=size;
            text.style.unityFontStyleAndWeight=bold ? FontStyle.Bold : FontStyle.Normal;
            text.style.color=color;
            parent.Add(text);
            return text;
        }

        VisualElement HudBarElement(VisualElement parent,float x,float y,float width,float height,Color color)
        {
            var track=HudElement(parent,x,y,width,height,new Color(.12f,.16f,.2f),Color.clear);
            return HudElement(track,0,0,width,height,color,Color.clear);
        }

        static void PositionHudElement(VisualElement element,float x,float y,float width,float height)
        {
            element.style.position=Position.Absolute;
            element.style.left=x;
            element.style.top=y;
            element.style.width=width;
            element.style.height=height;
        }

        void DisposePersistentHud()
        {
            if (persistentHud) Destroy(persistentHud.gameObject);
            if (persistentHudSettings) Destroy(persistentHudSettings);
        }
    }
}
