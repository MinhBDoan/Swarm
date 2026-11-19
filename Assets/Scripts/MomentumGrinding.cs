using UnityEngine;
namespace Swarm
{
    public partial class MomentumMotor
    {
        GrindRail grindRail;
        float grindDistance,grindSpeed,grindBalance,grindAge,railRegrabUntil;
        int grindSign;
        public bool IsGrinding=>grindRail;
        public float GrindBalance=>grindBalance;
        public Vector3 GrindDirection {get {if(!grindRail)return Vector3.zero;grindRail.At(grindDistance,out var tangent);return tangent*grindSign;}}
        public bool TryGrabNearestRail(Vector3 look)
        {
            GrindRail nearest=null;float best=2.1f;
            foreach(var rail in GrindRail.Rails)
            {
                if(!rail || rail.points==null || rail.points.Length<2)continue;
                Vector3 point=rail.At(rail.ClosestDistance(transform.position-Vector3.up*1.15f),out _)+Vector3.up*1.15f;
                float distance=Vector3.Distance(transform.position,point);
                if(distance<best){nearest=rail;best=distance;}
            }
            return nearest && TryGrabRail(nearest,look);
        }
        public bool TryGrabRail(GrindRail rail,Vector3 look)
        {
            if(!rail || rail.points==null || rail.points.Length<2 || rail.Length<1 || IsGrinding || IsZiplining || Time.time<railRegrabUntil)return false;
            float along=rail.ClosestDistance(transform.position-Vector3.up*1.15f);
            Vector3 point=rail.At(along,out var tangent)+Vector3.up*1.15f;
            if(Vector3.Distance(transform.position,point)>2.1f || Physics.Linecast(transform.position,point,1))return false;
            float radius=controller.radius*.95f,half=controller.height*.5f-radius;
            if(Physics.CheckCapsule(point+Vector3.up*half,point-Vector3.up*half,radius,1))return false;
            float alignment=Vector3.Dot(Velocity,tangent);
            int sign=Mathf.Abs(alignment)>1 ? (alignment<0 ? -1 : 1) : Vector3.Dot(look,tangent)<0 ? -1 : 1;
            controller.Move(point-transform.position);
            if(Vector3.Distance(transform.position,point)>.2f)return false;
            grindRail=rail;grindDistance=along;grindSign=sign;
            grindSpeed=Mathf.Max(4,Vector3.ProjectOnPlane(Velocity,Vector3.up).magnitude);
            grindAge=0;grindBalance=.08f*sign;Velocity=tangent*sign*grindSpeed;
            Sliding=false;jumpTime=groundedTime=wallTime=-100;return true;
        }
        public void StepGrinding(float mouseDelta,bool jump,bool drop,float dt)
        {
            if(!IsGrinding || CombatPlayground.BlocksInput)return;
            if(jump || drop){ReleaseGrind(jump);return;}
            if(dt<=0 || CombatPlayground.BlocksInput)return;
            dt=Mathf.Min(dt,.05f);grindAge+=dt;
            Vector3 before=GrindDirection;
            grindSpeed=Mathf.Max(0,grindSpeed+Vector3.Dot(Vector3.down*gravity,before)*dt);
            if(grindSpeed<.5f){ReleaseGrind(false);return;}
            grindDistance+=grindSign*grindSpeed*dt;
            Vector3 destination=grindRail.At(grindDistance,out var tangent)+Vector3.up*1.15f;
            Vector3 direction=tangent*grindSign;
            float turn=Vector3.SignedAngle(before,direction,Vector3.up)*Mathf.Deg2Rad;
            // Mouse motion is a displacement, while instability integrates over time.
            grindBalance+=(grindBalance*.6f+Mathf.Sin(grindAge*2.3f)*(.25f+grindSpeed*.006f))*dt+turn*.7f+mouseDelta*.16f;
            Velocity=direction*grindSpeed;controller.Move(destination-transform.position);
            BestSpeed=Mathf.Max(BestSpeed,Vector3.ProjectOnPlane(Velocity,Vector3.up).magnitude);
            if(Mathf.Abs(grindBalance)>=1){ReleaseGrind(false,true);return;}
            if(grindDistance<=0 || grindDistance>=grindRail.Length || Vector3.Distance(transform.position,destination)>.25f)ReleaseGrind(false);
        }
        public void ReleaseGrind(bool jump,bool failed=false)
        {
            if(!IsGrinding)return;
            Vector3 direction=GrindDirection;grindRail=null;railRegrabUntil=Time.time+.35f;
            jumpTime=groundedTime=wallTime=-100;
            if(failed)Velocity=direction*grindSpeed*.75f+Vector3.Cross(Vector3.up,direction)*Mathf.Sign(grindBalance)*3;
            else if(jump){Velocity+=Vector3.up*jumpSpeed;impulseUntil=Time.time+.18f;}
        }
        void DrawGrindHud()
        {
            if(!enabled || CombatPlayground.BlocksInput || !Camera.main)return;
            if(!IsGrinding)
            {
                if(IsZiplining)return;
                foreach(var rail in GrindRail.Rails)
                    if(rail && rail.points!=null && rail.points.Length>1 && Vector3.Distance(transform.position,rail.At(rail.ClosestDistance(transform.position),out _))<2.5f)
                    {GUI.Box(new Rect(Screen.width/2-180,Screen.height*.7f,360,30),"E: GRIND RAIL / carry your approach speed");break;}
                return;
            }
            Vector3 screen=Camera.main.WorldToScreenPoint(transform.position+Vector3.up*1.8f);
            if(screen.z<=0)return;
            float x=Mathf.Clamp(screen.x-110,12,Screen.width-232),y=Mathf.Clamp(Screen.height-screen.y-55,150,Screen.height-110);
            GUI.Box(new Rect(x-8,y-8,236,72),"BALANCE / mouse left-right");
            Color saved=GUI.color;GUI.color=new Color(.15f,.18f,.22f);GUI.DrawTexture(new Rect(x,y+20,220,10),Texture2D.whiteTexture);
            GUI.color=new Color(.3f,.9f,1);GUI.DrawTexture(new Rect(x+108,y+17,4,16),Texture2D.whiteTexture);
            GUI.color=Mathf.Abs(grindBalance)>.7f ? Color.red : Color.yellow;
            GUI.DrawTexture(new Rect(x+110+Mathf.Clamp(grindBalance,-1,1)*105-4,y+15,8,20),Texture2D.whiteTexture);GUI.color=saved;
            GUI.Label(new Rect(x-5,y+36,230,25),"Space: jump off / Ctrl or C: drop");
        }
    }
}
