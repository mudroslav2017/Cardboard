using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    [DefaultExecutionOrder(300)]
    public class FactoryVolumeCursor : MonoBehaviour
    {
        public Camera lens,overlay;
        public FactoryMainMenu menu;
        public Transform model;
        public float modelHeight=1,pixelHeight=46;
        public Vector2 ScreenPosition => Mouse.current!=null?Mouse.current.position.ReadValue():new Vector2(Screen.width*.5f,Screen.height*.5f);
        public Ray PointerRay => lens.ScreenPointToRay(ScreenPosition);
        public Vector3 HotspotLocal {get{CalibrateHotspot();return hotspot;}}
        public Vector3 HotspotWorldPosition => model.TransformPoint(HotspotLocal);
        static readonly Quaternion Orientation=Quaternion.Euler(0,0,90)*Quaternion.Euler(-9,-12,0);
        static FactoryVolumeCursor owner;
        Mesh calibratedMesh;
        Vector3 hotspot;
        float pressScale=1;
        bool focused=true;

        public static void SetSystemVisible(bool requested)
        {
            // All gameplay systems share one owner, so opening a menu cannot reveal the OS cursor between frames.
            bool visible=requested && !(owner && owner.isActiveAndEnabled && owner.focused);
            if(Cursor.visible!=visible)Cursor.visible=visible;
        }
        void OnEnable()
        {
            owner=this;CalibrateHotspot();
            SetSystemVisible(false);
        }
        void CalibrateHotspot()
        {
            if(!model)return;
            var filter=model.GetComponent<MeshFilter>();var mesh=filter?filter.sharedMesh:null;
            if(!mesh || mesh==calibratedMesh)return;
            float highest=float.NegativeInfinity;hotspot=Vector3.zero;
            foreach(var vertex in mesh.vertices)
            {
                float height=(Orientation*vertex).y;
                if(height>highest){highest=height;hotspot=vertex;}
            }
            calibratedMesh=mesh;
        }
        void LateUpdate(){Refresh(Time.unscaledDeltaTime);}
        public void Refresh(float dt)
        {
            bool visible=focused && Mouse.current!=null && Cursor.lockState!=CursorLockMode.Locked;
            if(overlay)
            {
                overlay.enabled=visible || (menu && menu.BlocksGameplay);
                overlay.fieldOfView=lens.fieldOfView;overlay.aspect=lens.aspect;
            }
            model.gameObject.SetActive(visible);
            if(!focused)return;
            SetSystemVisible(false);
            if(!visible)return;
            const float depth=.85f;
            float target=Mouse.current.leftButton.isPressed?.88f:1;
            pressScale=Mathf.Lerp(pressScale,target,1-Mathf.Exp(-22*Mathf.Max(0,dt)));
            float worldPerPixel=2*depth*Mathf.Tan(lens.fieldOfView*Mathf.Deg2Rad*.5f)/Mathf.Max(1,lens.pixelHeight);
            model.localScale=Vector3.one*(worldPerPixel*pixelHeight/Mathf.Max(.0001f,modelHeight)*pressScale);
            var point=lens.ScreenToWorldPoint(new Vector3(ScreenPosition.x,ScreenPosition.y,depth));
            model.SetPositionAndRotation(point,lens.transform.rotation*Orientation);
            // Offset the actual model vertex, including its depth, tilt and click animation.
            model.position+=point-HotspotWorldPosition;
        }
        void OnApplicationFocus(bool value)
        {
            focused=value;SetSystemVisible(!value);
            if(!value && model)model.gameObject.SetActive(false);
        }
        void OnDisable()
        {
            if(owner==this){owner=null;SetSystemVisible(true);}
            if(model)model.gameObject.SetActive(false);
        }
    }
}
