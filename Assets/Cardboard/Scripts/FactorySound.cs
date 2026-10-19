using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    public class FactorySound : MonoBehaviour
    {
        AudioSource effects;
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        void Awake()
        {
            effects=gameObject.AddComponent<AudioSource>();effects.volume=.28f;
            clips["good"]=Tone("good",.30f,540,810);
            clips["wrong"]=Tone("wrong",.38f,145,90);
            clips["alarm"]=Tone("alarm",.7f,480,740);
            clips["impact"]=Tone("impact",.20f,90,38);
        }
        AudioClip Tone(string name,float seconds,float a,float b)
        {
            const int rate=22050;var data=new float[(int)(rate*seconds)];
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate,u=(float)i/data.Length;
                float frequency=Mathf.Lerp(a,b,u);
                float envelope=Mathf.Min(1,u*30)*(1-u);
                data[i]=(Mathf.Sin(t*frequency*Mathf.PI*2)*.25f+Mathf.Sin(t*frequency*Mathf.PI*4)*.06f)*envelope;
            }
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        public void Play(string name) { if(effects && clips.TryGetValue(name,out var clip)) effects.PlayOneShot(clip); }
        void OnDestroy()
        {
            foreach(var clip in clips.Values) if(clip) Destroy(clip);
        }
    }
}
