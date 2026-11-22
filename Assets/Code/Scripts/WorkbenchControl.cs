using UnityEngine;

namespace Cardboard
{
    public enum WorkbenchPart { Teeth, Hub, Feed, Clamp, Guard, Crank, Cutter, Reject, Output }

    public class WorkbenchControl : MonoBehaviour
    {
        public FactoryWorkbench machine;
        public WorkbenchPart part;
        void Start()
        {
            var hit=GetComponent<BoxCollider>();if(!hit) return;
            if(part==WorkbenchPart.Feed || part==WorkbenchPart.Clamp || part==WorkbenchPart.Cutter)
                hit.size=new Vector3(Mathf.Max(hit.size.x,.44f),hit.size.y,Mathf.Max(hit.size.z,.34f));
            else if(part==WorkbenchPart.Guard || part==WorkbenchPart.Reject)
                hit.size=new Vector3(Mathf.Max(hit.size.x,.40f),Mathf.Max(hit.size.y,.34f),Mathf.Max(hit.size.z,.30f));
        }
        public void Use() { if(machine) machine.Operate(part); }
    }
}
