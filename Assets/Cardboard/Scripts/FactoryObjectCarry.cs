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
        float chargeTime;
        public const float MaxChargeSeconds=1.25f;
        public bool Charging {get;private set;}
        public float ThrowCharge => Mathf.Clamp01(chargeTime/MaxChargeSeconds);
        public string Prompt => Held && Held.instructionSheet?"E — перевернуть · удерживайте ЛКМ — бросить · ПКМ — положить":"Удерживайте ЛКМ — бросить · ПКМ — положить";
        public static float ThrowSpeed(float charge,float mass,bool instruction=false)
        {
            float maximum=instruction?12:Mathf.Lerp(11,8,Mathf.InverseLerp(.5f,5,mass));
            return Mathf.Lerp(.65f,maximum,Mathf.SmoothStep(0,1,Mathf.Clamp01(charge)));
        }
        public void ItemReceived(){CancelThrow();pickupFrame=Time.frameCount;}
        public void CancelThrow(){Charging=false;chargeTime=0;}

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
            HoldDistance=prop.instructionSheet?1.02f:Mathf.Clamp(1.05f+radius*.8f,1.2f,2);
            gripRotation=prop.instructionSheet?Quaternion.identity:Quaternion.Inverse(Camera.main.transform.rotation)*body.rotation;
            game.Toast(Prompt,5);
            return true;
        }
        void Update()
        {
            if(!Held && !game.HasPart){CancelThrow();return;}
            if(game.CaptureActive || game.Ended){CancelThrow();if(Held)Release(false);return;}
            if(game.MenuActive || game.PlayerBlocked || CardboardMarker.DrawingMode){CancelThrow();return;}
            if(Time.frameCount==pickupFrame)return;
            var mouse=Mouse.current;if(mouse==null){CancelThrow();return;}
            TickInput(Time.deltaTime,mouse.leftButton.wasPressedThisFrame,mouse.leftButton.isPressed,
                mouse.leftButton.wasReleasedThisFrame,mouse.rightButton.wasPressedThisFrame);
        }
        public void TickInput(float dt,bool press,bool hold,bool release,bool putDown)
        {
            if(!Held && !game.HasPart){CancelThrow();return;}
            if(putDown)
            {
                CancelThrow();if(Held)Release(false);else game.ReleasePart(false);return;
            }
            if(press){Charging=true;chargeTime=0;}
            if(!Charging)return;
            if(hold)chargeTime=Mathf.Min(MaxChargeSeconds,chargeTime+Mathf.Max(0,dt));
            if(release || !hold)
            {
                float strength=ThrowCharge;CancelThrow();
                if(Held)Release(true,strength);else game.ReleasePart(true,strength);
            }
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
        public void Release(bool thrown,float strength=1)
        {
            if(!Held)return;
            var prop=Held;Held=null;CancelThrow();
            Vector3 velocity=Vector3.ClampMagnitude(prop.Body.linearVelocity,.55f);
            if(thrown)
            {
                float speed=ThrowSpeed(strength,prop.Body.mass,prop.instructionSheet);
                velocity=Camera.main.transform.forward*speed+(CardboardRunner.Player?CardboardRunner.Player.Velocity*.35f:Vector3.zero);
            }
            prop.Release(velocity,thrown);
            if(game.instruction && prop.instructionSheet)game.instruction.Released();
        }
        public void Clear()
        {CancelThrow();if(Held){Held.IsHeld=false;Held=null;}}
        void OnDisable(){CancelThrow();if(Application.isPlaying)Release(false);}
    }
}
