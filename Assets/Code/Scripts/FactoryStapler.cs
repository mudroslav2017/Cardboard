using UnityEngine;

namespace Cardboard
{
    public sealed class FactoryStapler : MonoBehaviour
    {
        [SerializeField] Material casing, trim, grip, staples, standCardboard;

        enum Stroke { Rest, Closing, Opening }

        const float CloseTime = .10f;
        const float OpenTime = .16f;
        const float GripFollowRate = 19f;
        const float MaxGripLag = .11f;
        const float MaxGripTurnLag = 12f;
        static readonly Vector3 HeldOffset = new Vector3(.32f, -.20f, .98f);
        // Bring the sole toward the view, with the free end of the lever hanging down.
        static readonly Quaternion HeldTurn = Quaternion.Euler(-90f, 0f, 0f);

        FactoryStaplerModel model;
        FactoryPhysicsProp prop;
        FactoryInteractable pickup;
        BoxCollider hitbox;
        Stroke stroke;
        float compression;
        GUIStyle promptStyle;

        void Awake()
        {
            var stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stand.name = "Kraft stand for the stapler";
            stand.transform.SetParent(transform.parent, false);
            stand.transform.localPosition = transform.localPosition - Vector3.up * .54f;
            stand.transform.localScale = new Vector3(.68f, .82f, .72f);
            var standRenderer = stand.GetComponent<MeshRenderer>();
            standRenderer.sharedMaterial = standCardboard;
            CardboardSurface.Configure(standRenderer);

            model = new FactoryStaplerModel(transform, casing, trim, grip);
            hitbox = gameObject.AddComponent<BoxCollider>();
            hitbox.center = new Vector3(0f, .008f, .022f);
            hitbox.size = new Vector3(.234f, .262f, .629f);
            gameObject.AddComponent<Rigidbody>().mass = .8f;
            prop = gameObject.AddComponent<FactoryPhysicsProp>();
            prop.itemName = "stapler";
            pickup = gameObject.AddComponent<FactoryInteractable>();
            pickup.action = FactoryAction.PhysicsProp;
            pickup.prompt = "E - PICK UP STAPLER";
        }

        internal void BeginGrip(Transform lens)
        {
            var body = prop.Body;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.detectCollisions = false;
            SetGripPose(lens.TransformPoint(HeldOffset), lens.rotation * HeldTurn);
        }

        internal void EndGrip()
        {
            var body = prop.Body;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.detectCollisions = true;
            stroke = Stroke.Rest;
            compression = 0f;
            model.SetSqueeze(0f);
        }

        internal void Press()
        {
            if (stroke == Stroke.Rest) stroke = Stroke.Closing;
        }

        internal void AdvanceGrip(Transform lens, float dt)
        {
            Vector3 targetPosition = lens.TransformPoint(HeldOffset);
            Quaternion targetRotation = lens.rotation * HeldTurn;
            float follow = 1f - Mathf.Exp(-GripFollowRate * dt);
            Vector3 position = Vector3.Lerp(transform.position, targetPosition, follow);
            Quaternion rotation = Quaternion.Slerp(transform.rotation, targetRotation, follow);
            SetGripPose(targetPosition + Vector3.ClampMagnitude(position - targetPosition, MaxGripLag),
                Quaternion.RotateTowards(targetRotation, rotation, MaxGripTurnLag));

            if (stroke == Stroke.Closing)
            {
                compression = Mathf.MoveTowards(compression, 1f, dt / CloseTime);
                model.SetSqueeze(Mathf.SmoothStep(0f, 1f, compression));
                if (compression < 1f) return;

                Transform muzzle = model.Muzzle;
                FactoryStapleProjectile.Launch(muzzle.position + muzzle.forward * .078f,
                    muzzle.rotation, staples, hitbox, CardboardRunner.Player);
                stroke = Stroke.Opening;
            }
            else if (stroke == Stroke.Opening)
            {
                compression = Mathf.MoveTowards(compression, 0f, dt / OpenTime);
                model.SetSqueeze(Mathf.SmoothStep(0f, 1f, compression));
                if (compression <= 0f) stroke = Stroke.Rest;
            }
        }

        void SetGripPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            prop.Body.position = position;
            prop.Body.rotation = rotation;
        }

        void OnGUI()
        {
            var game = CardboardFactoryGame.Instance;
            if (!game || game.PlayerBlocked || game.CaptureActive || game.CameraOverride || CardboardMarker.DrawingMode)
                return;
            if (game.Focus != pickup || prop.IsHeld) return;
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label)
                {
                    font = CardboardTypography.DefaultFont, fontSize = 16, alignment = TextAnchor.MiddleCenter
                };
                promptStyle.normal.textColor = new Color(.99f, .91f, .76f);
            }
            float scale = Mathf.Min(Screen.width / 1080f, Screen.height / 660f);
            if (scale <= 0f) return;
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float x = Screen.width / scale * .5f, y = Screen.height / scale * .5f;
            var panel = new Rect(x - 160f, y + 22f, 320f, 32f);
            GUI.color = new Color(.09f, .12f, .11f, .94f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(panel, pickup.prompt, promptStyle);
            GUI.DrawTexture(new Rect(x - 1f, y - 1f, 2f, 2f), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }
    }
}
