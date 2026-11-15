using System.Collections.Generic;
using UnityEngine;
namespace Swarm
{
    public class DrillHazard : MonoBehaviour
    {
        public static DrillHazard Instance { get; private set; }
        public Transform head;
        public LineRenderer warning;
        public Renderer beacon;
        public const float RaisedSeconds=4.5f, WarningSeconds=1.8f, DropSeconds=.65f, BuriedSeconds=1.2f, RiseSeconds=2;
        public const float CycleSeconds=RaisedSeconds+WarningSeconds+DropSeconds+BuriedSeconds+RiseSeconds;
        public const float HeadRadius=8.8f, HeadHeight=5, RaisedTip=12, BuriedTip=-.35f;
        public enum DrillPhase { Clear, Warning, Descending, Embedded, Rising }
        public float CycleTime { get; private set; }
        public bool Paused { get; private set; }
        public DrillPhase Phase => PhaseAt(CycleTime);
        public Vector3 Center => transform.position;
        public float TipHeight => TipAt(CycleTime)+Center.y;
        public float PreviousTip { get; private set; }
        public bool IsStriking => Phase==DrillPhase.Descending || Phase==DrillPhase.Embedded || Phase==DrillPhase.Rising;
        readonly HashSet<int> struck=new HashSet<int>();
        int strikeCycle=-1;
        Material signalMaterial;
        public static DrillPhase PhaseAt(float time)
        {
            float t=Mathf.Repeat(time,CycleSeconds);
            if (t<RaisedSeconds) return DrillPhase.Clear;
            if (t<RaisedSeconds+WarningSeconds) return DrillPhase.Warning;
            if (t<RaisedSeconds+WarningSeconds+DropSeconds) return DrillPhase.Descending;
            if (t<RaisedSeconds+WarningSeconds+DropSeconds+BuriedSeconds) return DrillPhase.Embedded;
            return DrillPhase.Rising;
        }
        public static float TipAt(float time)
        {
            float t=Mathf.Repeat(time,CycleSeconds);
            if (t<RaisedSeconds+WarningSeconds) return RaisedTip;
            t-=RaisedSeconds+WarningSeconds;
            if (t<DropSeconds) return Mathf.Lerp(RaisedTip,BuriedTip,Mathf.SmoothStep(0,1,t/DropSeconds));
            t-=DropSeconds;
            if (t<BuriedSeconds) return BuriedTip;
            return Mathf.Lerp(BuriedTip,RaisedTip,Mathf.SmoothStep(0,1,(t-BuriedSeconds)/RiseSeconds));
        }
        void Awake()
        {
            Instance=this; PreviousTip=TipHeight;
            if (warning)
            {
                signalMaterial=new Material(warning.sharedMaterial); warning.sharedMaterial=signalMaterial;
                warning.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; warning.receiveShadows=false;
                if (beacon) beacon.sharedMaterial=signalMaterial;
            }
        }
        void Update()
        {
            var combat=CombatPlayground.Instance;
            bool client=combat && combat.IsCoopClient;
            if (!client) Paused=combat && combat.DrillWorldPaused;
            PreviousTip=TipHeight;
            if (!Paused) CycleTime+=Mathf.Min(Time.deltaTime,.05f);
            if (!client && IsStriking)
            {
                int cycle=Mathf.FloorToInt(CycleTime/CycleSeconds);
                if (cycle!=strikeCycle) { strikeCycle=cycle; struck.Clear(); }
            }
            Pose();
            if (!client && combat) combat.TickDrill(this,Mathf.Min(Time.deltaTime,.05f));
        }
        public void AcceptState(float time,bool paused) { CycleTime=time; PreviousTip=TipHeight; Paused=paused; Pose(); }
        void Pose()
        {
            if (head) head.localPosition=Vector3.up*TipAt(CycleTime);
            Color color=Phase==DrillPhase.Warning ? new Color(1,.65f,.08f) : IsStriking ? new Color(1,.2f,.08f) : new Color(.2f,.7f,.65f);
            if (warning)
            {
                warning.startColor=warning.endColor=Color.white;
                warning.startWidth=warning.endWidth=Phase==DrillPhase.Warning ? .12f+Mathf.Sin(CycleTime*18)*.04f : .07f;
                if (signalMaterial) signalMaterial.color=color;
            }

        }
        public bool Near(Vector3 point) => Vector3.ProjectOnPlane(point-Center,Vector3.up).magnitude<48;
        public string Status
        {
            get
            {
                float t=Mathf.Repeat(CycleTime,CycleSeconds);
                switch (Phase)
                {
                    case DrillPhase.Clear: return $"DRILL CLEAR / warning in {RaisedSeconds-t:0.0}s";
                    case DrillPhase.Warning: return $"DRILL DROPPING IN {RaisedSeconds+WarningSeconds-t:0.0}s";
                    case DrillPhase.Descending:return "DRILL STRIKING / clear the center";
                    case DrillPhase.Embedded:return "DRILL DOWN / use the rim";
                    default:return "DRILL RISING / wait for clearance";
                }
            }
        }
        public bool TryStrike(int actor,Vector3 center,float halfHeight,float radius)
        {
            if (Paused || !IsStriking || struck.Contains(actor)) return false;
            float tip=Mathf.Min(PreviousTip,TipHeight),top=Mathf.Max(PreviousTip,TipHeight)+HeadHeight;
            if (center.y+halfHeight<tip || center.y-halfHeight>top) return false;
            float coneRadius=HeadRadius*Mathf.Clamp01((center.y+halfHeight-tip)/HeadHeight);
            if (Vector3.ProjectOnPlane(center-Center,Vector3.up).magnitude>coneRadius+radius) return false;
            struck.Add(actor); return true;
        }
        public Vector3 EscapeImpulse(Vector3 point)
        {
            Vector3 away=Vector3.ProjectOnPlane(point-Center,Vector3.up);
            if (away.sqrMagnitude<.01f) away=Vector3.right;
            return away.normalized*10+Vector3.up*7;
        }
        void OnGUI()
        {
            var combat=CombatPlayground.Instance;
            if (combat && combat.Running) return;
            var camera=Camera.main;
            if (!camera || !Near(camera.transform.position)) return;
            var rect=new Rect(18,242,470,100); GUI.Box(rect,GUIContent.none);
            var style=new GUIStyle(GUI.skin.label) {fontSize=18,wordWrap=true};
            GUI.Label(new Rect(32,252,442,80),Status+"\nSlide through the bowl / safe route around the rim\n"+(combat ? $"Practice health: {combat.DrillPracticeHealth:0}/100" : ""),style);
        }
        void OnDestroy() { if (signalMaterial) Destroy(signalMaterial); if (Instance==this) Instance=null; }
    }
}

