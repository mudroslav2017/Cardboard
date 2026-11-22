using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cardboard
{
    [DefaultExecutionOrder(150)]
    public class CardboardMarkerHand : MonoBehaviour
    {
        public Transform Tip => pen;
        public Transform Palm => marker && marker.Erasing?clothPalm:palm;
        public bool ClothVisible => clothVisuals && clothVisuals.gameObject.activeInHierarchy;
        public bool Visible => pen && pen.gameObject.activeSelf;
        Transform pen,palm,arm,markerVisuals,clothVisuals,clothPalm;
        Camera lens;
        public Camera viewCamera;
        public Material paperTemplate;
        public const int HandLayer=13;
        public const string ShaderName="Cardboard/FirstPersonHand";
        CardboardMarker marker;
        Material ink;
        readonly List<Material> owned=new List<Material>();
        Mesh clothMesh;
        Vector2 previousPointer,mouseMotion;
        bool wasVisible;
        bool wasErasing;
        float equip=1;

        Material Material(string name,string hex)
        {
            var shader=paperTemplate?paperTemplate.shader:Shader.Find(ShaderName);
            // Clone the serialized material, including the shader variant kept in the build.
            var mat=paperTemplate?new Material(paperTemplate):new Material(shader);
            mat.name=name;mat.SetColor("_BaseColor",CardboardArt.Hex(hex));
            owned.Add(mat);return mat;
        }
        Transform Part(string name,Transform parent,Vector3 pos,Vector3 size,Material mat,PrimitiveType shape=PrimitiveType.Cube)
        {
            var obj=GameObject.CreatePrimitive(shape);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=pos;obj.transform.localScale=size;obj.layer=HandLayer;
            var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=mat;
            var collider=obj.GetComponent<Collider>();if(collider){collider.enabled=false;Destroy(collider);}
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.allowOcclusionWhenDynamic=false;
            var properties=new MaterialPropertyBlock();if(mat==ink)properties.SetFloat("_Cull",(float)CullMode.Back);else properties.SetColor("_BaseColor",mat.GetColor("_BaseColor"));renderer.SetPropertyBlock(properties);
            return obj.transform;
        }
        Transform Cylinder(string name,float z,float length,float radius,Material mat)
        {
            var part=Part(name,markerVisuals,new Vector3(0,0,z),new Vector3(radius*2,length*.5f,radius*2),mat,PrimitiveType.Cylinder);
            part.localRotation=Quaternion.Euler(90,0,0);return part;
        }
        void EnsureViewCamera()
        {
            // Draw first-person geometry with the base camera on every graphics backend.
            // The dedicated material handles world-depth occlusion and convex back faces.
            if(viewCamera)
            {
                viewCamera.enabled=false;
            }
            lens.cullingMask|=1<<HandLayer;
        }
        void UpdateViewCamera(bool show)
        {
            if(viewCamera)viewCamera.enabled=false;
        }
        void EnsureModel()
        {
            if(pen) return;
            lens=GetComponent<Camera>();marker=GetComponent<CardboardMarker>();
            lens.cullingMask &= ~(1<<FactoryDeathPresentation.Layer);
            EnsureViewCamera();
            var sleeve=Material("Cardboard sleeve","4F8075");
            var skin=Material("Hand cut from kraft","C39A69");
            var crease=Material("Hand fold lines","8E6746");
            var body=Material("Ivory marker barrel","F1E9D4");
            var black=Material("Marker grip","303638");
            ink=Material("Selected felt tip","252A31");
            pen=new GameObject("Right hand • marker nib origin").transform;pen.SetParent(transform,false);
            markerVisuals=new GameObject("Marker and grip").transform;markerVisuals.SetParent(pen,false);
            // Local +Z is the writing direction. The felt nib ends exactly at the root origin.
            Cylinder("Felt writing tip",-.016f,.032f,.009f,ink);
            Cylinder("Nib collar",-.053f,.042f,.020f,black);
            Cylinder("Marker barrel",-.235f,.32f,.026f,body);
            Cylinder("Ink color band",-.100f,.025f,.027f,ink);
            Cylinder("End cap",-.411f,.045f,.028f,ink);
            for(int i=0;i<3;i++)
                Part("Printed barrel stripe",markerVisuals,new Vector3(.026f,0,-.16f-i*.035f),new Vector3(.002f,.028f,.012f),black);
            palm=Part("Palm gripping the marker",markerVisuals,new Vector3(.035f,-.066f,-.28f),new Vector3(.105f,.13f,.115f),skin);
            palm.localRotation=Quaternion.Euler(-12,0,-12);
            for(int i=0;i<3;i++)
            {
                var finger=Part("Wrapped finger "+i,markerVisuals,new Vector3(-.009f,-.022f,-.24f-i*.037f),new Vector3(.091f,.052f,.029f),skin);
                finger.localRotation=Quaternion.Euler(0,0,-18);
                Part("Finger crease",markerVisuals,new Vector3(-.025f,.005f,-.24f-i*.037f),new Vector3(.027f,.002f,.003f),crease);
            }
            var thumb=Part("Thumb",markerVisuals,new Vector3(.053f,.003f,-.215f),new Vector3(.045f,.05f,.09f),skin);
            thumb.localRotation=Quaternion.Euler(0,-28,12);
            CreateCloth(skin,crease);
            arm=Part("Sleeved forearm",transform,Vector3.zero,Vector3.one,sleeve);
            pen.gameObject.SetActive(false);arm.gameObject.SetActive(false);
        }
        void CreateCloth(Material skin,Material crease)
        {
            clothVisuals=new GameObject("Cleaning cloth and grip").transform;clothVisuals.SetParent(pen,false);
            var clothMaterial=Material("Blue grey cleaning cloth","88AFB1");
            var seamMaterial=Material("Cloth woven seams","507C81");
            var cloth=new GameObject("Folded fabric",typeof(MeshFilter),typeof(MeshRenderer));
            cloth.transform.SetParent(clothVisuals,false);cloth.layer=HandLayer;
            const int count=6,faceCount=count*count;
            var vertices=new Vector3[faceCount*2];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            for(int side=0;side<2;side++)for(int y=0;y<count;y++)for(int x=0;x<count;x++)
            {
                float u=x/(float)(count-1),v=y/(float)(count-1);
                float fold=Mathf.Pow(Mathf.Sin(u*Mathf.PI*2)*Mathf.Sin(v*Mathf.PI*2),2)*.004f;
                int index=side*faceCount+y*count+x;
                vertices[index]=new Vector3((u-.5f)*.19f,(v-.5f)*.16f,-fold-side*.012f);uv[index]=new Vector2(u,v);
                if(x==count-1 || y==count-1)continue;
                int a=index,b=index+1,c=index+count+1,d=index+count;
                if(side==0)triangles.AddRange(new[]{a,b,c,a,c,d});else triangles.AddRange(new[]{a,c,b,a,d,c});
            }
            // Close the hem so the rag has thickness when viewed from the side.
            for(int i=0;i<count-1;i++)
            {
                int[] edge={i,i+1,(count-1)*count+i+1,(count-1)*count+i,i*count,(i+1)*count,(i+1)*count+count-1,i*count+count-1};
                for(int e=0;e<edge.Length;e+=2)
                {int a=edge[e],b=edge[e+1];triangles.AddRange(new[]{a,b,b+faceCount,a,b+faceCount,a+faceCount});}
            }
            clothMesh=new Mesh{name="Folded wiping cloth"};clothMesh.vertices=vertices;clothMesh.uv=uv;clothMesh.triangles=triangles.ToArray();clothMesh.RecalculateNormals();clothMesh.RecalculateBounds();
            cloth.GetComponent<MeshFilter>().sharedMesh=clothMesh;
            var renderer=cloth.GetComponent<MeshRenderer>();renderer.sharedMaterial=clothMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            for(int i=-4;i<=4;i++)Part("Cloth weave",clothVisuals,new Vector3(i*.019f,0,-.017f),new Vector3(.0015f,.145f,.0015f),seamMaterial);
            foreach(float y in new[]{-.069f,.069f})Part("Stitched cloth hem",clothVisuals,new Vector3(0,y,-.018f),new Vector3(.178f,.003f,.002f),seamMaterial);
            clothPalm=Part("Palm holding the cloth",clothVisuals,new Vector3(.025f,-.02f,-.075f),new Vector3(.11f,.13f,.065f),skin);
            for(int i=0;i<3;i++)
            {
                Part("Fingers pressing the cloth",clothVisuals,new Vector3(.005f,.045f-i*.035f,-.03f),new Vector3(.12f,.027f,.034f),skin);
                Part("Cloth grip finger crease",clothVisuals,new Vector3(-.025f,.045f-i*.035f,-.048f),new Vector3(.03f,.002f,.002f),crease);
            }
            var thumb=Part("Thumb gripping cloth edge",clothVisuals,new Vector3(.075f,-.025f,-.035f),new Vector3(.04f,.075f,.045f),skin);
            thumb.localRotation=Quaternion.Euler(0,0,20);clothVisuals.gameObject.SetActive(false);
        }
        void LateUpdate() { RefreshPose(Time.deltaTime); }

        public void RefreshPose(float deltaTime)
        {
            EnsureModel();
            bool show=marker && CardboardMarker.DrawingMode;
            pen.gameObject.SetActive(show);arm.gameObject.SetActive(show);UpdateViewCamera(show);
            if(!show) { wasVisible=false;return; }
            bool erasing=marker.Erasing;
            if(wasErasing!=erasing){equip=0;wasErasing=erasing;}
            equip=Mathf.Min(1,equip+Mathf.Max(0,deltaTime)*5);
            markerVisuals.gameObject.SetActive(!erasing);clothVisuals.gameObject.SetActive(erasing);
            Vector2 pointer=marker.Pointer;
            if(!marker.Target && (pointer.x<0 || pointer.y<0 || pointer.x>Screen.width || pointer.y>Screen.height))pointer=new Vector2(Screen.width*.6f,Screen.height*.45f);
            if(!wasVisible) { previousPointer=pointer;mouseMotion=Vector2.zero; }
            Vector2 movement=(pointer-previousPointer)/Mathf.Max(1,Screen.height)*720;
            mouseMotion=Vector2.Lerp(mouseMotion,movement,1-Mathf.Exp(-18*Mathf.Max(deltaTime,.001f)));
            previousPointer=pointer;
            Ray ray=lens.ScreenPointToRay(pointer);
            float planeDistance=.72f/Mathf.Max(.2f,Vector3.Dot(transform.forward,ray.direction));
            Vector3 target=ray.GetPoint(planeDistance)+(marker.IsPressed?ray.direction*.045f:Vector3.zero);
            if(marker.Target) target=marker.ContactPoint+marker.ContactNormal*(marker.IsTouching?.003f:.065f);
            Vector3 elbow=transform.TransformPoint(new Vector3(.30f,-.38f,.13f));
            Vector3 axis=(target-elbow).normalized;
            float side=Mathf.Clamp(mouseMotion.x*.6f,-13,13),up=Mathf.Clamp(-mouseMotion.y*.6f,-13,13);
            Quaternion rotation=Quaternion.LookRotation(axis,transform.up)*Quaternion.Euler(up,side,-18);
            if(erasing && marker.Target)rotation=Quaternion.LookRotation(-marker.ContactNormal,transform.up)*Quaternion.Euler(0,0,-12+side*.25f);
            if(!marker.IsTouching)target-=transform.up*(.16f*(1-Mathf.SmoothStep(0,1,equip)));
            // During a stroke the nib is attached to the very same point that receives ink.
            if(marker.IsTouching || !wasVisible) pen.position=target;
            else pen.position=Vector3.Lerp(pen.position,target,1-Mathf.Exp(-24*Mathf.Max(deltaTime,.001f)));
            pen.rotation=rotation;ink.SetColor("_BaseColor",marker.Ink);
            Vector3 wrist=Palm.TransformPoint(new Vector3(0,-.075f,-.01f));
            arm.position=(elbow+wrist)*.5f;
            arm.rotation=Quaternion.FromToRotation(Vector3.up,wrist-elbow);
            arm.localScale=new Vector3(.10f,Vector3.Distance(elbow,wrist),.105f);
            wasVisible=true;
        }
        void OnDisable()
        {
            if(viewCamera)viewCamera.enabled=false;
            if(pen)pen.gameObject.SetActive(false);
            if(arm)arm.gameObject.SetActive(false);
            wasVisible=false;
        }
        void OnDestroy()
        {
            foreach(var mat in owned)if(mat)Destroy(mat);
            if(clothMesh)Destroy(clothMesh);
        }
    }
}
