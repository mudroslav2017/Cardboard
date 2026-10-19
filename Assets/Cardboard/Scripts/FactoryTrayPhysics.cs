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
        Vector3 targetPosition,supportVelocity;
        Vector3 previousSupportVelocity;
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
            Active=Carrying=false;OutsideTime=0;RestorePlayerCollision();Freeze(body);
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
            targetPosition=position;targetRotation=rotation;supportVelocity=previousSupportVelocity=Vector3.zero;Active=Carrying=true;OutsideTime=0;
            // Detach once. From here gravity, contact and inertia move the dishes.
            foreach(var item in contents)
            {
                item.transform.SetParent(lunch.transform,true);item.isKinematic=false;item.useGravity=true;item.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                item.linearVelocity=Vector3.zero;item.angularVelocity=Vector3.zero;item.WakeUp();
            }
            trayShapes=GetComponentsInChildren<Collider>().Concat(contents.SelectMany(item=>item.GetComponentsInChildren<Collider>())).ToArray();
            if(CardboardRunner.Player)
            {
                playerShapes=CardboardRunner.Player.GetComponentsInChildren<Collider>();
                foreach(var shape in trayShapes)foreach(var player in playerShapes)if(shape && player)Physics.IgnoreCollision(shape,player,true);
            }
            Physics.SyncTransforms();
        }
        public void Aim(Vector3 position,Quaternion rotation){targetPosition=position;targetRotation=rotation;}
        public void StepSupport()
        {
            if(!Carrying || !body)return;
            body.MovePosition(Vector3.SmoothDamp(body.position,targetPosition,ref supportVelocity,.16f,3,Time.fixedDeltaTime));
            body.MoveRotation(Quaternion.RotateTowards(body.rotation,targetRotation,140*Time.fixedDeltaTime));
        }
        void FixedUpdate(){TickSupport(Time.fixedDeltaTime);}
        public void TickSupport(float dt)
        {
            if(!Active)return;
            if(Carrying)
            {
                StepSupport();
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
            Carrying=false;
            var oldPosition=transform.position;var delta=place.rotation*Quaternion.Inverse(transform.rotation);
            foreach(var item in contents)
            {
                item.position=place.position+delta*(item.position-oldPosition);item.rotation=delta*item.rotation;
                item.linearVelocity=delta*item.linearVelocity;
            }
            body.position=place.position;body.rotation=place.rotation;transform.SetPositionAndRotation(place.position,place.rotation);
            RestorePlayerCollision();
        }
        public void ReleaseTray()
        {
            Carrying=false;Active=true;
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
