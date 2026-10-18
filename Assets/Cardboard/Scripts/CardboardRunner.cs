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
        public bool IsResting => !isPlayer && (Time.time<restUntil || Time.time<paintHoldUntil);
        float cycle, jumpHeight, jumpVelocity;
        float restUntil, nextRest, paintHoldUntil;
        System.Random restRandom;
        Vector3 velocity;
        CharacterController characterController;
        public static CardboardRunner Player;
        public static float PartyUntil, FollowUntil;
        public static int Steps;
        public static string Mood = "Просто картон. Просто идём.";

        void Start()
        {
            if (isPlayer)
            {
                Player=this; Steps=0; PartyUntil=FollowUntil=0; Mood="Просто картон. Просто идём.";
                if (puppet) puppet.gameObject.SetActive(false);
            }
            cycle=personality*1.79f;
            characterController=GetComponent<CharacterController>();
            restRandom=new System.Random(137+personality*7919);
            nextRest=Time.time+3+(float)restRandom.NextDouble()*8;
        }

        void Update()
        {
            float dt=Mathf.Min(Time.deltaTime,.05f);
            if (dt<=0) return;
            var factory=CardboardFactoryGame.Instance;
            if(factory && factory.captureSequence && factory.captureSequence.ControlsWorkers && !isPlayer) return;
            if(factory && (factory.Ended || (isPlayer && factory.PlayerBlocked)))
            { velocity=Vector3.zero;speed=0;Pose(cycle,0);return; }
            bool chasing=factory && factory.State==FactoryState.Chased;
            Vector3 wish=Vector3.zero;
            bool party=Time.time<PartyUntil;
            if (isPlayer)
            {
                var keyboard=Keyboard.current;
                if (keyboard!=null)
                {
                    Vector2 input=new Vector2((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed?1:0)-(keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed?1:0),
                        (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed?1:0)-(keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed?1:0));
                    Vector3 forward=Quaternion.Euler(0,Camera.main?Camera.main.transform.eulerAngles.y:180,0)*Vector3.forward;
                    Vector3 right=Vector3.Cross(Vector3.up,forward);
                    wish=(forward*input.y+right*input.x).normalized*(factory && factory.Pretending?1.35f:keyboard.leftShiftKey.isPressed?6:3.2f);
                    if (keyboard.spaceKey.wasPressedThisFrame && jumpHeight<=.001f) jumpVelocity=5.5f;
                    if (!factory && keyboard.eKey.wasPressedThisFrame) Whistle();
                    if (!factory && keyboard.rKey.wasPressedThisFrame) Party();
                }
            }
            else
            {
                if(Time.time>=nextRest && Time.time>=restUntil) PauseFor(5);
                float a=Time.time*(.17f+personality*.005f)+personality*Mathf.PI*2/11;
                Vector3 target=new Vector3(Mathf.Cos(a)*(7.4f+Mathf.Sin(personality)*1.1f),0,Mathf.Sin(a)*4.8f);
                if (Time.time<FollowUntil && Player)
                    target=Player.transform.position+new Vector3(Mathf.Cos(a*2),0,Mathf.Sin(a*2))*(2.4f+personality*.15f);
                if (party) target=new Vector3(Mathf.Sin(a*4+personality)*8,0,Mathf.Cos(a*3)*5);
                wish=Vector3.ClampMagnitude(target-transform.position,1)*(party?4:1.4f+personality%4*.25f);
                if(factory)
                {
                    target=factory.WorkerTarget(this,out float factorySpeed);
                    wish=Vector3.ClampMagnitude(target-transform.position,1)*factorySpeed;
                }
                if (CardboardWorld.Instance)
                foreach (var other in CardboardWorld.Instance.runners)
                {
                    if (!other || other==this) continue;
                    if(chasing && other.isPlayer) continue;
                    Vector3 separation=transform.position-other.transform.position; separation.y=0;
                    float distance=separation.magnitude, space=other.isPlayer?1.5f:1;
                    if (distance<space && distance>.001f) wish+=separation/distance*(space-distance)*4;
                }
                if(IsResting && !chasing) wish=Vector3.zero;
            }
            if(!isPlayer && Time.time<paintHoldUntil && !chasing) velocity=Vector3.zero;
            velocity=Vector3.Lerp(velocity,wish,1-Mathf.Exp(-9*dt)); speed=velocity.magnitude;
            Vector3 position=transform.position+velocity*dt;
            if(factory) { position.x=Mathf.Clamp(position.x,CardboardFactoryWorld.WorkshopWest+.4f,CardboardFactoryWorld.WorkshopEast-.4f);position.z=Mathf.Clamp(position.z,-26.6f,13.6f); }
            else
            {
                float boundary=position.x*position.x/(10.7f*10.7f)+position.z*position.z/(7.2f*7.2f);
                if (boundary>1) { position.x/=Mathf.Sqrt(boundary); position.z/=Mathf.Sqrt(boundary); }
            }
            if(characterController && characterController.enabled)
            { characterController.Move(position-transform.position);position=transform.position; }
            position.y=0; transform.position=position;
            // Follow the walking direction. Resting sheets keep their world orientation.
            if (!isPlayer && (!IsResting || chasing))
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
            if(isPlayer && factory && factory.station && previousJump>0 && jumpHeight==0)
                factory.station.TryPump(transform.position);
            float oldCycle=cycle;
            cycle+=dt*speed*3;
            if (isPlayer && speed>.2f && Mathf.FloorToInt(oldCycle/Mathf.PI)!=Mathf.FloorToInt(cycle/Mathf.PI)) Steps++;
            Pose(cycle,Mathf.Clamp01(speed/1.5f));
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
        public void HoldForDrawing() { paintHoldUntil=Time.time+.35f; }
        public static void Whistle() { FollowUntil=Time.time+12; Mood="Все картонки идут за тобой."; }
        public static void Party() { PartyUntil=Time.time+10; Mood="Картонный переполох!"; }
    }
}
