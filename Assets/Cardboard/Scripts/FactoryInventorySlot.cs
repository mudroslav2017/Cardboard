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
        public MeshRenderer glow,outline;
        public Vector3 markerHome;
        public float Selection {get;private set;}
        public bool Visible => visuals && visuals.gameObject.activeSelf;
        MaterialPropertyBlock properties,outlineProperties;

        void LateUpdate()
        {
            bool visible=game && !game.MenuActive && !game.CaptureActive && !game.Ended;
            Pose(Time.unscaledDeltaTime,visible,CardboardMarker.DrawingMode);
        }
        public void Layout()
        {
            if(!lens || !overlay || !visuals)return;
            overlay.aspect=lens.aspect;
            const float depth=.65f;
            float halfHeight=depth*Mathf.Tan(overlay.fieldOfView*Mathf.Deg2Rad*.5f);
            // A fixed fraction of screen height keeps the slot stable when FOV or aspect changes.
            float size=halfHeight*.53f;
            visuals.localScale=Vector3.one*size;
            visuals.localPosition=new Vector3(halfHeight*lens.aspect-size*.86f,-halfHeight+size*.70f,depth);
            // Face the viewer before tilting: perspective at the screen corner otherwise reverses the apparent angle.
            visuals.localRotation=Quaternion.LookRotation(visuals.localPosition,Vector3.up)*Quaternion.Euler(3,9,3);
            if(outline)
            {
                if(outlineProperties==null)outlineProperties=new MaterialPropertyBlock();
                outlineProperties.SetVector("_PixelStep",new Vector4(2f/Mathf.Max(1,lens.pixelWidth),2f/Mathf.Max(1,lens.pixelHeight),0,0));
                outline.SetPropertyBlock(outlineProperties);
            }
        }
        public void Pose(float dt,bool visible,bool selected)
        {
            Layout();
            Selection=Mathf.Lerp(Selection,visible && selected?1:0,1-Mathf.Exp(-12*Mathf.Max(0,dt)));
            visuals.gameObject.SetActive(visible);overlay.enabled=visible;
            marker.localPosition=markerHome+new Vector3(0,.13f,-.15f)*Selection;
            marker.localRotation=Quaternion.Euler(0,0,-7*Selection);
            if(properties==null)properties=new MaterialPropertyBlock();
            properties.SetFloat("_Strength",Selection);
            glow.SetPropertyBlock(properties);glow.enabled=visible && Selection>.001f;
        }
        void OnDisable()
        {
            if(overlay)overlay.enabled=false;
            if(visuals)visuals.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            if(lens && overlay)lens.GetUniversalAdditionalCameraData().cameraStack.Remove(overlay);
        }
    }
}
