using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    // Shared floor grid for capture and lunch routes through the canteen doorway.
    public sealed class FactoryCaptureNavigation
    {
        const float Step=.6f, MinX=-28.5f, MinZ=CardboardFactoryWorld.CanteenSouth-.6f;
        readonly int width=Mathf.CeilToInt((CardboardFactoryWorld.WorkshopEast-MinX+1)/Step);
        readonly int depth=Mathf.CeilToInt((14.5f-MinZ)/Step)+1;
        readonly bool[] walkable;
        static readonly Collider[] overlaps=new Collider[48];
        public FactoryCaptureNavigation(float clearance=.30f,Vector3[] occupied=null)
        {
            Physics.SyncTransforms();walkable=new bool[width*depth];
            for(int z=0;z<depth;z++) for(int x=0;x<width;x++)
            {
                Vector3 p=Point(z*width+x);
                bool floor=Physics.Raycast(p+Vector3.up*.3f,Vector3.down,.8f,1<<8,QueryTriggerInteraction.Ignore);
                bool blocked=Blocked(p+Vector3.up*(clearance+.08f),p+Vector3.up*1.65f,clearance);
                if(occupied!=null) foreach(var diner in occupied)
                    if((p-diner).sqrMagnitude<(clearance+.3f)*(clearance+.3f)) {blocked=true;break;}
                walkable[z*width+x]=floor && !blocked;
            }
        }
        FactoryCaptureNavigation(FactoryCaptureNavigation source)
        {walkable=(bool[])source.walkable.Clone();}
        public FactoryCaptureNavigation Avoiding(Vector3[] occupied,float clearance=.38f)
        {
            var route=new FactoryCaptureNavigation(this);
            for(int id=0;id<route.walkable.Length;id++)
            {
                if(!route.walkable[id]) continue;
                var point=route.Point(id);
                foreach(var person in occupied) if((point-person).sqrMagnitude<(clearance+.3f)*(clearance+.3f))
                {route.walkable[id]=false;break;}
            }
            return route;
        }
        static bool Blocked(Vector3 bottom,Vector3 top,float radius)
        {
            int count=Physics.OverlapCapsuleNonAlloc(bottom,top,radius,overlaps,(1<<8)|1,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++) if(!overlaps[i].GetComponentInParent<CardboardRunner>()) return true;
            return false;
        }
        public static bool LineBlocked(Vector3 from,Vector3 to)
        {
            Vector3 delta=to-from;
            foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,(1<<8)|1,QueryTriggerInteraction.Ignore))
                if(!hit.collider.GetComponentInParent<CardboardRunner>()) return true;
            return false;
        }
        Vector3 Point(int id) => new Vector3(MinX+(id%width)*Step,0,MinZ+(id/width)*Step);
        int Cell(Vector3 p)
        {
            int x=Mathf.Clamp(Mathf.RoundToInt((p.x-MinX)/Step),0,width-1);
            int z=Mathf.Clamp(Mathf.RoundToInt((p.z-MinZ)/Step),0,depth-1);
            int best=-1;float score=float.MaxValue;
            for(int radius=0;radius<14;radius++)
            {
                for(int dz=-radius;dz<=radius;dz++) for(int dx=-radius;dx<=radius;dx++)
                {
                    if(radius>0 && Mathf.Abs(dx)!=radius && Mathf.Abs(dz)!=radius) continue;
                    int nx=x+dx,nz=z+dz;if(nx<0 || nx>=width || nz<0 || nz>=depth) continue;
                    int id=nz*width+nx;if(!walkable[id]) continue;
                    float d=(Point(id)-p).sqrMagnitude;if(d<score){score=d;best=id;}
                }
                if(best>=0) return best;
            }
            return -1;
        }
        public List<Vector3> Path(Vector3 from,Vector3 to)
        {
            var result=new List<Vector3>();int start=Cell(from),goal=Cell(to);
            if(start<0 || goal<0) return result;
            var cost=new float[walkable.Length];var parent=new int[walkable.Length];var closed=new bool[walkable.Length];
            var queued=new bool[walkable.Length];var open=new List<int>{start};
            for(int i=0;i<cost.Length;i++){cost[i]=float.MaxValue;parent[i]=-1;}cost[start]=0;queued[start]=true;
            while(open.Count>0)
            {
                int best=0;float value=float.MaxValue;
                for(int i=0;i<open.Count;i++)
                {float f=cost[open[i]]+Vector3.Distance(Point(open[i]),Point(goal));if(f<value){value=f;best=i;}}
                int current=open[best];open.RemoveAt(best);queued[current]=false;
                if(current==goal)
                {
                    for(int p=goal;p!=start && p>=0;p=parent[p]) result.Add(Point(p));
                    result.Reverse();
                    // Keep the exact target when its capsule is in the same navigable cell.
                    if((Point(goal)-to).sqrMagnitude<.25f) result.Add(new Vector3(to.x,0,to.z));
                    return result;
                }
                closed[current]=true;int x=current%width,z=current/width;
                for(int dz=-1;dz<=1;dz++) for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0 && dz==0) continue;
                    int nx=x+dx,nz=z+dz;if(nx<0 || nx>=width || nz<0 || nz>=depth)continue;
                    int next=nz*width+nx;if(!walkable[next] || closed[next])continue;
                    if(dx!=0 && dz!=0 && (!walkable[z*width+nx] || !walkable[nz*width+x]))continue;
                    float distance=cost[current]+Step*(dx!=0 && dz!=0?1.414214f:1);
                    if(distance>=cost[next])continue;cost[next]=distance;parent[next]=current;
                    if(!queued[next]){open.Add(next);queued[next]=true;}
                }
            }
            return result;
        }
    }
}
