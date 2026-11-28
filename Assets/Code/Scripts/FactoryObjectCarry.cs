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
        FactoryStapler stapler;
        int pickupFrame;
        public string Prompt => stapler ? string.Empty :
            Held && Held.instructionSheet ? "E - FLIP · LMB - THROW · RMB - PUT DOWN" : "LMB - THROW · RMB - PUT DOWN";
        public static float ThrowSpeed(float mass,bool instruction=false)
        {return instruction?12:Mathf.Lerp(11,8,Mathf.InverseLerp(.5f,5,mass));}
        public void ItemReceived(){pickupFrame=Time.frameCount;}
        public bool Take(FactoryPhysicsProp prop)
        {
            if(Held || !prop || !game.ActionLens || game.HandsBusy || game.HasPart ||
                (!game.MultiplayerPrototype && game.PlayerBlocked) || game.CaptureActive)return false;
            if(prop.TryGetComponent(out FactoryKitchenTool kitchenTool))kitchenTool.Undock();
            Held=prop;ItemReceived();prop.BeginCarry();
            if(CardboardMarker.Instance)CardboardMarker.Instance.SetDrawingMode(false);
            if(prop.TryGetComponent(out stapler))
            {
                stapler.BeginGrip(game.ActionLens);
                return true;
            }
            var body=prop.Body;
            // Sample physics coordinates: interpolated render transforms can lag behind the body.
            localCenter=Quaternion.Inverse(body.rotation)*(body.worldCenterOfMass-body.position);
            float radius=0;foreach(var shape in prop.GetComponentsInChildren<Collider>())
                radius=Mathf.Max(radius,shape.bounds.extents.magnitude);
            HoldDistance=prop.instructionSheet?.72f:Mathf.Clamp(1.05f+radius*.8f,1.2f,2);
            if(prop.instructionSheet && game.instruction)game.instruction.MarkPickedUp();
            gripRotation=prop.instructionSheet?Quaternion.identity:Quaternion.Inverse(game.ActionLens.rotation)*body.rotation;
            if(!game.kitchen || !game.kitchen.OwnsHeld(prop))game.Toast(Prompt,5);
            return true;
        }
        void Update()
        {
            if(game.MultiplayerPrototype)return;
            if(game.FilmActive)return;if(!Held && !game.HasPart){return;}
            if(game.CaptureActive || game.Ended){if(Held)Release(false);return;}
            if(game.MenuActive || game.PlayerBlocked || CardboardMarker.DrawingMode){return;}
            if(Time.frameCount==pickupFrame)return;
            var mouse=Mouse.current;if(mouse==null){return;}
            if(stapler && (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked)) return;
            if(Held && Held.GetComponent<FactoryKitchenUtensil>())return;
            bool kitchenTool=Held && Held.GetComponent<FactoryKitchenTool>();
            TickInput(!kitchenTool && mouse.leftButton.wasPressedThisFrame,mouse.rightButton.wasPressedThisFrame);
        }
        public void TickInput(bool throwPressed,bool putDownPressed)
        {
            if(!Held && !game.HasPart)return;
            if(putDownPressed)
            {if(Held)Release(false);else game.ReleasePart(false);return;}
            if(throwPressed)
            {
                if(stapler)stapler.Press();
                else if(Held)Release(true);
                else game.ReleasePart(true);
            }
        }
        public void FlipInstruction(bool reverse)
        {if(Held && Held.instructionSheet)gripRotation=Quaternion.Euler(0,reverse?180:0,0);}
        void FixedUpdate(){TickGrip();}
        void LateUpdate()
        {
            if(!stapler || !game.ActionLens || game.PlayerBlocked || game.CaptureActive || game.CameraOverride) return;
            stapler.AdvanceGrip(game.ActionLens, Time.deltaTime);
            TargetPosition = stapler.transform.position;
            TargetRotation = stapler.transform.rotation;
        }
        public void TickGrip()
        {
            if(!Held || stapler || !game.ActionLens || Time.timeScale<=0)return;
            var lens=game.ActionLens;var body=Held.Body;
            TargetRotation=lens.rotation*gripRotation;
            Vector3 center=lens.TransformPoint(new Vector3(0,Held.instructionSheet?-.075f:-.20f,HoldDistance));
            bool utensilHeld=Held.TryGetComponent(out FactoryKitchenUtensil utensil);
            if(utensilHeld)
            {
                if(!game.MultiplayerPrototype && (game.PlayerBlocked || game.CameraOverride) || game.CaptureActive)return;
                utensil.SampleVelocity();
                utensil.GripPose(lens,out center,out var orientation);
                TargetRotation=orientation;
            }
            else if(Held.TryGetComponent(out FactoryKitchenTool kitchenTool))
            {
                Quaternion heading=Quaternion.Euler(0,lens.eulerAngles.y,0);
                float height=kitchenTool.Stockpot?-.49f:kitchenTool.pan?-.39f:-.53f;
                center=lens.position+heading*new Vector3(0,height,1.02f);
                TargetRotation=heading*Quaternion.Euler(kitchenTool.CarryPitch,0,0);
            }
            TargetPosition=utensilHeld?center:center-TargetRotation*localCenter;
            Vector3 wish=Vector3.ClampMagnitude((TargetPosition-body.position)*(utensilHeld?22f:13f),12);
            Vector3 acceleration=Vector3.ClampMagnitude((wish-body.linearVelocity)*(utensilHeld?22f:13f),utensilHeld?155:95);
            body.AddForce(acceleration,ForceMode.Acceleration);
            var delta=TargetRotation*Quaternion.Inverse(body.rotation);
            if(delta.w<0)delta=new Quaternion(-delta.x,-delta.y,-delta.z,-delta.w);
            delta.ToAngleAxis(out float angle,out Vector3 axis);
            if(axis.sqrMagnitude>.001f)
            {
                bool steadyUtensil=utensilHeld && utensil.Tool!=FactoryKitchenUtensil.Kind.Knife;
                float angularFollow=steadyUtensil?24f:11f;
                Vector3 angularWish=axis*(Mathf.DeltaAngle(0,angle)*Mathf.Deg2Rad*angularFollow);
                body.AddTorque(Vector3.ClampMagnitude((angularWish-body.angularVelocity)*angularFollow,steadyUtensil?330f:100f),ForceMode.Acceleration);
            }
            // A blocked object stays against the wall; walking away releases the grip.
            if(Vector3.Distance(body.worldCenterOfMass,lens.position)>3.6f)Release(false);
        }
        public void Release(bool thrown)
        {
            if(!Held)return;
            if(game.kitchen && game.kitchen.OwnsHeld(Held))game.kitchen.CancelFlick();
            var prop=Held;Held=null;
            EndStaplerGrip();
            Vector3 velocity=Vector3.ClampMagnitude(prop.Body.linearVelocity,.55f);
            if(thrown)
            {
                float speed=ThrowSpeed(prop.Body.mass,prop.instructionSheet);
                velocity=(game.ActionLens ? game.ActionLens.forward : Vector3.forward)*speed+
                    (!game.MultiplayerPrototype && CardboardRunner.Player?CardboardRunner.Player.Velocity*.35f:Vector3.zero);
            }
            prop.Release(velocity,thrown);
            if(prop.TryGetComponent(out FactoryKitchenUtensil utensil))utensil.Rest();
            if(game.instruction && prop.instructionSheet)game.instruction.Released();
        }
        public void Clear()
        {
            EndStaplerGrip();
            if(Held){Held.IsHeld=false;Held=null;}
        }
        void EndStaplerGrip()
        {
            if(stapler)stapler.EndGrip();
            stapler=null;
        }
        void OnDisable(){if(Application.isPlaying)Release(false);}
    }
}
