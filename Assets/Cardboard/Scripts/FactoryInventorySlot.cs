using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cardboard
{
    [DefaultExecutionOrder(180)]
    public class FactoryInventorySlot : MonoBehaviour
    {
        public const int Layer=11;
        public CardboardFactoryGame game;
        public Camera lens,overlay;
        public Transform visuals,marker;
        public FactoryMarkerPalette palette;
        public MeshRenderer glow,outline;
        public Vector3 markerHome;
        public float Selection {get;private set;}
        public bool Visible => visuals && visuals.gameObject.activeSelf;
        public Vector3 InertialOffset {get;private set;}
        public Vector3 InertialTilt {get;private set;}
        MaterialPropertyBlock properties,outlineProperties;
        Vector3 offsetVelocity,tiltVelocity,previousPosition;
        Quaternion previousRotation;
        bool sampledCamera;
        CardboardCamera cameraMotion;

        void LateUpdate()
        {
            bool visible=game && !game.MenuActive && !game.CaptureActive && !game.Ended;
            if(visible)SampleCameraMotion(Time.unscaledDeltaTime);else ResetInertia();
            Pose(Time.unscaledDeltaTime,visible,CardboardMarker.DrawingMode);
        }
        void SampleCameraMotion(float dt)
        {
            var position=lens.transform.position;var rotation=lens.transform.rotation;
            if(!sampledCamera || dt<=0 || dt>.15f || Vector3.Distance(position,previousPosition)>1.5f ||
                Quaternion.Angle(previousRotation,rotation)>55)
            {
                ResetInertia();previousPosition=position;previousRotation=rotation;sampledCamera=true;return;
            }
            Vector3 velocity=Quaternion.Inverse(rotation)*(position-previousPosition)/dt;
            Vector3 angles=(Quaternion.Inverse(previousRotation)*rotation).eulerAngles;
            Vector3 angularVelocity=new Vector3(Mathf.DeltaAngle(0,angles.x),Mathf.DeltaAngle(0,angles.y),Mathf.DeltaAngle(0,angles.z))/dt;
            previousPosition=position;previousRotation=rotation;
            if(!cameraMotion)cameraMotion=lens.GetComponent<CardboardCamera>();
            TickInertia(dt,velocity,angularVelocity,cameraMotion?cameraMotion.WalkingOffset:Vector3.zero);
        }
        public void TickInertia(float dt,Vector3 localVelocity,Vector3 angularVelocity,Vector3 walkingOffset)
        {
            dt=Mathf.Clamp(dt,0,.05f);if(dt<=0)return;
            var target=new Vector3(-localVelocity.x*.0012f-angularVelocity.y*.000035f,
                -localVelocity.y*.0012f+angularVelocity.x*.000035f,-localVelocity.z*.0013f);
            target-=new Vector3(walkingOffset.x,walkingOffset.y,0)*.14f;
            target=new Vector3(Mathf.Clamp(target.x,-.006f,.006f),Mathf.Clamp(target.y,-.005f,.005f),Mathf.Clamp(target.z,-.008f,.008f));
            target*=1.5f;
            var tilt=new Vector3(-angularVelocity.x*.008f+localVelocity.z*.12f,
                -angularVelocity.y*.008f-localVelocity.x*.18f,-angularVelocity.z*.006f-localVelocity.x*.1f-angularVelocity.y*.004f);
            tilt=new Vector3(Mathf.Clamp(tilt.x,-1.6f,1.6f),Mathf.Clamp(tilt.y,-1.8f,1.8f),Mathf.Clamp(tilt.z,-1.2f,1.2f));
            InertialOffset=Vector3.SmoothDamp(InertialOffset,target,ref offsetVelocity,.17f,Mathf.Infinity,dt);
            InertialTilt=Vector3.SmoothDamp(InertialTilt,tilt,ref tiltVelocity,.20f,Mathf.Infinity,dt);
        }
        public void ResetInertia()
        {
            InertialOffset=InertialTilt=offsetVelocity=tiltVelocity=Vector3.zero;sampledCamera=false;
        }
        public void Layout()
        {
            if(!lens || !overlay || !visuals)return;
            overlay.aspect=lens.aspect;
            const float depth=.65f;
            float halfHeight=depth*Mathf.Tan(overlay.fieldOfView*Mathf.Deg2Rad*.5f);
            // A fixed fraction of screen height keeps the slot stable when FOV or aspect changes.
            float size=halfHeight*.53f/1.5f;
            visuals.localScale=Vector3.one*size;
            float halfWidth=halfHeight*lens.aspect;
            // Keep a pixel margin for the thicker outline and the small inertial excursion.
            var padding=new Vector2(2*halfWidth*20/Mathf.Max(1,lens.pixelWidth),2*halfHeight*20/Mathf.Max(1,lens.pixelHeight));
            var home=new Vector3(halfWidth-size*.86f-padding.x,-halfHeight+size*.70f+padding.y,depth);
            // Apply drag in the viewer-facing plane so depth motion does not pull the corner off screen.
            var facing=Quaternion.LookRotation(home,Vector3.up);
            visuals.localPosition=home+facing*InertialOffset;
            visuals.localRotation=facing*Quaternion.Euler(3,9,3)*Quaternion.Euler(InertialTilt);
            if(outline)
            {
                if(outlineProperties==null)outlineProperties=new MaterialPropertyBlock();
                outlineProperties.SetVector("_PixelStep",new Vector4(2f/Mathf.Max(1,lens.pixelWidth),2f/Mathf.Max(1,lens.pixelHeight),0,0));
                outline.SetPropertyBlock(outlineProperties);
            }
        }
        public void Pose(float dt,bool visible,bool selected)
        {
            if(!visible)ResetInertia();
            Layout();
            Selection=Mathf.Lerp(Selection,visible && selected?1:0,1-Mathf.Exp(-12*Mathf.Max(0,dt)));
            visuals.gameObject.SetActive(visible);overlay.enabled=visible;
            marker.localPosition=markerHome+new Vector3(0,.13f,-.15f)*Selection;
            marker.localRotation=Quaternion.Euler(0,0,-7*Selection);
            if(properties==null)properties=new MaterialPropertyBlock();
            properties.SetFloat("_Strength",Selection);
            glow.SetPropertyBlock(properties);glow.enabled=visible && Selection>.001f;
            if(palette)palette.Pose(dt,visible,selected);
        }
        void OnDisable()
        {
            ResetInertia();
            if(overlay)overlay.enabled=false;
            if(visuals)visuals.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            if(lens && overlay)lens.GetUniversalAdditionalCameraData().cameraStack.Remove(overlay);
        }
    }
}
