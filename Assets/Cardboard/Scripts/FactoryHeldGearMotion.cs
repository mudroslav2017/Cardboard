using UnityEngine;

namespace Cardboard
{
    [DefaultExecutionOrder(170)]
    public class FactoryHeldGearMotion : MonoBehaviour
    {
        public Vector3 InertialOffset {get;private set;}
        public Vector3 InertialTilt {get;private set;}
        Vector3 home,offsetVelocity,tiltVelocity,previousPosition;
        Quaternion homeRotation,previousRotation;
        Camera lens;
        CardboardCamera walking;
        CardboardFactoryGame game;
        bool initialized,sampled;

        public void Initialize(CardboardFactoryGame owner,Camera camera)
        {
            game=owner;lens=camera;walking=camera.GetComponent<CardboardCamera>();
            home=transform.localPosition;homeRotation=transform.localRotation;initialized=true;
            // Camera-attached meshes must update each frame instead of caching a world instance pose.
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                properties.SetFloat("_Cull",0);renderer.SetPropertyBlock(properties);
            }
            ResetInertia();
        }
        void LateUpdate(){RefreshPose(Time.unscaledDeltaTime);}
        public void RefreshPose(float dt)
        {
            if(!initialized || !lens)return;
            if(game && (game.MenuActive || game.CaptureActive || game.Ended))
            {ResetInertia();return;}
            var position=lens.transform.position;var rotation=lens.transform.rotation;
            if(!sampled || dt<=0 || dt>.15f || Vector3.Distance(position,previousPosition)>1.5f ||
                Quaternion.Angle(previousRotation,rotation)>55)
            {
                ResetInertia();previousPosition=position;previousRotation=rotation;sampled=true;return;
            }
            var velocity=Quaternion.Inverse(rotation)*(position-previousPosition)/dt;
            var angles=(Quaternion.Inverse(previousRotation)*rotation).eulerAngles;
            var angularVelocity=new Vector3(Mathf.DeltaAngle(0,angles.x),Mathf.DeltaAngle(0,angles.y),Mathf.DeltaAngle(0,angles.z))/dt;
            previousPosition=position;previousRotation=rotation;
            TickInertia(dt,velocity,angularVelocity,walking?walking.WalkingOffset:Vector3.zero);
        }
        public void TickInertia(float dt,Vector3 localVelocity,Vector3 angularVelocity,Vector3 walkingOffset)
        {
            if(!initialized)return;
            dt=Mathf.Clamp(dt,0,.05f);if(dt<=0)return;
            var target=new Vector3(-localVelocity.x*.0012f-angularVelocity.y*.000035f,
                -localVelocity.y*.0012f+angularVelocity.x*.000035f,-localVelocity.z*.0013f);
            target-=new Vector3(walkingOffset.x,walkingOffset.y,0)*.14f;
            // Six times the inventory's positional and angular excursion, with a heavier return.
            target=new Vector3(Mathf.Clamp(target.x,-.006f,.006f),Mathf.Clamp(target.y,-.005f,.005f),Mathf.Clamp(target.z,-.008f,.008f))*9;
            var tilt=new Vector3(-angularVelocity.x*.008f+localVelocity.z*.12f,
                -angularVelocity.y*.008f-localVelocity.x*.18f,-angularVelocity.z*.006f-localVelocity.x*.1f-angularVelocity.y*.004f);
            tilt=new Vector3(Mathf.Clamp(tilt.x,-1.6f,1.6f),Mathf.Clamp(tilt.y,-1.8f,1.8f),Mathf.Clamp(tilt.z,-1.2f,1.2f))*6;
            InertialOffset=Vector3.SmoothDamp(InertialOffset,target,ref offsetVelocity,.23f,Mathf.Infinity,dt);
            InertialTilt=Vector3.SmoothDamp(InertialTilt,tilt,ref tiltVelocity,.25f,Mathf.Infinity,dt);
            transform.localPosition=home+InertialOffset;
            transform.localRotation=homeRotation*Quaternion.Euler(InertialTilt);
        }
        public void ResetInertia()
        {
            InertialOffset=InertialTilt=offsetVelocity=tiltVelocity=Vector3.zero;sampled=false;
            if(initialized){transform.localPosition=home;transform.localRotation=homeRotation;}
        }
        void OnEnable(){ResetInertia();}
        void OnDisable(){ResetInertia();}
    }
}
