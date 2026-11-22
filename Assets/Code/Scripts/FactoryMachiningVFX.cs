using UnityEngine;

namespace Cardboard
{
    public class FactoryMachiningVFX : MonoBehaviour
    {
        public ParticleSystem[] shavings;
        public int EmittedCount {get;private set;}
        float remainder;int variant;
        void Awake(){Clear();}
        public void Cut(float progress)
        {
            if(progress<=0 || shavings==null || shavings.Length==0)return;
            remainder+=progress*180;
            int count=Mathf.FloorToInt(remainder);remainder-=count;
            for(int i=0;i<count;i++)
            {
                var system=shavings[variant++%shavings.Length];
                if(system){if(!system.isPlaying)system.Play(false);system.Emit(1);EmittedCount++;}
            }
        }
        public void Clear()
        {
            remainder=0;variant=0;EmittedCount=0;
            if(shavings==null)return;
            foreach(var system in shavings)if(system)
            {
                system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                if(Application.isPlaying)system.Play(false);
            }
        }
    }
}
