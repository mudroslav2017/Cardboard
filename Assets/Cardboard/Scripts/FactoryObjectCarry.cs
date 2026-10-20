using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    public class FactoryObjectCarry : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public FactoryPhysicsProp Held {get;private set;}
        public Vector3 TargetPosition {get;private set;}
        public Quaternion TargetRotation {get;private set;}
        public float HoldDistance {get;private set;}
        Quaternion gripRotation;
        Vector3 localCenter;
        int pickupFrame;
        public string Prompt => Held && Held.instructionSheet?"E - FLIP · LMB - THROW · RMB - PUT DOWN":"LMB - THROW · RMB - PUT DOWN";
        public static float ThrowSpeed(float mass,bool instruction=false)
        {return instruction?12:Mathf.Lerp(11,8,Mathf.InverseLerp(.5f,5,mass));}
        public void ItemReceived(){pickupFrame=Time.frameCount;}
        public bool Take(FactoryPhysicsProp prop)
        {
            if(Held || !prop || !Camera.main || game.HandsBusy || game.HasPart || game.PlayerBlocked || game.CaptureActive)return false;
            Held=prop;ItemReceived();prop.BeginCarry();
            if(CardboardMarker.Instance)CardboardMarker.Instance.SetDrawingMode(false);
            var body=prop.Body;
            // Sample physics coordinates: interpolated render transforms can lag behind the body.
            localCenter=Quaternion.Inverse(body.rotation)*(body.worldCenterOfMass-body.position);
            float radius=0;foreach(var shape in prop.GetComponentsInChildren<Collider>())
                radius=Mathf.Max(radius,shape.bounds.extents.magnitude);
            HoldDistance=prop.instructionSheet?.72f:Mathf.Clamp(1.05f+radius*.8f,1.2f,2);
            if(prop.instructionSheet && game.instruction)game.instruction.MarkPickedUp();
            gripRotation=prop.instructionSheet?Quaternion.identity:Quaternion.Inverse(Camera.main.transform.rotation)*body.rotation;
            game.Toast(Prompt,5);
            return true;
        }
        void Update()
        {
            if(!Held && !game.HasPart){return;}
            if(game.CaptureActive || game.Ended){if(Held)Release(false);return;}
            if(game.MenuActive || game.PlayerBlocked || CardboardMarker.DrawingMode){return;}
            if(Time.frameCount==pickupFrame)return;
            var mouse=Mouse.current;if(mouse==null){return;}
            TickInput(mouse.leftButton.wasPressedThisFrame,mouse.rightButton.wasPressedThisFrame);
        }
        public void TickInput(bool throwPressed,bool putDownPressed)
        {
            if(!Held && !game.HasPart)return;
            if(putDownPressed)
            {if(Held)Release(false);else game.ReleasePart(false);return;}
            if(throwPressed)
            {if(Held)Release(true);else game.ReleasePart(true);}
        }
        public void FlipInstruction(bool reverse)
        {if(Held && Held.instructionSheet)gripRotation=Quaternion.Euler(0,reverse?180:0,0);}
        void FixedUpdate(){TickGrip();}
        public void TickGrip()
        {
            if(!Held || !Camera.main || Time.timeScale<=0)return;
            var lens=Camera.main.transform;var body=Held.Body;
            TargetRotation=lens.rotation*gripRotation;
            Vector3 center=lens.TransformPoint(new Vector3(0,Held.instructionSheet?-.075f:-.20f,HoldDistance));
            TargetPosition=center-TargetRotation*localCenter;
            Vector3 wish=Vector3.ClampMagnitude((TargetPosition-body.position)*13,12);
            Vector3 acceleration=Vector3.ClampMagnitude((wish-body.linearVelocity)*13,95);
            body.AddForce(acceleration,ForceMode.Acceleration);
            var delta=TargetRotation*Quaternion.Inverse(body.rotation);
            if(delta.w<0)delta=new Quaternion(-delta.x,-delta.y,-delta.z,-delta.w);
            delta.ToAngleAxis(out float angle,out Vector3 axis);
            if(axis.sqrMagnitude>.001f)
            {
                Vector3 angularWish=axis*(Mathf.DeltaAngle(0,angle)*Mathf.Deg2Rad*11);
                body.AddTorque(Vector3.ClampMagnitude((angularWish-body.angularVelocity)*11,100),ForceMode.Acceleration);
            }
            // A blocked object stays against the wall; walking away releases the grip.
            if(Vector3.Distance(body.worldCenterOfMass,lens.position)>3.6f)Release(false);
        }
        public void Release(bool thrown)
        {
            if(!Held)return;
            var prop=Held;Held=null;
            Vector3 velocity=Vector3.ClampMagnitude(prop.Body.linearVelocity,.55f);
            if(thrown)
            {
                float speed=ThrowSpeed(prop.Body.mass,prop.instructionSheet);
                velocity=Camera.main.transform.forward*speed+(CardboardRunner.Player?CardboardRunner.Player.Velocity*.35f:Vector3.zero);
            }
            prop.Release(velocity,thrown);
            if(game.instruction && prop.instructionSheet)game.instruction.Released();
        }
        public void Clear()
        {if(Held){Held.IsHeld=false;Held=null;}}
        void OnDisable(){if(Application.isPlaying)Release(false);}
    }
}
