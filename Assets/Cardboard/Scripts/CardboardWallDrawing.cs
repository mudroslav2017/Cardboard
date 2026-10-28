using UnityEngine;
using UnityEngine.Rendering;

namespace Cardboard
{
    [RequireComponent(typeof(MeshFilter))]
    public class CardboardWallDrawing : MonoBehaviour
    {
        public Material inkMaterial;
        public Mesh faceMesh;
        public CardboardDrawingSurface[] Faces {get;private set;}

        void Awake()
        {
            if(!inkMaterial || !faceMesh)return;
            var bounds=GetComponent<MeshFilter>().sharedMesh.bounds;
            var normals=new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left};
            Faces=new CardboardDrawingSurface[normals.Length];
            for(int i=0;i<normals.Length;i++)
            {
                var normal=normals[i];var right=Vector3.Cross(Vector3.up,normal);
                var size=bounds.size;
                float width=Vector3.Dot(size,new Vector3(Mathf.Abs(right.x),0,Mathf.Abs(right.z)));
                float height=size.y;
                var node=new GameObject("Wall marker face "+i,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
                node.layer=gameObject.layer;node.transform.SetParent(transform,false);
                // Keep the ink just in front of the original wall, with unique UVs on each side.
                float offset=.002f/transform.TransformVector(normal).magnitude;
                node.transform.localPosition=bounds.center+Vector3.Scale(bounds.extents,normal)+normal*offset;
                node.transform.localRotation=Quaternion.LookRotation(normal,Vector3.up);
                node.transform.localScale=new Vector3(width,height,1);
                node.GetComponent<MeshFilter>().sharedMesh=faceMesh;
                node.GetComponent<MeshCollider>().sharedMesh=faceMesh;
                var renderer=node.GetComponent<MeshRenderer>();renderer.sharedMaterial=inkMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.enabled=false;
                var surface=node.AddComponent<CardboardDrawingSurface>();surface.transparentInk=true;
                surface.physicalSize=new Vector2(transform.TransformVector(right*width).magnitude,transform.TransformVector(Vector3.up*height).magnitude);
                Faces[i]=surface;
            }
        }
        public void ClearDrawing()
        {
            if(Faces==null)return;
            foreach(var face in Faces)if(face)face.ClearDrawing();
        }
    }
}
