using UnityEngine;

namespace Cardboard
{
    [RequireComponent(typeof(FactoryPhysicsProp))]
    public sealed class FactoryKitchenFood : MonoBehaviour
    {
        public bool Rat { get; private set; }
        public bool Sliced { get; private set; }
        public float Amount { get; private set; }
        public Mesh Model { get; private set; }
        public Material[] Materials { get; private set; }
        public bool Peeled { get; private set; }
        public float PeelProgress => Peeled ? 1f : potatoSkin?.Progress ?? 0f;
        public bool OnPeelingStand { get; private set; }
        public bool Dusty { get; private set; }
        public bool Burnt { get; private set; }
        public bool Fried => !Burnt && fryTime >= FryDuration;
        public bool ReadyForPot => Sliced && Peeled && !Burnt && (!Rat || Fried);
        public FactoryPhysicsProp Prop { get; private set; }
        public string PickupPrompt => Burnt ? "E - PICK UP BURNT PIECE" : Rat && Fried ? "E - PICK UP COOKED RAT" :
            Dusty ? "E - PICK UP DUSTY PIECE" : "E - PICK UP " + (Rat ? "RAT PIECE" : "POTATO");
        public float FryDuration { get; private set; }
        public float FryProgress => Mathf.Clamp01(fryTime / FryDuration);
        public bool FitsLadle { get; private set; }
        float fryTime, lastSurfaceContact = float.NegativeInfinity;
        FactoryKitchenTool touchingSurface;
        FactoryPotatoSkin potatoSkin;
        Renderer skin;
        bool ownsModel;
        Mesh collisionModel;
        MaterialPropertyBlock appearance;
        Color[] rawColors;
        Color brown;
        float lastBrowning = -1f;

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
            Vector3 size = model.bounds.size;
            float thickness = Mathf.Min(size.x, size.y, size.z);
            FryDuration = Mathf.Clamp(29f * Mathf.Pow(thickness / .067f, .68f), 8f, 44f);
            FitsLadle = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < .225f;
            brown = rat ? new Color(.66f, .37f, .20f) : new Color(.79f, .48f, .20f);
            rawColors = new Color[Materials.Length];
            for (int i = 0; i < Materials.Length; i++) rawColors[i] = Materials[i].GetColor("_BaseColor");
            Peeled = rat || source && source.Peeled;
            if (source)
            {
                fryTime = source.FryProgress * FryDuration;
                Dusty = source.Dusty;
                Burnt = source.Burnt;
                UpdateAppearance();
            }
        }

        internal void TurnForPeeling(float angle, Material flesh)
        {
            if (potatoSkin == null)
            {
                potatoSkin = new FactoryPotatoSkin(Model);
                skin.SetPropertyBlock(null);
                for (int i = 0; i < Materials.Length; i++) skin.SetPropertyBlock(null, i);
                GetComponent<MeshFilter>().sharedMesh = potatoSkin.Mesh;
                skin.sharedMaterials = new[] { Materials[0], flesh };
            }
            potatoSkin.Turn(angle);
        }

        internal float PeelHeight(Vector2 point) => potatoSkin?.HeightAt(point) ?? 0f;

        internal bool ScrapePeel(Vector2 from, Vector2 to, Material flesh)
        {
            if (!potatoSkin.Scrape(from, to)) return false;
            if (potatoSkin.Progress < 1f) return true;
            Peeled = true;
            GetComponent<MeshFilter>().sharedMesh = Model;
            Destroy(potatoSkin.Mesh);
            potatoSkin = null;
            for (int i = 0; i < Materials.Length - 1; i++)
            {
                Materials[i] = flesh;
                rawColors[i] = flesh.GetColor("_BaseColor");
            }
            skin.sharedMaterials = Materials;
            UpdateAppearance();
            return true;
        }

        public void RestForPeeling(Vector3 position, Quaternion rotation)
        {
            var body = Prop.Body;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            body.detectCollisions = false;
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            OnPeelingStand = true;
        }

        public void ReleaseFromPeeling()
        {
            if (!OnPeelingStand) return;
            var body = Prop.Body;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.WakeUp();
            OnPeelingStand = false;
        }

        public void Fry(float seconds)
        {
            if (Burnt) return;
            bool alreadyFried = Fried;
            fryTime += seconds;
            Burnt = fryTime >= FryDuration + Mathf.Clamp(FryDuration * .22f, 2f, 6f);
            if (Burnt || alreadyFried != Fried || Mathf.Abs(lastBrowning - FryProgress) > .025f) UpdateAppearance();
        }

        void UpdateAppearance()
        {
            if (potatoSkin != null) return;
            lastBrowning = FryProgress;
            float browning = Fried ? 1f : lastBrowning * .77f;
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

        internal bool OnSurface(FactoryKitchenTool tool) => touchingSurface == tool &&
            (Time.fixedTime - lastSurfaceContact <= Time.fixedDeltaTime * 2.1f || Prop.Body.IsSleeping() && tool.Contains(Prop.Body.position));

        internal void LeaveSurface()
        {
            touchingSurface = null;
        }

        internal void SettleOnBoard(FactoryKitchenTool board)
        {
            var body = Prop.Body;
            if (!Sliced || body.IsSleeping() || !board.Contains(body.position) || !OnSurface(board)) return;
            var boardBody = board.Prop.Body;
            Vector3 up = boardBody.rotation * Vector3.up;
            if (up.y < .7f) return;
            Vector3 relative = body.linearVelocity - boardBody.GetPointVelocity(body.position);
            if (Vector3.Dot(relative, up) > .25f || relative.sqrMagnitude > 2.25f) return;
            Vector3 shear = Vector3.ProjectOnPlane(relative, up);
            body.AddForce(-up * 3f - shear * 11f, ForceMode.Acceleration);
        }

        void OnCollisionStay(Collision collision) => RecordSurface(collision);

        void RecordSurface(Collision collision)
        {
            var tool = collision.collider.GetComponentInParent<FactoryKitchenTool>();
            if (!tool) return;
            Vector3 up = tool.transform.up;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var point = collision.GetContact(i);
                if (Vector3.Dot(point.normal, up) < .65f || tool.transform.InverseTransformPoint(point.point).y > .047f) continue;
                touchingSurface = tool;
                lastSurfaceContact = Time.fixedTime;
                return;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            RecordSurface(collision);
            if (Dusty || collision.contactCount == 0 || collision.GetContact(0).point.y > .19f ||
                collision.GetContact(0).normal.y < .45f) return;
            Dusty = true;
            UpdateAppearance();
        }

        void OnDestroy()
        {
            if (ownsModel && Model) Destroy(Model);
            if (potatoSkin != null) Destroy(potatoSkin.Mesh);
            if (collisionModel) Destroy(collisionModel);
        }
    }
}
