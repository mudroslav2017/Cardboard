using UnityEngine;

namespace Cardboard
{
    [RequireComponent(typeof(FactoryPhysicsProp))]
    public sealed class FactoryKitchenFood : MonoBehaviour
    {
        public const float SecondsPerSide = 4.5f;
        public bool Rat { get; private set; }
        public bool Sliced { get; private set; }
        public float Amount { get; private set; }
        public Mesh Model { get; private set; }
        public Material[] Materials { get; private set; }
        public int PeelRemoved { get; private set; }
        public int PeelCount => Rat ? 0 : Materials.Length - 1;
        public bool Peeled => PeelRemoved >= PeelCount;
        public bool OnCuttingBoard { get; private set; }
        public bool Dusty { get; private set; }
        public bool Burnt { get; private set; }
        public bool Fried => !Burnt && firstSide >= SecondsPerSide && secondSide >= SecondsPerSide;
        public bool ReadyForPot => Sliced && Peeled && !Burnt && (!Rat || Fried);
        public FactoryPhysicsProp Prop { get; private set; }
        public string PickupPrompt => Burnt ? "E - PICK UP BURNT PIECE" : Dusty ? "E - PICK UP DUSTY PIECE" : "E - PICK UP " + (Rat ? "RAT PIECE" : "POTATO");
        public bool SideReady => (flipped ? secondSide : firstSide) >= SecondsPerSide;
        float firstSide, secondSide;
        bool flipped;
        Renderer skin;
        bool ownsModel;
        Mesh collisionModel;
        MaterialPropertyBlock appearance;
        Color[] rawColors;

        public void Setup(bool rat, float amount, Mesh model, Material[] materials, FactoryKitchenFood source)
        {
            Rat = rat;
            Sliced = source;
            Amount = amount;
            Model = model;
            ownsModel = source;
            Materials = (Material[])materials.Clone();
            Prop = GetComponent<FactoryPhysicsProp>();
            if (TryGetComponent(out MeshCollider shape)) collisionModel = shape.sharedMesh;
            skin = GetComponent<MeshRenderer>();
            appearance = new MaterialPropertyBlock();
            rawColors = new Color[Materials.Length];
            for (int i = 0; i < Materials.Length; i++) rawColors[i] = Materials[i].GetColor("_BaseColor");
            if (source)
            {
                PeelRemoved = source.PeelRemoved;
                firstSide = source.firstSide;
                secondSide = source.secondSide;
                flipped = source.flipped;
                Dusty = source.Dusty;
                Burnt = source.Burnt;
                UpdateAppearance();
            }
        }

        public void RemovePeel(Material flesh)
        {
            if (Peeled) return;
            Materials[PeelRemoved] = flesh;
            rawColors[PeelRemoved++] = flesh.GetColor("_BaseColor");
            skin.sharedMaterials = Materials;
            UpdateAppearance();
        }

        public void RestForCutting(Vector3 position, Quaternion rotation)
        {
            var body = Prop.Body;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            body.detectCollisions = false;
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            OnCuttingBoard = true;
        }

        public void ReleaseFromCutting()
        {
            if (!OnCuttingBoard) return;
            var body = Prop.Body;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.WakeUp();
            OnCuttingBoard = false;
        }

        public void Fry(float seconds)
        {
            if (Burnt) return;
            if (flipped) secondSide += seconds;
            else firstSide += seconds;
            Burnt = Mathf.Max(firstSide, secondSide) >= SecondsPerSide + 4f;
            UpdateAppearance();
        }

        void UpdateAppearance()
        {
            float browning = Mathf.Max(firstSide, secondSide) / SecondsPerSide;
            Color brown = Rat ? new Color(.66f, .37f, .20f) : new Color(.79f, .48f, .20f);
            for (int i = 0; i < rawColors.Length; i++)
            {
                bool edge = i == rawColors.Length - 1;
                Color charred = edge ? new Color(.16f, .10f, .060f) : new Color(.072f, .054f, .040f);
                SetTint(Burnt ? charred : Color.Lerp(rawColors[i], brown, edge ? browning * .82f : browning), i);
            }
        }

        void SetTint(Color tint, int material)
        {
            if (Dusty) tint = Color.Lerp(tint, new Color(.30f, .24f, .17f), .39f);
            appearance.SetColor("_BaseColor", tint);
            skin.SetPropertyBlock(appearance, material);
        }

        public void Flip()
        {
            flipped = !flipped;
            var body = Prop.Body;
            body.linearVelocity = new Vector3(body.linearVelocity.x * .25f, .85f, body.linearVelocity.z * .25f);
            body.angularVelocity = transform.right * 10f;
            body.WakeUp();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (Dusty || collision.contactCount == 0 || collision.GetContact(0).point.y > .19f ||
                collision.GetContact(0).normal.y < .45f) return;
            Dusty = true;
            UpdateAppearance();
        }

        void OnDestroy()
        {
            if (ownsModel && Model) Destroy(Model);
            if (collisionModel) Destroy(collisionModel);
        }
    }
}
