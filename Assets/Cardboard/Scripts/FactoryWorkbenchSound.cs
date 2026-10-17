using UnityEngine;

namespace Cardboard
{
    public class FactoryWorkbenchSound : MonoBehaviour
    {
        AudioSource source;
        AudioClip light,heavy;
        void Awake()
        {
            source=gameObject.AddComponent<AudioSource>();source.spatialBlend=1;source.minDistance=1;source.maxDistance=9;source.volume=.32f;
            light=Make("dry ratchet",.07f,440);heavy=Make("cardboard machine clunk",.18f,115);
        }
        AudioClip Make(string label,float duration,float pitch)
        {
            const int rate=22050;var samples=new float[(int)(rate*duration)];uint noise=17;
            for(int i=0;i<samples.Length;i++)
            {
                noise=1664525*noise+1013904223;float u=(float)i/samples.Length;
                samples[i]=((noise%65536/32768f-1)*.3f+Mathf.Sin(i*pitch/rate*Mathf.PI*2)*.6f)*Mathf.Exp(-u*8);
            }
            var clip=AudioClip.Create(label,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void Clunk(bool weight) { if(source) source.PlayOneShot(weight?heavy:light); }
        void OnDestroy() { if(light) Destroy(light);if(heavy) Destroy(heavy); }
    }
}
