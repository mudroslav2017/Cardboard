using UnityEngine;
using System.Linq;

namespace Cardboard
{
    // The tray is a moving solid support; its contents are independent dynamic bodies.
    [DefaultExecutionOrder(20)]
    public class FactoryTrayPhysics : MonoBehaviour
    {
        public FactoryLunch lunch;
        public Rigidbody body;
        public Rigidbody[] contents;
        public Vector3[] homes;
        public Quaternion[] rotations;
        public Vector2 halfSize=new Vector2(.36f,.23f);
        public bool Active {get;private set;}
        public bool Carrying {get;private set;}
        public float OutsideTime {get;private set;}
        Collider[] playerShapes;
        Collider[] trayShapes;
        Quaternion targetRotation;
        Vector3 targetPosition,previousTargetPosition,supportVelocity;
        Vector3 previousSupportVelocity;
        float balanceInput,supportMassRatio=1,clickDirection,clickRemaining,correctionRemaining;
        Vector3 supportOffset;
        Quaternion Heading=>Quaternion.Euler(0,targetRotation.eulerAngles.y,0);
        public float RollDegrees
        {
            get{var up=Quaternion.Inverse(Heading)*(body.rotation*Vector3.up);return Mathf.Atan2(-up.x,up.y)*Mathf.Rad2Deg;}
        }
        public float PitchDegrees
        {
            get{var up=Quaternion.Inverse(Heading)*(body.rotation*Vector3.up);return Mathf.Atan2(up.z,up.y)*Mathf.Rad2Deg;}
        }
        public float RollSpeed=>Vector3.Dot(body.angularVelocity,Heading*Vector3.forward)*Mathf.Rad2Deg;
        public void SetBalanceInput(float input){balanceInput=Mathf.Clamp(input,-1,1);}
        public void PressBalance(float direction)
        {
            if(!Carrying || Mathf.Abs(direction)<.5f)return;
            clickDirection=Mathf.Sign(direction);clickRemaining=.24f;
            // Keep a tap until physics consumes it, even if press and release happen between fixed steps.
            correctionRemaining=Mathf.Abs(RollDegrees)>2 && RollDegrees*clickDirection>0?.9f:0;
        }
        void ClearBalanceInput(){balanceInput=clickDirection=clickRemaining=correctionRemaining=0;}
        void Awake()
        {
            if(!body)body=GetComponent<Rigidbody>();trayShapes=GetComponentsInChildren<Collider>(true);
            body.solverIterations=16;body.solverVelocityIterations=8;
            foreach(var item in contents)if(item){item.solverIterations=16;item.solverVelocityIterations=8;}
        }
        static void Freeze(Rigidbody item)
        {
            if(!item)return;
            if(!item.isKinematic){item.linearVelocity=Vector3.zero;item.angularVelocity=Vector3.zero;}
            item.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;item.isKinematic=true;item.useGravity=false;
        }
        public void ResetContents()
        {
            Active=Carrying=false;OutsideTime=0;ClearBalanceInput();RestorePlayerCollision();Freeze(body);
            for(int i=0;i<contents.Length;i++)
            {
                var item=contents[i];if(!item)continue;Freeze(item);
                item.transform.SetParent(transform,false);item.transform.localPosition=homes[i];item.transform.localRotation=rotations[i];
                item.gameObject.SetActive(true);
            }
        }
        public void Begin(Vector3 position,Quaternion rotation)
        {
            Freeze(body);transform.SetPositionAndRotation(position,rotation);body.position=position;body.rotation=rotation;
            targetPosition=previousTargetPosition=position;targetRotation=rotation;supportVelocity=previousSupportVelocity=Vector3.zero;Active=Carrying=true;OutsideTime=0;ClearBalanceInput();
            body.isKinematic=false;body.useGravity=true;body.constraints=RigidbodyConstraints.None;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.linearDamping=.3f;body.angularDamping=1;body.maxAngularVelocity=6;
            body.WakeUp();
            // Detach once. From here gravity, contact and inertia move the dishes.
            foreach(var item in contents)
            {
                item.transform.SetParent(lunch.transform,true);item.isKinematic=false;item.useGravity=true;item.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                item.linearVelocity=Vector3.zero;item.angularVelocity=Vector3.zero;item.WakeUp();
            }
            Physics.SyncTransforms();
            float totalMass=body.mass;Vector3 loadedCenter=body.worldCenterOfMass*body.mass;
            foreach(var item in contents){totalMass+=item.mass;loadedCenter+=item.worldCenterOfMass*item.mass;}
            supportMassRatio=totalMass/body.mass;
            supportOffset=Quaternion.Inverse(body.rotation)*(loadedCenter/totalMass-body.position);supportOffset.y=.015f;
            trayShapes=GetComponentsInChildren<Collider>().Concat(contents.SelectMany(item=>item.GetComponentsInChildren<Collider>())).ToArray();
            if(CardboardRunner.Player)
            {
                playerShapes=CardboardRunner.Player.GetComponentsInChildren<Collider>();
                foreach(var shape in trayShapes)foreach(var player in playerShapes)if(shape && player)Physics.IgnoreCollision(shape,player,true);
            }
            Physics.SyncTransforms();
        }
        public void Aim(Vector3 position,Quaternion rotation){targetPosition=position;targetRotation=rotation;}
        public void StepSupport(float dt)
        {
            if(!Carrying || !body || dt<=0)return;
            // Hands supply a soft force in all three axes; collisions and inertia remain physical.
            Vector3 targetVelocity=Vector3.ClampMagnitude((targetPosition-previousTargetPosition)/dt,5);
            previousTargetPosition=targetPosition;
            Vector3 acceleration=(targetPosition-body.position)*160+(targetVelocity-body.linearVelocity)*24-Physics.gravity*supportMassRatio;
            body.AddForceAtPosition(Vector3.ClampMagnitude(acceleration,100),body.position+body.rotation*supportOffset,ForceMode.Acceleration);
            Vector3 forward=Heading*Vector3.forward;
            var error=targetRotation*Quaternion.Inverse(body.rotation);
            if(error.w<0)error=new Quaternion(-error.x,-error.y,-error.z,-error.w);
            error.ToAngleAxis(out float angle,out Vector3 axis);
            Vector3 correction=axis.sqrMagnitude>.001f?axis*(Mathf.DeltaAngle(0,angle)*Mathf.Deg2Rad):Vector3.zero;
            // Gently follow heading and pitch. Roll is free and must be balanced by the player.
            Vector3 torque=Vector3.ProjectOnPlane(correction,forward)*28-Vector3.ProjectOnPlane(body.angularVelocity,forward)*8;
            float difficulty=lunch?Mathf.SmoothStep(0,1,(lunch.CarryTime-1.5f)/4):0;
            float sway=lunch?(.30f*Mathf.Sin(lunch.CarryTime*1.45f)-lunch.Tilt*.55f)*difficulty:0;
            float input=Mathf.Abs(balanceInput)>.01f?balanceInput:clickRemaining>0?clickDirection:0;
            float rollSpeed=Vector3.Dot(body.angularVelocity,forward);
            // A held button asks for a modest roll speed; feedback also brakes existing momentum.
            float rollDrive=Mathf.Abs(input)>.01f?Mathf.Clamp((-input*.55f-rollSpeed)*6,-4,4):0;
            if(correctionRemaining>0)
                rollDrive=Mathf.Clamp(-RollDegrees*Mathf.Deg2Rad*48-rollSpeed*13,-8,8);
            body.AddTorque(torque+forward*(rollDrive+sway),ForceMode.Acceleration);
            clickRemaining=Mathf.Max(0,clickRemaining-dt);correctionRemaining=Mathf.Max(0,correctionRemaining-dt);
            supportVelocity=body.linearVelocity;
        }
        void FixedUpdate(){TickSupport(Time.fixedDeltaTime);}
        public void TickSupport(float dt)
        {
            if(!Active)return;
            if(Carrying)
            {
                StepSupport(dt);
                var acceleration=Vector3.ClampMagnitude((supportVelocity-previousSupportVelocity)/Mathf.Max(dt,.001f),20);
                previousSupportVelocity=supportVelocity;
                bool outside=false;
                foreach(var item in contents)
                {
                    if(!item)continue;
                    // Use physics poses, not the previous interpolated render pose.
                    Vector3 local=Quaternion.Inverse(body.rotation)*(item.worldCenterOfMass-body.position);
                    if(Mathf.Abs(local.x)<halfSize.x+.04f && Mathf.Abs(local.z)<halfSize.y+.04f && local.y>-.05f && local.y<.4f)
                    {
                        float assistance=1-Mathf.InverseLerp(18,42,Vector3.Angle(body.rotation*Vector3.up,Vector3.up));
                        Vector3 up=body.rotation*Vector3.up;
                        var relative=Vector3.ProjectOnPlane(supportVelocity-item.linearVelocity,up);
                        item.AddForce((acceleration*.85f+Vector3.ClampMagnitude(relative*8,12)
                            -Vector3.ProjectOnPlane(Physics.gravity,up)*.75f)*assistance,ForceMode.Acceleration);
                    }
                    if(Mathf.Abs(local.x)>halfSize.x+.065f || Mathf.Abs(local.z)>halfSize.y+.065f || local.y<-.12f)outside=true;
                }
                OutsideTime=outside?OutsideTime+dt:0;
                if(OutsideTime>.65f && lunch)lunch.Spill();
            }
            else if(playerShapes!=null)
            {
                bool overlap=false;
                foreach(var shape in trayShapes)foreach(var player in playerShapes)
                    if(shape && player && shape.bounds.Intersects(player.bounds))overlap=true;
                if(!overlap)RestorePlayerCollision();
            }
        }
        public void Park(Transform place)
        {
            Carrying=false;ClearBalanceInput();
            var oldPosition=transform.position;var delta=place.rotation*Quaternion.Inverse(transform.rotation);
            foreach(var item in contents)
            {
                item.position=place.position+delta*(item.position-oldPosition);item.rotation=delta*item.rotation;
                item.linearVelocity=delta*item.linearVelocity;
            }
            body.position=place.position;body.rotation=place.rotation;transform.SetPositionAndRotation(place.position,place.rotation);
            body.linearVelocity=body.angularVelocity=Vector3.zero;
            RestorePlayerCollision();
        }
        public void ReleaseTray()
        {
            Carrying=false;Active=true;ClearBalanceInput();
            body.isKinematic=false;body.useGravity=true;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.WakeUp();
        }
        void RestorePlayerCollision()
        {
            if(playerShapes!=null && trayShapes!=null)
                foreach(var shape in trayShapes)foreach(var player in playerShapes)if(shape && player)Physics.IgnoreCollision(shape,player,false);
            playerShapes=null;
        }
        void OnDisable(){RestorePlayerCollision();}
    }
}
