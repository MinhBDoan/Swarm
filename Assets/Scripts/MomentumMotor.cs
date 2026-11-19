using UnityEngine;

namespace Swarm
{
    [RequireComponent(typeof(CharacterController))]
    public class MomentumMotor : MonoBehaviour
    {
        public float runSpeed = 11f, groundAcceleration = 28f, airAcceleration = 7f;
        public float jumpSpeed = 10f, gravity = 25f;
        public float bouncePush = 9f, bounceWindow = .16f;
        public Vector3 Velocity { get; private set; }
        public bool Sliding { get; private set; }
        public float BestSpeed { get; private set; }
        public Transform view;
        public float SlideHopBonus { get; private set; }
        public bool RecentSlideHop => Time.unscaledTime-lastSlideHopAt<.6f;
        float slideAge=-1,lastSlideHopAt=-100;
        bool slideHopReady=true,wasGrounded,wasSliding;
        public bool IsZiplining => zipline;
        MiningZipline zipline;
        float zipDistance, zipSpeed, regrabUntil;
        int zipDirection;
        Vector3 zipOffset;
        bool crouchWasHeld;
        CharacterController controller;
        Vector3 wallNormal, groundNormal = Vector3.up;
        float wallTime = -100f, jumpTime = -100f, groundedTime = -100f, bounceTime = -100f;
        bool grounded;
        float impulseUntil;
        Vector3 spawn;

        void Awake() { controller = GetComponent<CharacterController>(); spawn = transform.position; }
        public void SetSpawn(Vector3 position) { spawn = position; Respawn(); }
        void Update()
        {
            var combat=CombatPlayground.Instance;
            // Browsing blocks controls, not gravity or enemy knockback.
            if (combat && combat.Running && combat.ShopOpen && !combat.PartyPaused && !combat.LocalMenuOpen &&
                !combat.ChoosingUpgrade && combat.Health>0 && !combat.Won)
            {
                if (transform.position.y < -25f) Respawn();
                Simulate(Vector2.zero,false,false,Mathf.Min(Time.deltaTime,.033f));
                return;
            }
            if (CombatPlayground.BlocksInput) return;
            bool interact = Input.GetKeyDown(KeyCode.E);
            bool shopInteraction = interact && !IsZiplining && CombatPlayground.Instance && CombatPlayground.Instance.TryOpenShop();
            bool rewardInteraction = interact && !shopInteraction && !IsZiplining && combat &&
                (combat.TryFreeChest() || combat.TryEliteChallenge());
            bool bossInteraction = interact && !shopInteraction && !rewardInteraction && !IsZiplining && CombatPlayground.Instance &&
                CombatPlayground.Instance.TryBossConsole();
            if (shopInteraction) return;
            if (Cursor.lockState != CursorLockMode.Locked && !Application.isBatchMode) return;
            if (Input.GetKeyDown(KeyCode.R) || transform.position.y < -25f) Respawn();
            if (interact && !bossInteraction && !rewardInteraction)
            {
                if (IsZiplining) ReleaseZipline(false);
                else if (!(CombatPlayground.Instance && (CombatPlayground.Instance.TrySupplyInteraction() || CombatPlayground.Instance.StartCacheChallenge())))
                    TryGrabNearestZipline(view.forward);
            }
            Simulate(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C), Input.GetButtonDown("Jump"),
                Mathf.Min(Time.deltaTime, .033f));
        }
        // Input-independent stepping also supports controlled movement experiments.
        public void Simulate(Vector2 input, bool slideHeld, bool jumpPressed, float dt)
        {
            bool crouchPressed = slideHeld && !crouchWasHeld;
            crouchWasHeld = slideHeld;
            if (IsZiplining)
            {
                if (crouchPressed) ReleaseZipline(false);
                else RideZipline(jumpPressed,dt);
                return;
            }
            if (jumpPressed) jumpTime = Time.time;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 wish = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
            Vector3 velocity = Velocity;
            // Keep support across tiny gaps from the controller's slope solver.
            // Otherwise alternating air frames let uphill input add speed back.
            float supportReach=controller.height*.5f+.35f;
            // The capsule touches steep curves beside its center ray; extend only
            // the steep-surface query, keeping ordinary ground snapping unchanged.
            bool foundSupport = Physics.Raycast(transform.position + Vector3.up*.1f, Vector3.down,
                out RaycastHit support, supportReach+controller.radius*10, 1 << 0);
            bool supported=foundSupport && support.distance<=supportReach && support.normal.y>.55f;
            bool steepSupport=foundSupport && support.normal.y>.1f && support.normal.y<=.55f &&
                support.distance<=controller.height*.5f-controller.radius+controller.radius/support.normal.y+.4f;
            grounded = (controller.isGrounded || (supported && Vector3.Dot(velocity, support.normal) <= .1f)) && !(Time.time<impulseUntil && velocity.y>0);
            if (supported || steepSupport)
                groundNormal = support.normal;
            if (grounded && !wasGrounded && velocity.y<=0) slideHopReady=true;
            if (grounded) groundedTime = Time.time;
            bool groundJumpQueued = Time.time - jumpTime < .13f && Time.time - groundedTime < .1f && Time.time - bounceTime > .18f;
            Sliding = grounded && slideHeld;
            slideAge=Sliding ? wasSliding ? slideAge+dt : 0 : -1;
            wasGrounded=grounded; wasSliding=Sliding;
            Vector3 horizontal = new Vector3(velocity.x, 0, velocity.z);
            if (grounded)
            {
                if (Sliding || steepSupport)
                {
                    Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down * gravity, groundNormal);
                    horizontal += new Vector3(downhill.x, 0, downhill.z) * dt;
                    // No artificial slide drag: flat terrain carries speed, slopes change it.
                    // Steering bends a slide without replacing its earned speed.
                    // Steering cannot turn a gravity-driven rollback back uphill.
                    // Below walking speed, let gravity decide the direction.
                    if (Sliding && wish.sqrMagnitude > .01f && horizontal.magnitude > 2 && Vector3.Dot(horizontal, wish) >= 0)
                        horizontal = Vector3.RotateTowards(horizontal, wish * horizontal.magnitude, 1.4f * dt, 0);
                }
                else if (wish.sqrMagnitude > .01f)
                {
                    float along = Vector3.Dot(horizontal, wish);
                    horizontal += wish * Mathf.Clamp(runSpeed - along, 0, groundAcceleration * dt);
                    horizontal = Vector3.RotateTowards(horizontal, wish * horizontal.magnitude, 5f * dt, 0);
                }
                else if (!groundJumpQueued)
                    horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, groundAcceleration * .4f * dt);
                velocity.y = (Sliding || steepSupport) && groundNormal.y > .1f
                    ? -Vector3.Dot(horizontal, groundNormal) / groundNormal.y - 2f
                    : -3f;
            }
            else
            {
                // Air acceleration adds only the missing speed along the input direction.
                float available = Mathf.Max(0, runSpeed - Vector3.Dot(horizontal, wish));
                horizontal += wish * Mathf.Min(available, airAcceleration * dt);
                velocity.y -= gravity * dt;
            }
            velocity.x = horizontal.x; velocity.z = horizontal.z;
            if (Time.time - jumpTime < .13f)
            {
                if (!grounded && Time.time - wallTime < bounceWindow && Time.time - bounceTime > .18f)
                {
                    // Keep the tangent and falling velocity; jump input supplies upward impulse.
                    Vector3 planar = new Vector3(velocity.x, 0, velocity.z);
                    planar = Vector3.ProjectOnPlane(planar, wallNormal);
                    planar += wallNormal * bouncePush;
                    velocity.x = planar.x; velocity.z = planar.z;
                    velocity.y = Mathf.Max(velocity.y, jumpSpeed * .85f);
                    jumpTime = -100; wallTime = -100; bounceTime = Time.time;
                }
                else if (Time.time - groundedTime < .1f && Time.time - bounceTime > .18f)
                {
                    if (Sliding && slideHopReady && slideAge>=.08f && slideAge<=.28f)
                    {
                        Vector3 planar=new Vector3(velocity.x,0,velocity.z);
                        float speed=planar.magnitude,ceiling=runSpeed*2;
                        if (speed>=runSpeed*.8f && speed<ceiling)
                        {
                            float fade=1-Mathf.InverseLerp(runSpeed,ceiling,speed);
                            float bonus=Mathf.Min(speed*.07f*fade,ceiling-speed);
                            planar*=1+bonus/speed; velocity.x=planar.x; velocity.z=planar.z;
                            SlideHopBonus=bonus; lastSlideHopAt=Time.unscaledTime;
                        }
                    }
                    slideHopReady=false;
                    velocity.y = jumpSpeed; jumpTime = -100; groundedTime = -100;
                }
            }
            Velocity = velocity;
            groundNormal = Vector3.up;
            controller.Move(Velocity * dt);
            BestSpeed = Mathf.Max(BestSpeed, new Vector2(Velocity.x, Velocity.z).magnitude);
        }
        public bool TryGrabNearestZipline(Vector3 look)
        {
            MiningZipline nearest = null; float distance = 2.2f;
            foreach (var line in MiningZipline.Lines)
            {
                if (!line || line.Length < .1f) continue;
                float candidate = Vector3.Distance(transform.position,line.ClosestPoint(transform.position));
                if (candidate < distance) { nearest = line; distance = candidate; }
            }
            return nearest && TryGrabZipline(nearest,look);
        }
        public bool TryGrabZipline(MiningZipline line, Vector3 look)
        {
            if (!line || IsZiplining || Time.time < regrabUntil || line.Length < .1f) return false;
            Vector3 point = line.ClosestPoint(transform.position);
            if (Vector3.Distance(transform.position,point) > 2.2f || Physics.Linecast(transform.position,point,1 << 0)) return false;
            Vector3 offset;
            if (Mathf.Abs(line.Axis.y) > .7f)
            {
                offset = Vector3.ProjectOnPlane(transform.position-point,line.Axis);
                if (offset.sqrMagnitude < .01f) offset = Vector3.ProjectOnPlane(-look,line.Axis);
                if (offset.sqrMagnitude < .01f) offset = Vector3.right;
                offset = offset.normalized*.7f;
            }
            else offset = Vector3.ProjectOnPlane(Vector3.down*1.1f,line.Axis);
            Vector3 destination = point+offset;
            float radius = controller.radius*.95f;
            float half = controller.height*.5f-radius;
            if (Physics.CheckCapsule(destination+Vector3.up*half,destination-Vector3.up*half,radius,1 << 0)) return false;
            // Sweep into the hanging position; never teleport through terrain.
            controller.Move(destination-transform.position);
            if (Vector3.Distance(transform.position,destination) > .2f) return false;
            zipline = line; zipOffset = offset; zipDistance = Vector3.Dot(point-line.endpointA,line.Axis);
            zipDirection = line.Direction(look,Velocity);
            zipSpeed = Mathf.Max(line.travelSpeed,Vector3.Dot(Velocity,line.Axis)*zipDirection);
            Velocity = line.Axis*zipDirection*zipSpeed;
            Sliding = false; jumpTime = groundedTime = wallTime = -100;
            return true;
        }
        void RideZipline(bool jumpPressed, float dt)
        {
            if (jumpPressed) { ReleaseZipline(true); return; }
            zipSpeed = Mathf.MoveTowards(zipSpeed,zipline.travelSpeed,zipline.excessSpeedDrag*dt);
            zipDistance += zipDirection*zipSpeed*dt;
            Vector3 destination = zipline.endpointA+zipline.Axis*Mathf.Clamp(zipDistance,0,zipline.Length)+zipOffset;
            Velocity = zipline.Axis*zipDirection*zipSpeed;
            controller.Move(destination-transform.position);
            BestSpeed = Mathf.Max(BestSpeed,new Vector2(Velocity.x,Velocity.z).magnitude);
            if (zipDistance <= 0 || zipDistance >= zipline.Length || Vector3.Distance(transform.position,destination) > .25f)
                ReleaseZipline(false);
        }
        public void ReleaseZipline(bool jump)
        {
            if (!zipline) return;
            Vector3 axis = zipline.Axis;
            zipline = null; regrabUntil = Time.time+.18f;
            jumpTime = groundedTime = wallTime = -100;
            if (jump)
            {
                Vector3 outward = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(view.forward,Vector3.up),axis);
                if (outward.sqrMagnitude > .01f) outward = outward.normalized*4; else outward = Vector3.zero;
                Velocity += Vector3.up*(Mathf.Abs(axis.y) > .7f ? 3 : jumpSpeed*.8f)+outward;
            }
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > .55f) { groundNormal = hit.normal; return; }
            if (Mathf.Abs(hit.normal.y) < .3f)
            {
                wallNormal = hit.normal; wallTime = Time.time;
                // Remove only velocity entering the wall. Falling and tangent motion survive.
                float into = Vector3.Dot(Velocity, hit.normal);
                if (into < 0) Velocity -= hit.normal * into;
            }
            else if (hit.normal.y < -.55f && Velocity.y > 0)
                Velocity = new Vector3(Velocity.x, 0, Velocity.z);
        }
        public void ApplyImpulse(Vector3 impulse)
        {
            if (zipline) ReleaseZipline(false);
            Velocity+=impulse; impulseUntil=Time.time+.18f;
            jumpTime=groundedTime=wallTime=-100;
        }
        public void Respawn()
        {
            zipline = null; regrabUntil = 0; impulseUntil=0;
            slideAge=-1; slideHopReady=true; wasGrounded=wasSliding=false; SlideHopBonus=0; lastSlideHopAt=-100;
            controller.enabled = false; transform.position = spawn; controller.enabled = true;
            Velocity = Vector3.zero; wallTime = groundedTime = jumpTime = -100;
        }
        void OnGUI()
        {
            if (CombatPlayground.Instance && CombatPlayground.Instance.Running) return;
            if (CombatPlayground.Instance && CombatPlayground.Instance.BossConsoleInReach)
                GUI.Label(new Rect(18,233,470,28),"Press F to start a run, then E to summon the Sentinel.");
            if (RecentSlideHop) GUI.Label(new Rect(18,Screen.height-48,470,28),$"SLIDE HOP  +{SlideHopBonus:0.0} m/s",new GUIStyle(GUI.skin.label) {fontSize=20});
            GUI.Box(new Rect(18, 18, 470, 185), "SWARM / MOVEMENT LAB");
            GUI.Label(new Rect(32, 45, 440, 112), $"Speed: {new Vector2(Velocity.x, Velocity.z).magnitude:0.0} m/s    Best: {BestSpeed:0.0}\nWASD move | Mouse look | Space jump / wall bounce\nCtrl or C slide | R reset | Esc unlock cursor\n{(Sliding ? "SLIDING" : grounded ? "GROUNDED" : "AIRBORNE")} — Find your own line through the canyon.\nF starts combat (host in co-op) — weapon fires when enemies are visible.");
            GUI.Label(new Rect(32,153,440,49),CanyonLayout.Area(transform.position)+"\n"+CanyonLayout.RouteHintAt(transform.position));
            GUI.Label(new Rect(18,207,470,24),IsZiplining ? "ZIP — Space hop / Ctrl or C drop / E release" : "E: grab nearby zip line in your facing direction / interact");
        }
    }
}
