using UnityEngine;

namespace Cardboard
{
    // Imported poses are baked in the editor; player builds never depend on
    // SampleAnimation's handling of generic root-motion curves or clip stripping.
    public class FactoryGuardAnimation : MonoBehaviour
    {
        public Transform[] parts;
        public Vector3[] positions,scales;
        public Quaternion[] rotations;
        public int frames;
        float previous=-1;
        public void Evaluate(float blend,bool force=false)
        {
            if(frames<2 || parts==null)return;
            blend=Mathf.Clamp01(blend);
            if(!force && blend==previous)return;
            previous=blend;
            float sample=blend*(frames-1);int a=Mathf.FloorToInt(sample),b=Mathf.Min(a+1,frames-1);
            for(int i=0;i<parts.Length;i++)
            {
                if(!parts[i])continue;
                int first=i*frames+a,last=i*frames+b;
                parts[i].localPosition=Vector3.Lerp(positions[first],positions[last],sample-a);
                parts[i].localRotation=Quaternion.Slerp(rotations[first],rotations[last],sample-a);
                parts[i].localScale=Vector3.Lerp(scales[first],scales[last],sample-a);
            }
        }
        void OnEnable(){previous=-1;}
    }
}
