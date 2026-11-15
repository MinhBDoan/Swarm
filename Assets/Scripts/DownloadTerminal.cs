using UnityEngine;
namespace Swarm
{
    public class DownloadTerminal : MonoBehaviour
    {
        public int terminalIndex=2;
        LineRenderer boundary,progress;
        Material material;
        readonly Vector3[] innerPoints=new Vector3[97];
        void Awake()
        {
            material=new Material(Shader.Find("Sprites/Default"));
            boundary=Line("Download boundary",.1f); progress=Line("Download progress",.16f);
            boundary.loop=true; boundary.positionCount=96;
            Physics.SyncTransforms();
            for(int i=0;i<97;i++)
            {
                if(i<96) boundary.SetPosition(i,GroundPoint(i/96f,CanyonLayout.DownloadRadius));
                innerPoints[i]=GroundPoint(i/96f,CanyonLayout.DownloadRadius-.22f);
            }
        }
        LineRenderer Line(string title,float width)
        {
            var obj=new GameObject(title,typeof(LineRenderer)); obj.layer=2; obj.transform.SetParent(transform,false);
            var line=obj.GetComponent<LineRenderer>(); line.useWorldSpace=false; line.sharedMaterial=material;
            line.startWidth=line.endWidth=width; line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; return line;
        }
        static Vector3 Point(float t,float radius) => new Vector3(Mathf.Sin(t*Mathf.PI*2)*radius,.07f,Mathf.Cos(t*Mathf.PI*2)*radius);
        Vector3 GroundPoint(float fraction,float radius)
        {
            Vector3 local=Point(fraction,radius),world=transform.TransformPoint(local);
            var hits=Physics.RaycastAll(world+Vector3.up,Vector3.down,3,1<<0);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.collider.name.Contains("download console")) continue;
                local.y=hit.point.y-transform.position.y+.07f; break;
            }
            return local;
        }
        Vector3 ProgressPoint(float fraction)
        {
            float sample=Mathf.Clamp01(fraction)*96; int index=Mathf.Min(95,Mathf.FloorToInt(sample));
            return Vector3.Lerp(innerPoints[index],innerPoints[index+1],sample-index);
        }
        void Update()
        {
            var combat=CombatPlayground.Instance;
            bool active=combat && combat.Running && combat.DownloadStateAt(terminalIndex)==CombatPlayground.DownloadPhase.Active;
            bool done=combat && combat.Running && combat.DownloadStateAt(terminalIndex)==CombatPlayground.DownloadPhase.Completed;
            Color color=CombatPlayground.RarityColor((CombatPlayground.UpgradeRarity)terminalIndex);
            boundary.startColor=boundary.endColor=color;
            float percent=combat && combat.Running ? combat.DownloadProgressAt(terminalIndex) : 0;
            progress.enabled=percent>.01f; int count=Mathf.Max(2,Mathf.CeilToInt(percent*.96f)+1); progress.positionCount=count;
            for (int i=0;i<count;i++) progress.SetPosition(i,ProgressPoint(percent*.01f*i/(count-1)));
            progress.startColor=progress.endColor=done ? Color.green : active && combat.SelectedDownload==terminalIndex && combat.DownloadOccupants==0 ? new Color(1,.35f,.2f) : Color.white;
        }
        void OnDestroy() { if (material) Destroy(material); }
    }
}
