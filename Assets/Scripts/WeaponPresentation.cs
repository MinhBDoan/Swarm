using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
namespace Swarm
{
    public partial class CombatPlayground
    {
        readonly Dictionary<int,Mesh> presentationMeshes=new Dictionary<int,Mesh>();
        readonly Dictionary<MomentumMotor,LineRenderer[]> pulseIndicators=new Dictionary<MomentumMotor,LineRenderer[]>();
        Material pulseRangeMaterial;
        Mesh WeaponMesh(int shape)
        {
            if (presentationMeshes.TryGetValue(shape,out var existing)) return existing;
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            if (shape==5)
            {
                MeshBox(vertices,triangles,new Vector3(-.25f,0,.1f),new Vector3(.7f,.12f,.2f),Quaternion.Euler(0,-38,0));
                MeshBox(vertices,triangles,new Vector3(.25f,0,.1f),new Vector3(.7f,.12f,.2f),Quaternion.Euler(0,38,0));
            }
            else if (shape==6)
            {
                MeshBox(vertices,triangles,Vector3.zero,new Vector3(.055f,.055f,1.25f),Quaternion.identity);
                MeshCone(vertices,triangles,.18f,.38f,.62f);
                MeshBox(vertices,triangles,new Vector3(0,0,-.5f),new Vector3(.3f,.045f,.24f),Quaternion.identity);
                MeshBox(vertices,triangles,new Vector3(0,0,-.5f),new Vector3(.045f,.3f,.24f),Quaternion.identity);
            }
            else
            {
                MeshBox(vertices,triangles,Vector3.zero,new Vector3(.23f,.23f,.8f),Quaternion.identity);
                MeshCone(vertices,triangles,.23f,.4f,.4f);
                MeshBox(vertices,triangles,new Vector3(0,0,-.28f),new Vector3(.5f,.08f,.25f),Quaternion.identity);
                MeshBox(vertices,triangles,new Vector3(0,0,-.28f),new Vector3(.08f,.5f,.25f),Quaternion.identity);
            }
            var mesh=new Mesh { name=shape==5 ? "SwarmBoomerang" : shape==6 ? "SwarmArrow" : "SwarmRocket" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            presentationMeshes.Add(shape,mesh); return mesh;
        }
        void MeshBox(List<Vector3> vertices,List<int> triangles,Vector3 center,Vector3 size,Quaternion rotation)
        {
            Vector3[] corners={new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
            int[] faces={0,3,2,1,4,5,6,7,0,4,7,3,1,2,6,5,3,7,6,2,0,1,5,4};
            for (int f=0;f<6;f++)
            {
                int start=vertices.Count;
                for (int i=0;i<4;i++) vertices.Add(center+rotation*Vector3.Scale(corners[faces[f*4+i]],size*.5f));
                triangles.AddRange(new[] {start,start+1,start+2,start,start+2,start+3});
            }
        }
        void MeshCone(List<Vector3> vertices,List<int> triangles,float radius,float length,float z)
        {
            for (int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4; int start=vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,z));
                vertices.Add(new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,z));
                vertices.Add(new Vector3(0,0,z+length));
                triangles.AddRange(new[] {start,start+1,start+2});
                start=vertices.Count;
                vertices.Add(new Vector3(0,0,z));
                vertices.Add(new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,z));
                vertices.Add(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,z));
                triangles.AddRange(new[] {start,start+1,start+2});
            }
        }
        GameObject CreateWeaponProjectile(BoltUpgrade weapon,Vector3 origin,Vector3 direction)
        {
            int shape=weapon==BoltUpgrade.Boomerang ? 5 : weapon==BoltUpgrade.PiercingBow ? 6 : 7;
            var body=new GameObject(WeaponName(weapon)+" projectile",typeof(MeshFilter),typeof(MeshRenderer));
            body.layer=2; body.transform.SetParent(root); body.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(direction));
            body.GetComponent<MeshFilter>().sharedMesh=WeaponMesh(shape);
            body.GetComponent<Renderer>().sharedMaterial=weaponMaterials[weapon];
            var trail=body.AddComponent<TrailRenderer>();
            trail.time=weapon==BoltUpgrade.Boomerang ? .24f : .18f;
            trail.startWidth=weapon==BoltUpgrade.RocketLauncher ? .18f : .08f; trail.endWidth=.01f;
            trail.minVertexDistance=.12f; trail.sharedMaterial=weaponMaterials[weapon];
            Spark(origin,.1f,weapon==BoltUpgrade.RocketLauncher ? .4f : .23f,.1f,weaponMaterials[weapon]);
            return body;
        }
        void UpdatePulseIndicator(int rank,float clock)
        {
            if (!pulseRangeMaterial) pulseRangeMaterial=MakeMaterial(new Color(.12f,.35f,.22f));
            if (!pulseIndicators.TryGetValue(player,out var lines) || !lines[0])
            {
                lines=new LineRenderer[2];
                for (int i=0;i<2;i++)
                {
                    var body=new GameObject(i==0 ? "Pulse range" : "Pulse charge",typeof(LineRenderer));
                    body.layer=2; body.transform.SetParent(root); lines[i]=body.GetComponent<LineRenderer>();
                    lines[i].useWorldSpace=false; lines[i].startWidth=lines[i].endWidth=i==0 ? .035f : .07f;
                    lines[i].sharedMaterial=i==0 ? pulseRangeMaterial : weaponMaterials[BoltUpgrade.PulseField];
                    lines[i].loop=i==0;
                }
                pulseIndicators[player]=lines;
            }
            foreach (var line in lines) line.gameObject.SetActive(true);
            float radius=3.5f+(rank-1)*.5f;
            float progress=1-Mathf.Clamp01(clock/1.4f);
            for (int i=0;i<2;i++)
            {
                var line=lines[i];
                var avatar=player.GetComponent<NetworkAvatar>();
                line.forceRenderingOff=CoopAuthority && avatar && avatar.OwnerClientId!=NetworkManager.Singleton.LocalClientId;
                line.transform.position=player.transform.position+Vector3.up*.1f;
                int count=i==0 ? 48 : Mathf.Max(2,Mathf.RoundToInt(progress*48));
                line.positionCount=count;
                for (int j=0;j<count;j++)
                { float angle=j*Mathf.PI*2/48; line.SetPosition(j,new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(radius+(i==0 ? 0 : -.08f))); }
            }
        }
        void SteerReturningBoomerang(Bullet bullet,Vector3 home,float dt)
        {
            bullet.returnAge+=dt;
            float t=Mathf.Clamp01(bullet.returnAge/bullet.returnDuration),u=1-t;
            Vector3 side=Vector3.Cross(Vector3.up,bullet.returnForward).normalized;
            Vector3 control1=bullet.returnStart+bullet.returnForward*1.25f;
            Vector3 control2=Vector3.Lerp(bullet.returnStart,home,.6f)+side*.35f;
            Vector3 point=u*u*u*bullet.returnStart+3*u*u*t*control1+3*u*t*t*control2+t*t*t*home;
            // The endpoint follows the thrower; cap pursuit speed if they teleport/reset.
            bullet.velocity=Vector3.ClampMagnitude((point-bullet.body.transform.position)/Mathf.Max(.0001f,dt),40);
        }
        Material BulletFeedbackMaterial(Bullet bullet)
        {
            if (bullet.boomerang) return weaponMaterials[BoltUpgrade.Boomerang];
            if (bullet.arrow) return weaponMaterials[BoltUpgrade.PiercingBow];
            return bullet.rocket ? weaponMaterials[BoltUpgrade.RocketLauncher] : sparkMaterial;
        }
        void ArrowImpact(Vector3 position,Vector3 direction)
        {
            var body=new GameObject("Piercing streak",typeof(LineRenderer)); body.layer=2; body.transform.SetParent(root); body.transform.position=position;
            var line=body.GetComponent<LineRenderer>(); line.sharedMaterial=weaponMaterials[BoltUpgrade.PiercingBow]; line.useWorldSpace=false;
            line.startWidth=.13f; line.endWidth=.025f; line.positionCount=2;
            line.SetPosition(0,-direction*.7f); line.SetPosition(1,direction*1.1f);
            AddEffect(body,1,1,.14f);
        }
        void SetPulseVisible(MomentumMotor owner,bool visible)
        { if (owner && pulseIndicators.TryGetValue(owner,out var lines)) foreach (var line in lines) if (line) line.gameObject.SetActive(visible); }
        void PrunePulseIndicators()
        {
            var removed=new List<MomentumMotor>();
            foreach (var entry in pulseIndicators)
            {
                var avatar=entry.Key ? entry.Key.GetComponent<NetworkAvatar>() : null;
                if (entry.Key && (!avatar || party.ContainsKey(avatar.OwnerClientId))) continue;
                foreach (var line in entry.Value) if (line) { line.gameObject.SetActive(false); Destroy(line.gameObject); }
                removed.Add(entry.Key);
            }
            foreach (var owner in removed) pulseIndicators.Remove(owner);
        }
        void ClearWeaponPresentation() { pulseIndicators.Clear(); }
        void DisposeWeaponPresentation()
        {
            foreach (var mesh in presentationMeshes.Values) Destroy(mesh);
            if (pulseRangeMaterial) Destroy(pulseRangeMaterial);
        }
    }
}
