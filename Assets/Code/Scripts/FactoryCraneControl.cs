using UnityEngine;

namespace Cardboard
{
    public enum CraneAccess { LadderBottom, LadderTop, RoofHatch, CabinHatch, Console, Forklift }

    public sealed class FactoryCraneControl : MonoBehaviour
    {
        public CraneAccess access;
    }
}
