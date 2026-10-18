using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cardboard
{
    [DefaultExecutionOrder(150)]
    public class CardboardMarkerHand : MonoBehaviour
    {
        public Transform Tip => pen;
        public Transform Palm => palm;
        public bool Visible => pen && pen.gameObject.activeSelf;
        Transform pen,palm,arm;
        Camera lens;
        CardboardMarker marker;
        Material ink;
        readonly List<Material> owned=new List<Material>();
        Vector2 previousPointer,mouseMotion;
        bool wasVisible;

        Material Material(string name,string hex)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader) shader=Shader.Find("Standard");
            var mat=new Material(shader) {name=name,color=CardboardArt.Hex(hex)};
            mat.SetFloat("_Smoothness",0);owned.Add(mat);return mat;
        }
        Transform Part(string name,Transform parent,Vector3 pos,Vector3 size,Material mat,PrimitiveType shape=PrimitiveType.Cube)
        {
            var obj=GameObject.CreatePrimitive(shape);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=pos;obj.transform.localScale=size;
            var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=mat;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var collider=obj.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            return obj.transform;
        }
        Transform Cylinder(string name,float z,float length,float radius,Material mat)
        {
            var part=Part(name,pen,new Vector3(0,0,z),new Vector3(radius*2,length*.5f,radius*2),mat,PrimitiveType.Cylinder);
            part.localRotation=Quaternion.Euler(90,0,0);return part;
        }
        void EnsureModel()
        {
            if(pen) return;
            lens=GetComponent<Camera>();marker=GetComponent<CardboardMarker>();
            var sleeve=Material("Cardboard sleeve","4F8075");
            var skin=Material("Hand cut from kraft","C39A69");
            var crease=Material("Hand fold lines","8E6746");
            var body=Material("Ivory marker barrel","F1E9D4");
            var black=Material("Marker grip","303638");
            ink=Material("Selected felt tip","252A31");
            pen=new GameObject("Right hand • marker nib origin").transform;pen.SetParent(transform,false);
            // Local +Z is the writing direction. The felt nib ends exactly at the root origin.
            Cylinder("Felt writing tip",-.016f,.032f,.009f,ink);
            Cylinder("Nib collar",-.053f,.042f,.020f,black);
            Cylinder("Marker barrel",-.235f,.32f,.026f,body);
            Cylinder("Ink color band",-.100f,.025f,.027f,ink);
            Cylinder("End cap",-.411f,.045f,.028f,ink);
            for(int i=0;i<3;i++)
                Part("Printed barrel stripe",pen,new Vector3(.026f,0,-.16f-i*.035f),new Vector3(.002f,.028f,.012f),black);
            palm=Part("Palm gripping the marker",pen,new Vector3(.035f,-.066f,-.28f),new Vector3(.105f,.13f,.115f),skin);
            palm.localRotation=Quaternion.Euler(-12,0,-12);
            for(int i=0;i<3;i++)
            {
                var finger=Part("Wrapped finger "+i,pen,new Vector3(-.009f,-.022f,-.24f-i*.037f),new Vector3(.091f,.052f,.029f),skin);
                finger.localRotation=Quaternion.Euler(0,0,-18);
                Part("Finger crease",pen,new Vector3(-.025f,.005f,-.24f-i*.037f),new Vector3(.027f,.002f,.003f),crease);
            }
            var thumb=Part("Thumb",pen,new Vector3(.053f,.003f,-.215f),new Vector3(.045f,.05f,.09f),skin);
            thumb.localRotation=Quaternion.Euler(0,-28,12);
            arm=Part("Sleeved forearm",transform,Vector3.zero,Vector3.one,sleeve);
            pen.gameObject.SetActive(false);arm.gameObject.SetActive(false);
        }
        void LateUpdate() { RefreshPose(Time.deltaTime); }

        public void RefreshPose(float deltaTime)
        {
            EnsureModel();
            bool show=marker && CardboardMarker.DrawingMode;
            pen.gameObject.SetActive(show);arm.gameObject.SetActive(show);
            if(!show) { wasVisible=false;return; }
            Vector2 pointer=marker.Pointer;
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
            // During a stroke the nib is attached to the very same point that receives ink.
            if(marker.IsTouching || !wasVisible) pen.position=target;
            else pen.position=Vector3.Lerp(pen.position,target,1-Mathf.Exp(-24*Mathf.Max(deltaTime,.001f)));
            pen.rotation=rotation;ink.color=marker.Ink;
            Vector3 wrist=palm.TransformPoint(new Vector3(0,-.075f,-.01f));
            arm.position=(elbow+wrist)*.5f;
            arm.rotation=Quaternion.FromToRotation(Vector3.up,wrist-elbow);
            arm.localScale=new Vector3(.10f,Vector3.Distance(elbow,wrist),.105f);
            wasVisible=true;
        }
        void OnDestroy() { foreach(var mat in owned) if(mat) Destroy(mat); }
    }
}
