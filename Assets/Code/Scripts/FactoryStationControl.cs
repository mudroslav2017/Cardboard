using UnityEngine;

namespace Cardboard
{
    public enum StationPart { Feed, Selector, Press, Guard, Wheel, Output, Bath }

    public class FactoryStationControl : MonoBehaviour
    {
        public FactoryWorkstation station;
        public StationPart part;
        public int number;
        public bool Use() => station && station.Operate(part, number);
    }
}
