using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    [DefaultExecutionOrder(-20)]
    public class FactoryLunchCrowd : MonoBehaviour
    {
        public FactoryLunch lunch;
        public CardboardRunner[] workers;
        public Transform[] places, meals, bites;
        public float minimumLunch=27.5f, afterPlayer=7.5f, maximumLunch=35;
        public bool Active {get;private set;}
        public bool Returning {get;private set;}
        public float Elapsed {get;private set;}
        public bool AllInCanteen
        {
            get
            {
                if(!Active || Returning || workers==null)return false;
                foreach(var worker in workers)
                {
                    if(!worker || !worker.gameObject.activeInHierarchy)continue;
                    var p=worker.transform.position;
                    if(p.x<CardboardFactoryWorld.CanteenWest+.4f || p.x>CardboardFactoryWorld.CanteenEast-.4f ||
                        p.z>CardboardFactoryWorld.CanteenNorth-.6f || p.z<CardboardFactoryWorld.CanteenSouth+.4f)return false;
                }
                return true;
            }
        }
        public int EatingCount {get {int count=0;if(diners!=null) foreach(var diner in diners) if(diner.eating) count++;return count;} }
        sealed class Diner
        {
            public Vector3 home, goal, foodHome;
            public Quaternion homeRotation, foodRotation;
            public List<Vector3> path;
            public int point;
            public bool eating, done;
            public float eatingTime;
            public int lastPoint;
            public float pointAge;
        }
        Diner[] diners;
        FactoryCaptureNavigation navigation;
        float playerFinished;
        readonly List<Collider[]> deliveryCollisions=new List<Collider[]>();
        bool deliveryAccess;
        public void RefreshDeliveryAccess()
        {
            bool allow=lunch && lunch.Carrying && Active && !Returning && CardboardRunner.Player;
            if(allow==deliveryAccess)return;
            RestoreDeliveryCollisions();deliveryAccess=allow;if(!allow)return;
            var shapes=new List<Collider>(CardboardRunner.Player.GetComponentsInChildren<Collider>());
            shapes.AddRange(lunch.tray.GetComponentsInChildren<Collider>());
            for(int i=0;i<workers.Length;i++)
            {
                var place=places[i].position;
                if(Mathf.Abs(place.z-(lunch.destination.position.z+.45f))>2.4f || Mathf.Abs(place.x-lunch.destination.position.x)>4.2f)continue;
                foreach(var other in workers[i].GetComponentsInChildren<Collider>())foreach(var shape in shapes)
                {
                    if(!shape || !other || Physics.GetIgnoreCollision(shape,other))continue;
                    Physics.IgnoreCollision(shape,other,true);deliveryCollisions.Add(new[]{shape,other});
                }
            }
        }
        void RestoreDeliveryCollisions()
        {
            foreach(var pair in deliveryCollisions)if(pair[0] && pair[1])Physics.IgnoreCollision(pair[0],pair[1],false);
            deliveryCollisions.Clear();deliveryAccess=false;
        }
        int Index(CardboardRunner worker) => workers==null?-1:System.Array.IndexOf(workers,worker);
        public bool Guides(CardboardRunner worker)
        {int index=Index(worker);return Active && index>=0 && !diners[index].done;}
        public bool IsEating(CardboardRunner worker)
        {int index=Index(worker);return Active && index>=0 && diners[index].eating;}
        public string Describe(CardboardRunner worker)
        {
            int index=Index(worker);if(diners==null || index<0) return "inactive";
            var diner=diners[index];TryTarget(worker,out var target,out float speed);
            return "home="+diner.home+" goal="+diner.goal+" point="+diner.point+"/"+(diner.path==null?0:diner.path.Count)+
                " next="+target+" speed="+speed+" velocity="+worker.Velocity;
        }
        public void Begin()
        {
            if(Active) return;
            navigation=new FactoryCaptureNavigation(.38f);diners=new Diner[workers.Length];
            Active=true;Returning=false;Elapsed=playerFinished=0;
            for(int i=0;i<workers.Length;i++)
            {
                var diner=new Diner {home=workers[i].transform.position,homeRotation=workers[i].transform.rotation,
                    goal=places[i].position,foodHome=bites[i].localPosition,foodRotation=bites[i].localRotation};
                diners[i]=diner;diner.path=navigation.Path(diner.home,diner.goal);meals[i].gameObject.SetActive(false);
            }
        }
        public void BeginFinal()
        {
            if(!Active){Begin();return;}
            if(!Returning)return;
            Returning=false;Elapsed=playerFinished=0;navigation=new FactoryCaptureNavigation(.38f);
            for(int i=0;i<workers.Length;i++)
            {
                var diner=diners[i];diner.eating=diner.done=false;diner.eatingTime=0;diner.point=diner.lastPoint=0;diner.pointAge=0;
                diner.goal=places[i].position;diner.path=navigation.Path(workers[i].transform.position,diner.goal);
                meals[i].gameObject.SetActive(false);
            }
        }
        void Update() {Tick(Time.deltaTime);}
        public void Tick(float dt)
        {
            RefreshDeliveryAccess();
            if(!Active || !lunch.game || lunch.game.MenuActive || lunch.game.CaptureActive || lunch.game.Ended) return;
            Elapsed+=Mathf.Max(0,dt);
            bool playerDone=lunch.State==LunchState.Served || lunch.State==LunchState.Spilled || lunch.Missed;
            playerFinished=playerDone?playerFinished+Mathf.Max(0,dt):0;
            bool allAte=true;foreach(var diner in diners) {if(diner.eating) diner.eatingTime+=Mathf.Max(0,dt);if(diner.eatingTime<6) allAte=false;}
            // An unreachable dining place must not keep every worker in the canteen forever.
            bool finished=allAte && playerFinished>=afterPlayer;
            bool deadline=Elapsed>=Mathf.Max(minimumLunch,maximumLunch);
            if(!lunch.FinalLunch && !Returning && Elapsed>=minimumLunch && (finished || deadline)) ReturnToWork();
            bool complete=true,routesDirty=false;
            for(int i=0;i<workers.Length;i++)
            {
                var diner=diners[i];if(diner.done) continue;complete=false;
                // Lunch owns the trip through the doorway; normal work AI resumes inside the workshop.
                if(Returning && workers[i].transform.position.z>CardboardFactoryWorld.WorkshopSouth+1)
                {diner.done=true;workers[i].PauseFor(0);continue;}
                if(!diner.eating && (Returning || Elapsed>=i*.65f))
                {
                    diner.pointAge=diner.point==diner.lastPoint?diner.pointAge+Mathf.Max(0,dt):0;
                    diner.lastPoint=diner.point;
                    if(diner.pointAge>2) RepathAroundWorkers(i);
                }
                if(Vector3.Distance(workers[i].transform.position,diner.goal)>(Returning ? .85f : .23f)) continue;
                if(Returning) {diner.done=true;workers[i].transform.rotation=diner.homeRotation;}
                else if(!diner.eating)
                {
                    diner.eating=true;workers[i].transform.position=diner.goal;
                    workers[i].transform.rotation=places[i].rotation;meals[i].gameObject.SetActive(true);
                    routesDirty=true;
                }
            }
            if(Returning && complete) Active=false;
            if(routesDirty) AvoidOccupiedPlaces();
        }
        void AvoidOccupiedPlaces()
        {
            var occupied=new List<Vector3>();
            for(int i=0;i<diners.Length;i++) if(diners[i].eating) occupied.Add(places[i].position);
            var available=navigation.Avoiding(occupied.ToArray());
            for(int i=0;i<diners.Length;i++) if(!diners[i].eating && !diners[i].done)
            {diners[i].path=available.Path(workers[i].transform.position,diners[i].goal);diners[i].point=0;diners[i].pointAge=0;}
        }
        void RepathAroundWorkers(int index)
        {
            var occupied=new List<Vector3>();
            for(int i=0;i<workers.Length;i++) if(i!=index) occupied.Add(workers[i].transform.position);
            var diner=diners[index];
            var path=navigation.Avoiding(occupied.ToArray()).Path(workers[index].transform.position,diner.goal);
            if(path.Count>0) {diner.path=path;diner.point=0;if(Returning) diner.goal=path[path.Count-1];}
            diner.pointAge=0;diner.lastPoint=diner.point;
        }
        public bool TryTarget(CardboardRunner worker,out Vector3 target,out float speed)
        {
            int index=Index(worker);target=worker.transform.position;speed=0;
            if(!Active || index<0 || diners[index].done) return false;
            var diner=diners[index];
            if(diner.eating || (!Returning && Elapsed<index*.65f)) return true;
            speed=Returning?2.2f:2.5f;
            if(diner.path==null || diner.path.Count==0) return true;
            while(diner.point<diner.path.Count-1 && Vector3.Distance(worker.transform.position,diner.path[diner.point])<.10f) diner.point++;
            target=diner.path[diner.point];
            if(diner.point==diner.path.Count-1 && Vector3.Distance(worker.transform.position,target)<.35f) target=diner.goal;
            return true;
        }
        public void PoseDiner(CardboardRunner worker)
        {
            int index=Index(worker);if(index<0 || !IsEating(worker)) return;
            float phase=Elapsed*2.4f+index*.83f;
            worker.transform.rotation=places[index].rotation;
            if(worker.puppet)
            {
                worker.puppet.localPosition=new Vector3(0,-.16f+Mathf.Sin(phase)*.008f,0);
                worker.puppet.localRotation=Quaternion.Euler(3+Mathf.Sin(phase)*3,0,Mathf.Sin(phase*.5f)*1.5f);
            }
            float lift=Mathf.SmoothStep(0,1,Mathf.Max(0,Mathf.Sin(phase)));
            var diner=diners[index];var bite=bites[index];
            Vector3 plate=bite.parent.TransformPoint(diner.foodHome);
            Vector3 mouth=worker.transform.position+Vector3.up*1.57f+worker.transform.forward*.22f;
            bite.position=Vector3.Lerp(plate,mouth,lift);
            bite.localRotation=diner.foodRotation*Quaternion.Euler(0,0,lift*25);
        }
        void ReturnToWork()
        {
            Returning=true;
            RefreshDeliveryAccess();
            // Remove every meal before sampling navigation: a bite held at a worker's mouth
            // otherwise becomes an obstacle around the route's starting cell.
            for(int i=0;i<workers.Length;i++)
            {
                var diner=diners[i];diner.eating=false;meals[i].gameObject.SetActive(false);
                bites[i].localPosition=diner.foodHome;bites[i].localRotation=diner.foodRotation;
                workers[i].Pose(0,0);
            }
            navigation=new FactoryCaptureNavigation(.38f);
            for(int i=0;i<workers.Length;i++)
            {
                var diner=diners[i];diner.goal=diner.home;diner.point=0;diner.pointAge=0;diner.lastPoint=0;
                diner.path=navigation.Path(workers[i].transform.position,diner.goal);
                if(diner.path.Count>0) diner.goal=diner.path[diner.path.Count-1];
            }
        }
        public void Cancel()
        {
            RestoreDeliveryCollisions();
            Active=Returning=false;
            if(meals!=null) foreach(var meal in meals) if(meal) meal.gameObject.SetActive(false);
            if(workers!=null) foreach(var worker in workers) if(worker) worker.Pose(0,0);
            if(diners!=null) foreach(var diner in diners) diner.eating=false;
        }
        public void ResetLunch()
        {
            Cancel();Elapsed=playerFinished=0;
            if(diners!=null)
            {
                for(int i=0;i<workers.Length;i++)
                {
                    var body=workers[i].GetComponent<CharacterController>();bool enabled=body && body.enabled;
                    if(body) body.enabled=false;
                    workers[i].transform.SetPositionAndRotation(diners[i].home,diners[i].homeRotation);
                    if(body) body.enabled=enabled;
                    bites[i].localPosition=diners[i].foodHome;bites[i].localRotation=diners[i].foodRotation;
                }
            }
            diners=null;navigation=null;
        }
        void OnDisable(){RestoreDeliveryCollisions();}
    }
}
