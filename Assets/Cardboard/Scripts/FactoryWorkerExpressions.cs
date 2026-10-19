using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    [DefaultExecutionOrder(80)]
    public class FactoryWorkerExpressions : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public CardboardRunner[] workers;
        public FactoryWorkerAppearance[] people;
        readonly HashSet<CardboardRunner> suspicious=new HashSet<CardboardRunner>();
        public int SuspiciousCount=>suspicious.Count;
        public bool IsSuspicious(CardboardRunner worker)=>suspicious.Contains(worker);
        public void ResetShift()
        {
            suspicious.Clear();var pool=new List<CardboardRunner>();
            foreach(var worker in workers)if(worker && !worker.isPlayer)pool.Add(worker);
            for(int i=pool.Count-1;i>0;i--){int j=Random.Range(0,i+1);var swap=pool[i];pool[i]=pool[j];pool[j]=swap;}
            for(int i=0;i<(pool.Count+1)/2;i++)suspicious.Add(pool[i]);
            foreach(var person in people)if(person)person.SetState(FactoryFaceState.Normal);
        }
        void LateUpdate(){Refresh();}
        public void Refresh()
        {
            if(!game)return;
            bool chase=(game.intro && game.intro.Angry) || game.CaptureActive || game.State==FactoryState.Chased || game.State==FactoryState.Captured || game.State==FactoryState.Dead;
            foreach(var person in people)
            {
                if(!person)continue;
                var worker=person.GetComponentInParent<CardboardRunner>();
                var reaction=worker?worker.GetComponent<FactoryWorkerImpact>():null;
                bool speaking=reaction && reaction.Reacting && reaction.message && reaction.message.gameObject.activeInHierarchy;
                var state=chase || speaking?FactoryFaceState.Angry:
                    game.Mistakes>=1 && worker && suspicious.Contains(worker)?FactoryFaceState.Suspicious:FactoryFaceState.Normal;
                person.SetState(state);
            }
        }
    }
}
