using UnityEngine;

namespace Cardboard
{
    [RequireComponent(typeof(FactoryInteractable))]
    public sealed class FactorySewerAccess : MonoBehaviour
    {
        [SerializeField] Transform landing;
        [SerializeField] Transform surfaceReturn;

        public void Configure(Transform arrival, Transform returnLanding = null)
        {
            landing = arrival;
            surfaceReturn = returnLanding;
        }

        public void Use(CardboardRunner player)
        {
            if (!player || !landing || player.InSewer == (surfaceReturn != null)) return;
            player.UseSewerAccess(landing, surfaceReturn);
            if (Camera.main && Camera.main.TryGetComponent(out CardboardCamera view))
            {
                view.ResetView(landing.eulerAngles.y);
                view.RefreshPlayerPose(0);
            }
        }
    }
}
