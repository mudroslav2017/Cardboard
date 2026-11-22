using UnityEngine;

namespace Cardboard
{
    public class FactoryMarkerPalette : MonoBehaviour
    {
        public const float Stagger=.085f,Duration=.46f;
        public Transform[] cards;
        public TextMesh[] numbers;
        public TextMesh shortcut;
        public bool Expanded {get;private set;}
        Vector3[] starts;
        Quaternion[] rotations;
        float[] reveal,fromReveal;
        float elapsed;
        bool selected;

        public static Vector3 ClosedPosition => new Vector3(0,-.06f,.19f);
        public static Vector3 OpenPosition(int index) => new Vector3(-2.75f+index*.45f,-.08f,.19f);
        static readonly float[] CardAngles={-4,3,-2,4,-3};
        static Quaternion OpenRotation(int index) => Quaternion.Euler(-3,-7,CardAngles[index]);
        public float Reveal(int index) => reveal!=null && index<reveal.Length?reveal[index]:0;
        static float Bounce(float t)
        {
            float s=t-1;
            return 1+2.70158f*s*s*s+1.70158f*s*s;
        }
        void Prepare()
        {
            if(starts!=null && starts.Length==cards.Length)return;
            starts=new Vector3[cards.Length];rotations=new Quaternion[cards.Length];
            reveal=new float[cards.Length];fromReveal=new float[cards.Length];
            ResetClosed();
        }
        void ResetClosed()
        {
            selected=Expanded=false;elapsed=0;
            for(int i=0;i<cards.Length;i++)
            {
                cards[i].localPosition=ClosedPosition;cards[i].localRotation=Quaternion.identity;
                cards[i].gameObject.SetActive(false);reveal[i]=0;
                starts[i]=ClosedPosition;rotations[i]=Quaternion.identity;fromReveal[i]=0;
            }
        }
        // The inventory supplies the same selection state as the existing F input path.
        public void Pose(float dt,bool visible,bool markerSelected)
        {
            if(cards==null || cards.Length==0)return;
            Prepare();
            if(!visible){ResetClosed();return;}
            if(selected!=markerSelected)
            {
                selected=markerSelected;elapsed=0;Expanded=false;
                for(int i=0;i<cards.Length;i++)
                {
                    starts[i]=cards[i].localPosition;rotations[i]=cards[i].localRotation;
                    fromReveal[i]=reveal[i];
                }
            }
            elapsed+=Mathf.Max(0,dt);
            for(int i=0;i<cards.Length;i++)
            {
                float delay=selected?i*Stagger:(cards.Length-1-i)*.025f;
                float t=Mathf.Clamp01((elapsed-delay)/Duration);
                float eased=selected?Bounce(t):1-Mathf.Pow(1-t,3);
                cards[i].localPosition=Vector3.LerpUnclamped(starts[i],selected?OpenPosition(i):ClosedPosition,eased);
                cards[i].localRotation=Quaternion.SlerpUnclamped(rotations[i],selected?OpenRotation(i):Quaternion.identity,eased);
                reveal[i]=Mathf.Lerp(fromReveal[i],selected?1:0,t);
                cards[i].gameObject.SetActive(reveal[i]>.0001f);
            }
            Expanded=selected && reveal[cards.Length-1]>=1;
        }
    }
}
