using UnityEngine;
using static Cardboard.CardboardArt;

namespace Cardboard
{
    // The grip is parented to the moving handle; the sleeve follows the camera.
    [DefaultExecutionOrder(80)]
    public class FactoryOperatorHand : MonoBehaviour
    {
        public Transform palm, sleeve;
        public Transform Grip { get; private set; }
        public static FactoryOperatorHand Create(Transform station)
        {
            var hand = Group("Operator right hand", station, Vector3.zero).gameObject.AddComponent<FactoryOperatorHand>();
            hand.palm = Group("Gripping hand", hand.transform, Vector3.zero);
            Box("Palm", hand.palm, new Vector3(0, 0, -.06f), new Vector3(.15f, .17f, .075f), Cream);
            for (int i = 0; i < 4; i++)
            {
                float x = -.057f + i * .038f;
                Box("Finger", hand.palm, new Vector3(x, .10f, -.025f), new Vector3(.032f, .10f, .045f), Cream);
                Box("Curled fingertip", hand.palm, new Vector3(x, .13f, .018f), new Vector3(.032f, .045f, .06f), Cream);
            }
            var thumb = Box("Thumb", hand.palm, new Vector3(.09f, .015f, -.008f), new Vector3(.055f, .11f, .055f), Cream);
            thumb.localRotation = Quaternion.Euler(0, 0, -35);
            Box("Cuff", hand.palm, new Vector3(0, -.12f, -.06f), new Vector3(.17f, .085f, .10f), Teal);
            hand.sleeve = Box("Right forearm sleeve", hand.transform, Vector3.zero, Vector3.one, Teal);
            hand.Release(); return hand;
        }
        public void Attach(Transform grip)
        {
            Grip = grip; palm.SetParent(grip, false); palm.localPosition = Vector3.zero;
            Vector3 scale = grip.lossyScale;
            palm.localScale = new Vector3(1 / Mathf.Abs(scale.x), 1 / Mathf.Abs(scale.y), 1 / Mathf.Abs(scale.z));
            palm.localRotation = Quaternion.identity; palm.gameObject.SetActive(true); sleeve.gameObject.SetActive(true);
            FollowArm();
        }
        public void Release()
        {
            Grip = null;
            if (palm) { palm.SetParent(transform, false); palm.gameObject.SetActive(false); }
            if (sleeve) sleeve.gameObject.SetActive(false);
        }
        void LateUpdate() { if (Grip) FollowArm(); }
        public void SyncPose() { if (Grip) FollowArm(); }
        void FollowArm()
        {
            var camera = Camera.main; if (!camera) return;
            Vector3 wrist = palm.TransformPoint(new Vector3(0, -.14f, -.06f));
            Vector3 elbow = camera.transform.TransformPoint(new Vector3(.30f, -.38f, .35f));
            sleeve.position = (wrist + elbow) * .5f;
            sleeve.rotation = Quaternion.FromToRotation(Vector3.up, wrist - elbow);
            sleeve.localScale = new Vector3(.13f, Vector3.Distance(wrist, elbow), .12f);
        }
        void OnDisable() { Release(); }
    }
}
