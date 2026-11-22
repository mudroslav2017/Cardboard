using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Cardboard
{
    public class FactoryDeathPresentation : MonoBehaviour
    {
        public const int Layer=12;
        public Camera lens,overlay;
        public Transform gearRoot;
        public Transform[] gears;
        public Image wave;
        public Light fill;
        Material waveMaterial;
        float impactTime;
        public float ImpactTime=>impactTime;
        void Awake()
        {
            foreach(var renderer in gearRoot.GetComponentsInChildren<MeshRenderer>(true))CardboardSurface.Configure(renderer);
        }
        void EnsureMaterial()
        {
            if(waveMaterial)return;
            waveMaterial=new Material(wave.material){name="Crush wave • live instance",hideFlags=HideFlags.HideAndDontSave};
            wave.material=waveMaterial;
        }
        public void Show(float time)
        {
            impactTime=Mathf.Max(0,time);EnsureMaterial();
            gearRoot.gameObject.SetActive(true);overlay.enabled=true;if(fill)fill.enabled=true;
            Layout();
        }
        public void Layout()
        {
            if(!lens || !overlay || !gearRoot)return;
            overlay.aspect=lens.aspect;
            const float depth=2;
            float halfHeight=depth*Mathf.Tan(overlay.fieldOfView*Mathf.Deg2Rad*.5f),halfWidth=halfHeight*lens.aspect;
            float diameter=halfHeight*.5f;
            for(int i=0;i<gears.Length;i++)
            {
                int corner=i/3,kind=i%3;
                float sideX=corner%2==0?-1:1,sideY=corner<2?1:-1;
                float insetX=kind==2?1.35f:kind==1?.38f:.62f;
                float insetY=kind==1?1.33f:kind==2?.40f:.62f;
                Vector3 target=new Vector3(sideX*(halfWidth-diameter*insetX),sideY*(halfHeight-diameter*insetY),depth);
                float t=Mathf.Clamp01((impactTime-kind*.045f)/.63f);
                float u=t-1;float ease=1+2.35f*u*u*u+1.35f*u*u;
                Vector3 radial=new Vector3(target.x,target.y,0).normalized;
                gears[i].localPosition=Vector3.LerpUnclamped(target+radial*halfHeight*1.8f,target,ease);
                float spin=sideX*(Mathf.Lerp(100,0,t)+(kind-1)*12);
                gears[i].localRotation=Quaternion.LookRotation(gears[i].localPosition,Vector3.up)*Quaternion.Euler(-sideY*13,sideX*18,spin);
                float ratio=kind==0?1:kind==1?.57f:.47f;
                gears[i].localScale=Vector3.one*diameter*ratio;
            }
            if(waveMaterial)
            {
                waveMaterial.SetFloat("_ImpactTime",impactTime);waveMaterial.SetFloat("_Aspect",lens.aspect);
                wave.enabled=impactTime<.85f;
            }
        }
        void LateUpdate(){if(overlay && overlay.enabled)Layout();}
        void OnDisable()
        {
            if(overlay)overlay.enabled=false;
            if(gearRoot)gearRoot.gameObject.SetActive(false);
            if(fill)fill.enabled=false;
        }
        void OnDestroy()
        {
            if(lens && overlay)lens.GetUniversalAdditionalCameraData().cameraStack.Remove(overlay);
            if(waveMaterial){if(Application.isPlaying)Destroy(waveMaterial);else DestroyImmediate(waveMaterial);}
        }
    }
}
