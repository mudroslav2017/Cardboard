using UnityEngine;

namespace Cardboard
{
    public enum WorkbenchPart { Teeth, Hub, Feed, Clamp, Guard, Crank, Cutter, Reject, Output }

    public class WorkbenchControl : MonoBehaviour
    {
        public FactoryWorkbench machine;
        public WorkbenchPart part;
        public void Use() { if(machine) machine.Operate(part); }
    }
}
