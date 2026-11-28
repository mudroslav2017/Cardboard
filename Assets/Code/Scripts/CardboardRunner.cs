using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    public class CardboardRunner : MonoBehaviour
    {
        public bool isPlayer;
        public int personality;
        public Transform puppet;
        public float speed;
        public float JumpHeight => jumpHeight;
        public Vector3 Velocity => velocity;
        public bool InSewer => sewerReturn;
        public bool IsResting => !isPlayer && (Time.time<restUntil || Time.time<paintHoldUntil);
        float cycle, jumpHeight, jumpVelocity;
        float restUntil, nextRest, paintHoldUntil;
        System.Random restRandom;
        Vector3 velocity;
        CharacterController characterController;
        Transform sewerReturn;
        float fallSpeed;
        float sewerFallLimit;
        float surfaceStepOffset;
        public static CardboardRunner Player;
        public static float PartyUntil, FollowUntil;
        public static int Steps;
        public static string Mood = "Just cardboard. Just walking.";

        void Start()
        {
            if (isPlayer)
            {
                Player=this; Steps=0; PartyUntil=FollowUntil=0; Mood="Just cardboard. Just walking.";
                if (puppet) puppet.gameObject.SetActive(false);
            }
            cycle=personality*1.79f;
            characterController=GetComponent<CharacterController>();
            restRandom=new System.Random(137+personality*7919);
            nextRest=Time.time+3+(float)restRandom.NextDouble()*8;
        }

        void Update()
        {
            TickMovement(Mathf.Min(Time.deltaTime,.05f));
        }
        public void TickMovement(float dt)
        {
            if (dt<=0) return;
            var factory=CardboardFactoryGame.Instance;
            var crane = isPlayer && factory ? factory.craneJob : null;
            var production = isPlayer && factory ? factory.production : null;
            if ((crane && (factory.MultiplayerPrototype ? factory.CoopLocalCrane : crane.HoldsPlayer)) ||
                (production && production.HoldsPlayer && (!factory.MultiplayerPrototype || factory.CoopLocalProduction)))
            { velocity = Vector3.zero; speed = jumpHeight = jumpVelocity = 0f; return; }
            bool hallPhysics = crane && !InSewer && crane.InHall(transform.position);
            if(factory && factory.intro && factory.intro.ControlsWorker(this))return;
            if(factory && factory.captureSequence && factory.captureSequence.ControlsWorkers && !isPlayer) return;
            var impact=!isPlayer?GetComponent<FactoryWorkerImpact>():null;
            if(impact && impact.TickReaction(dt)){velocity=Vector3.zero;speed=0;return;}
            if(factory && (factory.Ended || (isPlayer && factory.PlayerBlocked)))
            { velocity=Vector3.zero;speed=0;Pose(cycle,0);return; }
            bool chasing=factory && factory.State==FactoryState.Chased;
            var diners=factory && factory.lunch?factory.lunch.diners:null;
            bool lunchGuided=!isPlayer && !chasing && diners && diners.Guides(this);
            if(lunchGuided && diners.IsEating(this))
            {velocity=Vector3.zero;speed=0;diners.PoseDiner(this);return;}
            Vector3 wish=Vector3.zero;
            bool party=Time.time<PartyUntil;
            bool carryingTray = isPlayer && factory && factory.lunch && factory.lunch.LocalCarrying;
            float traySpeed = 1.3f;
            if (isPlayer)
            {
                var keyboard=Keyboard.current;
                if (keyboard!=null)
                {
                    bool handsBusy = factory && (factory.MultiplayerPrototype ? factory.CoopLocalHands : factory.HandsBusy);
                    Vector2 input=new Vector2((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed?1:0)-(keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed?1:0),
                        (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed?1:0)-(keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed?1:0));
                    Vector3 forward=Quaternion.Euler(0,Camera.main?Camera.main.transform.eulerAngles.y:180,0)*Vector3.forward;
                    Vector3 right=Vector3.Cross(Vector3.up,forward);
                    bool sprint = keyboard.leftShiftKey.isPressed;
                    bool carrySprint = sprint && factory && !factory.MultiplayerPrototype && !factory.Pretending;
                    if (carrySprint) traySpeed = 6f;
                    float moveSpeed = carrySprint ? 6f : carryingTray ? traySpeed :
                        handsBusy ? 1.8f : factory && factory.Pretending ? 1.35f : sprint ? 6f : 3.2f;
                    wish=(forward*input.y+right*input.x).normalized*moveSpeed;
                    if (keyboard.spaceKey.wasPressedThisFrame && !InSewer && jumpHeight<=.001f &&
                        !handsBusy)
                    {
                        if (!hallPhysics) jumpVelocity = 5.5f;
                        else if (characterController && characterController.isGrounded) fallSpeed = 5.5f;
                    }
                    if (!factory && keyboard.eKey.wasPressedThisFrame) Whistle();
                    if (!factory && keyboard.rKey.wasPressedThisFrame) Party();
                }
            }
            else
            {
                if(!lunchGuided && Time.time>=nextRest && Time.time>=restUntil) PauseFor(5);
                float a=Time.time*(.17f+personality*.005f)+personality*Mathf.PI*2/11;
                Vector3 target=new Vector3(Mathf.Cos(a)*(7.4f+Mathf.Sin(personality)*1.1f),0,Mathf.Sin(a)*4.8f);
                if (Time.time<FollowUntil && Player)
                    target=Player.transform.position+new Vector3(Mathf.Cos(a*2),0,Mathf.Sin(a*2))*(2.4f+personality*.15f);
                if (party) target=new Vector3(Mathf.Sin(a*4+personality)*8,0,Mathf.Cos(a*3)*5);
                wish=Vector3.ClampMagnitude(target-transform.position,1)*(party?4:1.4f+personality%4*.25f);
                if(factory)
                {
                    target=factory.WorkerTarget(this,out float factorySpeed);
                    var direction=target-transform.position;
                    wish=Vector3.ClampMagnitude(direction*(lunchGuided?4:1),1)*factorySpeed;
                }
                if (CardboardWorld.Instance)
                foreach (var other in CardboardWorld.Instance.runners)
                {
                    if (!other || other==this) continue;
                    if(chasing && other.isPlayer) continue;
                    Vector3 separation=transform.position-other.transform.position; separation.y=0;
                    float distance=separation.magnitude, space=other.isPlayer?1.5f:(lunchGuided ? .8f : 1);
                    if (distance<space && distance>.001f) wish+=separation/distance*(space-distance)*4;
                }
                if(IsResting && !chasing && !lunchGuided) wish=Vector3.zero;
            }
            if(carryingTray)
            {
                var heading=Quaternion.Euler(0,Camera.main?Camera.main.transform.eulerAngles.y:180,0);
                wish+=heading*Vector3.right*factory.lunch.StaggerVelocity;
            }
            if(!isPlayer && Time.time<paintHoldUntil && !chasing) velocity=Vector3.zero;
            if(factory && !isPlayer)FactoryDoor.OpenForWorker(transform.position,wish);
            velocity=Vector3.Lerp(velocity,wish,1-Mathf.Exp(-9*dt)); speed=velocity.magnitude;
            if(carryingTray)
            {velocity=Vector3.ClampMagnitude(velocity,traySpeed);speed=velocity.magnitude;}
            if (InSewer && characterController && characterController.enabled)
            {
                MoveWithGravity(velocity * dt, dt);
                if (transform.position.y < sewerFallLimit)
                    LeaveSewer();
                return;
            }
            Vector3 position=transform.position+velocity*dt;
            if(factory)
            {
                bool inCanteen=position.z<CardboardFactoryWorld.CanteenNorth+.25f;
                float west=inCanteen?CardboardFactoryWorld.CanteenWest:CardboardFactoryWorld.WorkshopWest;
                float east=inCanteen?CardboardFactoryWorld.CanteenEast:CardboardFactoryWorld.WorkshopEast;
                if(isPlayer && factory.intro && position.z> -3.45f && position.z< -.55f && (position.x>east-.8f || factory.exitDoor.IsOpen))east+=15;
                if (crane && (hallPhysics || crane.CanWalkThrough(position))) west = crane.WestLimit;
                position.x=Mathf.Clamp(position.x,west+.4f,east-.4f);
                if (!hallPhysics)
                    position.z=Mathf.Clamp(position.z,CardboardFactoryWorld.CanteenSouth+.4f,CardboardFactoryWorld.WorkshopNorth-.4f);
            }
            else
            {
                float boundary=position.x*position.x/(10.7f*10.7f)+position.z*position.z/(7.2f*7.2f);
                if (boundary>1) { position.x/=Mathf.Sqrt(boundary); position.z/=Mathf.Sqrt(boundary); }
            }
            if (characterController && characterController.enabled)
            {
                if (hallPhysics)
                {
                    MoveWithGravity(position - transform.position, dt);
                    if (transform.position.y < -2f) PlaceAtWorkAnchor(crane.RecoveryPoint, true);
                }
                else characterController.Move(position - transform.position);
                position = transform.position;
            }
            if (!hallPhysics) position.y = 0f;
            transform.position = position;
            // Follow the walking direction. Resting sheets keep their world orientation.
            if (!isPlayer && (!IsResting || chasing || lunchGuided))
            {
                Vector3 facing=velocity; facing.y=0;
                if (facing.sqrMagnitude>.01f)
                    transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(facing),1-Mathf.Exp(-7*dt));
            }
            float previousJump=jumpHeight;
            if (jumpHeight>0 || jumpVelocity>0)
            {
                jumpVelocity-=17*dt; jumpHeight=Mathf.Max(0,jumpHeight+jumpVelocity*dt);
                if (jumpHeight==0 && jumpVelocity<0) jumpVelocity=0;
            }
            if(isPlayer && factory && !factory.MultiplayerPrototype && factory.station && previousJump>0 && jumpHeight==0)
                factory.station.TryPump(transform.position);
            float oldCycle=cycle;
            cycle+=dt*speed*3;
            if (isPlayer && speed>.2f && Mathf.FloorToInt(oldCycle/Mathf.PI)!=Mathf.FloorToInt(cycle/Mathf.PI)) Steps++;
            Pose(cycle,Mathf.Clamp01(speed/1.5f));
        }

        public void UseSewerAccess(Transform landing, Transform returnLanding = null)
        {
            if (!characterController) characterController = GetComponent<CharacterController>();
            if (returnLanding)
            {
                sewerReturn = returnLanding;
                sewerFallLimit = landing.position.y - 12f;
                surfaceStepOffset = characterController.stepOffset;
                characterController.stepOffset = .32f;
            }
            else
            {
                sewerReturn = null;
                characterController.stepOffset = surfaceStepOffset;
            }
            characterController.enabled = false;
            transform.position = landing.position;
            characterController.enabled = true;
            velocity = Vector3.zero;
            speed = jumpHeight = jumpVelocity = 0;
            fallSpeed = -1f;
        }

        public void LeaveSewer()
        {
            if (InSewer) UseSewerAccess(sewerReturn);
        }

        void MoveWithGravity(Vector3 movement, float dt)
        {
            fallSpeed = Mathf.Max(fallSpeed - 17f * dt, -12f);
            movement.y = fallSpeed * dt;
            var contacts = characterController.Move(movement);
            if ((contacts & CollisionFlags.Below) != 0) fallSpeed = -1f;
            else if ((contacts & CollisionFlags.Above) != 0) fallSpeed = Mathf.Min(fallSpeed, 0f);
        }

        internal void PlaceAtWorkAnchor(Vector3 position, bool allowWalking)
        {
            if (!characterController) characterController = GetComponent<CharacterController>();
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = allowWalking;
            velocity = Vector3.zero;
            speed = fallSpeed = jumpHeight = jumpVelocity = 0f;
        }

        public void Pose(float phase,float walk)
        {
            if (!puppet) return;
            if(!isPlayer && Time.time<paintHoldUntil) return;
            float bounce=isPlayer?0:Mathf.Abs(Mathf.Sin(phase))*.14f*walk;
            puppet.localPosition=new Vector3(0,-.16f+jumpHeight+bounce,0);
            puppet.localRotation=Quaternion.Euler(0,0,Mathf.Sin(phase)*13*walk);
            puppet.localScale=Vector3.one;
        }
        public void PauseFor(float seconds)
        {
            if(isPlayer) return;
            restUntil=Time.time+seconds;
            nextRest=restUntil+7+(restRandom!=null?(float)restRandom.NextDouble()*7:7);
            velocity=Vector3.zero;speed=0;
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var body=hit.rigidbody;
            if(!body || body.isKinematic || hit.moveDirection.y<-.4f)return;
            var prop=body.GetComponent<FactoryPhysicsProp>();if(!prop || prop.IsHeld)return;
            var push=hit.moveDirection;push.y=0;
            body.AddForceAtPosition(push*Mathf.Min(speed,3)*1.8f,hit.point,ForceMode.Force);
        }
        public void HoldForDrawing() { paintHoldUntil=Time.time+.35f; }
        public static void Whistle() { FollowUntil=Time.time+12; Mood="The cardboard crowd is following you."; }
        public static void Party() { PartyUntil=Time.time+10; Mood="Cardboard commotion!"; }
    }
}
