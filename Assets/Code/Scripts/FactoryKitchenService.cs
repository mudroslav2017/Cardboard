using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    public sealed class FactoryKitchenService : MonoBehaviour
    {
        enum LadleAction { None, Scoop, Pour }
        const float ScoopDuration = .76f, PourDelay = .39f, PourDuration = .42f;
        const int VisiblePieces = 6;

        sealed class Ingredient
        {
            public FactoryKitchenFood piece;
            public Transform visual;
        }

        sealed class Place
        {
            public FactoryPhysicsProp bowl;
            public Transform bowlHome, liquid, tray;
            public Collider trayTarget;
            public bool full, onTray, trayLoaded;
            public Transform bite;
            public Vector3 biteHome;
            public readonly List<Ingredient> contents = new List<Ingredient>();
        }

        public bool BatchCooked { get; private set; }
        public bool HasScoop => scoop.Count > 0;
        internal bool LadleBusy => ladleAction != LadleAction.None;
        public int Filled { get; private set; }
        public int Plated { get; private set; }
        readonly Place[] places = new Place[FactoryKitchen.BowlsPerPot];
        readonly List<Ingredient> ingredients = new List<Ingredient>();
        readonly List<Ingredient> potContents = new List<Ingredient>();
        readonly List<Ingredient> scoop = new List<Ingredient>();
        readonly int[] trayBowl = new int[FactoryKitchen.BowlsPerPot];
        int ratsInPot, potatoesInPot;
        FactoryKitchen kitchen;
        Transform counter, ladleLiquid, stream;
        AudioClip splash, plateTap;
        Vector3 actionStart, dippedAt, pourStart;
        Place pouring;
        LadleAction ladleAction;
        float ladleAge, ladleYaw;
        bool flowStarted;

        internal void SetUp(FactoryKitchen kitchen)
        {
            this.kitchen = kitchen;
            Material soup = kitchen.broth.GetComponent<Renderer>().sharedMaterial;
            Material wood = kitchen.board.transform.Find("Banded chopping surface").GetComponent<Renderer>().sharedMaterial;
            var oldBowl = kitchen.traySoup.transform.Find("Soup bowl");
            Material tin = oldBowl.GetComponent<Renderer>().sharedMaterial;
            Material kraft = kitchen.game.gearEdge ? kitchen.game.gearEdge : wood;
            counter = CardboardArt.Group("Soup serving counter", transform, new Vector3(7.25f, 0, .18f));
            var counterLight = CardboardArt.Group("Serving work light", counter, new Vector3(0, 3.05f, -.42f)).gameObject.AddComponent<Light>();
            counterLight.type = LightType.Point;
            counterLight.color = new Color(1f, .89f, .72f);
            counterLight.intensity = 2.5f;
            counterLight.range = 4f;
            Shape("Worktop", counter, PrimitiveType.Cube, new Vector3(0, 1f, 0), new Vector3(3.84f, .09f, 1.16f), wood, true);
            Shape("Apron", counter, PrimitiveType.Cube, new Vector3(0, .90f, 0), new Vector3(3.71f, .14f, 1.04f), kraft);
            foreach (float x in new[] { -1.75f, 1.75f })
                foreach (float z in new[] { -.43f, .43f })
                    Shape("Folded leg", counter, PrimitiveType.Cube, new Vector3(x, .47f, z), new Vector3(.12f, .94f, .12f), kraft, true);

            for (int i = 0; i < places.Length; i++)
            {
                int column = i % 5;
                float row = i < 5 ? -.25f : .24f;
                var place = new Place();
                places[i] = place;
                place.bowlHome = CardboardArt.Group("Bowl rest", counter, new Vector3(-1.70f + .34f * column, 1.06f, row));
                Transform bowl = CardboardArt.Group("Soup bowl " + (i + 1), place.bowlHome, Vector3.zero);
                Shape("Bowl base", bowl, PrimitiveType.Cylinder, new Vector3(0, .008f, 0), new Vector3(.203f, .012f, .203f), tin);
                for (int j = 0; j < 12; j++)
                {
                    Quaternion rotation = Quaternion.Euler(0, j * 30, 0);
                    Vector3 radial = rotation * Vector3.forward;
                    Transform side = Shape("Folded bowl rim", bowl, PrimitiveType.Cube, radial * .114f + Vector3.up * .034f,
                        new Vector3(.064f, .063f, .012f), tin);
                    side.localRotation = rotation * Quaternion.Euler(15, 0, 0);
                }
                place.liquid = Shape("Soup in bowl", bowl, PrimitiveType.Cylinder, new Vector3(0, .055f, 0),
                    new Vector3(.205f, .005f, .205f), soup);
                var shape = bowl.gameObject.AddComponent<BoxCollider>();
                shape.size = new Vector3(.22f, .064f, .22f);
                shape.center = Vector3.up * .029f;
                var body = bowl.gameObject.AddComponent<Rigidbody>();
                body.mass = .25f;
                place.bowl = bowl.gameObject.AddComponent<FactoryPhysicsProp>();
                place.bowl.itemName = "soup bowl";
                AddControl(bowl, KitchenAction.Bowl, i);

                place.tray = CardboardArt.Group("Serving tray " + (i + 1), counter,
                    new Vector3(.12f + .365f * column, 1.052f, row));
                Shape("Tray base", place.tray, PrimitiveType.Cube, Vector3.zero, new Vector3(.326f, .022f, .378f), kraft);
                foreach (float side in new[] { -1f, 1f })
                {
                    Shape("Tray edge", place.tray, PrimitiveType.Cube, new Vector3(side * .155f, .021f, 0),
                        new Vector3(.021f, .026f, .378f), wood);
                    Shape("Tray edge", place.tray, PrimitiveType.Cube, new Vector3(0, .021f, side * .179f),
                        new Vector3(.326f, .026f, .021f), wood);
                }
                var trayTarget = place.tray.gameObject.AddComponent<BoxCollider>();
                trayTarget.size = new Vector3(.329f, .055f, .38f);
                trayTarget.center = Vector3.up * .018f;
                trayTarget.isTrigger = true;
                place.trayTarget = trayTarget;
                AddControl(place.tray, KitchenAction.Tray, i);
            }
            stream = Shape("Poured soup", counter, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, soup);
            splash = ServingSound("Soup pour", .34f, 120f, true);
            plateTap = ServingSound("Bowl touches tray", .11f, 530f, false);
            ResetService();
        }

        internal void SetUpLadle(FactoryKitchenUtensil ladle)
        {
            ladleLiquid = Shape("Ladle contents", ladle.transform, PrimitiveType.Cylinder, new Vector3(0, .01f, .14f),
                new Vector3(.145f, .008f, .145f), kitchen.broth.GetComponent<Renderer>().sharedMaterial);
            ladleLiquid.gameObject.SetActive(false);
        }

        internal void ResetService()
        {
            foreach (var ingredient in ingredients)
            {
                ingredient.visual.gameObject.SetActive(false);
                Destroy(ingredient.visual.gameObject);
                Destroy(ingredient.piece.gameObject);
            }
            ingredients.Clear();
            potContents.Clear();
            scoop.Clear();
            BatchCooked = false;
            EndGesture();
            Filled = Plated = 0;
            ratsInPot = potatoesInPot = 0;
            if (ladleLiquid) ladleLiquid.gameObject.SetActive(false);
            for (int i = 0; i < places.Length; i++)
            {
                var place = places[i];
                place.contents.Clear();
                place.bite = null;
                place.full = place.onTray = place.trayLoaded = false;
                place.liquid.gameObject.SetActive(false);
                place.tray.SetParent(counter, false);
                place.tray.SetLocalPositionAndRotation(new Vector3(.12f + .365f * (i % 5), 1.052f, i < 5 ? -.25f : .24f), Quaternion.identity);
                place.tray.gameObject.SetActive(true);
                place.trayTarget.enabled = true;
                place.bowl.gameObject.SetActive(true);
                place.bowl.GetComponent<Collider>().enabled = true;
                ParkBowl(place.bowl, place.bowlHome, Vector3.zero);
            }
        }

        internal void AddIngredient(FactoryKitchenFood piece)
        {
            var visual = CardboardArt.Group(piece.Rat ? "Cut rat in soup" : "Cut potato in soup", kitchen.pot, Vector3.zero);
            var filter = visual.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = piece.Model;
            var renderer = visual.gameObject.AddComponent<MeshRenderer>();
            var source = piece.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            var properties = new MaterialPropertyBlock();
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                source.GetPropertyBlock(properties, i);
                renderer.SetPropertyBlock(properties, i);
            }
            var ingredient = new Ingredient { piece = piece, visual = visual };
            ingredients.Add(ingredient);
            potContents.Add(ingredient);
            if (piece.Rat) ratsInPot++;
            else potatoesInPot++;
            Vector3 local = kitchen.pot.InverseTransformPoint(piece.Prop.Body.position);
            Vector2 offset = Vector2.ClampMagnitude(new Vector2(local.x, local.z), .23f);
            visual.SetLocalPositionAndRotation(new Vector3(offset.x, .20f, offset.y), Quaternion.Inverse(kitchen.pot.rotation) * piece.Prop.Body.rotation);
            visual.gameObject.SetActive(potContents.Count <= 22);
            // Keep the source alive to own its cut mesh. Serve with a render-only object so nested rigidbodies cannot restore stale poses.
            piece.gameObject.SetActive(false);
        }

        internal void FinishBoiling() => BatchCooked = true;
        internal int IngredientPieces(bool rat) => rat ? ratsInPot : potatoesInPot;

        internal bool StartScoop(FactoryKitchenUtensil ladle, float yaw)
        {
            if (LadleBusy || HasScoop || !BatchCooked || kitchen.Portions <= 0) return false;
            dippedAt = kitchen.broth.position;
            StartGesture(ladle, yaw, LadleAction.Scoop);
            return true;
        }

        internal bool StartPour(int slot, FactoryKitchenUtensil ladle, float yaw)
        {
            var place = places[slot];
            if (LadleBusy || !HasScoop || place.full) return false;
            pouring = place;
            flowStarted = false;
            StartGesture(ladle, yaw, LadleAction.Pour);
            return true;
        }

        void StartGesture(FactoryKitchenUtensil ladle, float yaw, LadleAction action)
        {
            actionStart = ladle.Tip;
            ladleYaw = yaw;
            ladleAge = 0;
            ladleAction = action;
        }

        internal bool GetLadlePose(out Vector3 tip, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(0, ladleYaw, 0);
            tip = actionStart;
            if (ladleAction == LadleAction.Scoop)
            {
                Vector3 inside = kitchen.pot.TransformPoint(Vector3.up * Mathf.Max(.055f, kitchen.broth.localPosition.y - .018f));
                Vector3 above = kitchen.pot.TransformPoint(Vector3.up * .46f);
                tip = ladleAge < .24f ? Vector3.Lerp(actionStart, above, Ease(ladleAge / .24f)) :
                    Vector3.Lerp(inside, above, ladleAge < .50f ? 1f - Ease((ladleAge - .24f) / .23f) :
                        Ease((ladleAge - .50f) / (ScoopDuration - .50f)));
                return true;
            }
            if (ladleAction != LadleAction.Pour) return false;
            tip = Vector3.Lerp(actionStart, BowlPourPoint(pouring), Ease(ladleAge / .25f));
            rotation = Quaternion.Euler(0, ladleYaw, -76f * Ease((ladleAge - .23f) / .16f));
            return true;
        }

        static float Ease(float value) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(value));
        static Vector3 BowlPourPoint(Place place) => place.bowl.transform.TransformPoint(Vector3.up * .275f);

        internal void Tick(FactoryKitchenUtensil ladle, float dt)
        {
            if (!LadleBusy) return;
            if (!ladle)
            {
                if (ladleAction == LadleAction.Pour && flowStarted) AnimatePour(1f);
                EndGesture();
                return;
            }
            ladleAge += dt;
            if (ladleAction == LadleAction.Scoop)
            {
                if (ladleAge >= .49f && !HasScoop) FillLadle(ladle);
                if (ladleAge >= ScoopDuration) EndGesture();
                return;
            }
            if (ladleAge < PourDelay) return;
            if (!flowStarted) BeginFlow();
            float level = Mathf.Clamp01((ladleAge - PourDelay) / PourDuration);
            AnimatePour(level);
            if (level >= 1) EndGesture();
        }

        void EndGesture()
        {
            pouring = null;
            flowStarted = false;
            ladleAction = LadleAction.None;
            stream.gameObject.SetActive(false);
        }

        void FillLadle(FactoryKitchenUtensil ladle)
        {
            PickIngredients(true);
            PickIngredients(false);
            for (int i = 0; i < scoop.Count; i++)
            {
                var ingredient = scoop[i];
                var piece = ingredient.visual;
                piece.SetParent(ladle.transform, false);
                piece.SetLocalPositionAndRotation(
                    new Vector3((i % 3 - 1) * .028f, .018f, .124f + (i / 3 % 2) * .039f), Quaternion.Euler(0, i * 57, 0));
                piece.localScale = Vector3.one * ScaleToFit(ingredient, .044f);
                piece.gameObject.SetActive(i < VisiblePieces);
            }
            ladleLiquid.gameObject.SetActive(true);
            kitchen.Scooped();
            float surface = Mathf.Lerp(.06f, .22f, kitchen.Portions / (float)places.Length);
            for (int i = 0; i < potContents.Count; i++)
            {
                var piece = potContents[i].visual;
                Vector3 at = piece.localPosition;
                at.y = surface;
                piece.localPosition = at;
                piece.gameObject.SetActive(i < 22);
            }
        }

        void PickIngredients(bool rat)
        {
            float stock = 0;
            int count = 0;
            foreach (var piece in potContents)
            {
                if (piece.piece.Rat != rat) continue;
                count++;
                stock += piece.piece.Amount;
            }
            int allowed = count == 0 ? 0 : Mathf.Max(1, count - kitchen.Portions + 1);
            float target = stock / kitchen.Portions;
            float collected = 0;
            for (int i = 0; i < allowed; i++)
            {
                int closest = -1;
                float score = float.PositiveInfinity;
                for (int j = 0; j < potContents.Count; j++)
                {
                    var piece = potContents[j];
                    if (piece.piece.Rat != rat) continue;
                    float mismatch = Mathf.Abs(target - collected - piece.piece.Amount);
                    if (i > 0 && kitchen.Portions > 1 && mismatch >= Mathf.Abs(target - collected)) continue;
                    float candidate = (piece.visual.position - dippedAt).sqrMagnitude + mismatch * .055f;
                    if (candidate >= score) continue;
                    closest = j;
                    score = candidate;
                }
                if (closest < 0) break;
                var picked = potContents[closest];
                collected += picked.piece.Amount;
                if (!rat && i == 0) scoop.Insert(Mathf.Min(1, scoop.Count), picked);
                else scoop.Add(picked);
                potContents.RemoveAt(closest);
            }
        }

        void BeginFlow()
        {
            var place = pouring;
            flowStarted = true;
            pourStart = BowlPourPoint(place);
            stream.gameObject.SetActive(true);
            AudioSource.PlayClipAtPoint(splash, place.bowl.transform.position, .52f);
            place.liquid.gameObject.SetActive(true);
            ladleLiquid.gameObject.SetActive(false);
            for (int i = 0; i < scoop.Count; i++)
            {
                var ingredient = scoop[i];
                ingredient.visual.SetParent(place.bowl.transform, true);
                ingredient.visual.gameObject.SetActive(i < VisiblePieces);
                place.contents.Add(ingredient);
            }
            scoop.Clear();
        }

        void AnimatePour(float level)
        {
            var place = pouring;
            var bowl = place.bowl.transform;
            place.liquid.localPosition = Vector3.up * Mathf.Lerp(.01f, .055f, level);
            Vector3 to = place.liquid.position;
            Vector3 flow = pourStart - to;
            stream.position = (pourStart + to) * .5f;
            stream.rotation = Quaternion.FromToRotation(Vector3.up, flow);
            float width = Mathf.Lerp(.033f, .019f, level);
            stream.localScale = new Vector3(width, flow.magnitude * .5f, width);
            Vector3 start = bowl.InverseTransformPoint(pourStart - Vector3.up * .025f);
            for (int i = 0; i < place.contents.Count; i++)
            {
                var ingredient = place.contents[i];
                if (i >= VisiblePieces && level < 1f) continue;
                Vector3 resting = i < 2 ? Vector3.right * (i == 0 ? -.034f : .034f) :
                    Quaternion.Euler(0, i * 137.5f, 0) * Vector3.forward * .049f;
                float radius = .097f - resting.magnitude;
                if (i < 2) radius = Mathf.Min(radius, .049f);
                float scale = ScaleToFit(ingredient, radius);
                Quaternion rotation = Quaternion.Euler(0, i * 57f, 0);
                Vector3 end = resting + Vector3.up * .056f - rotation * (ingredient.piece.Model.bounds.center * scale);
                float fall = Mathf.Clamp01((level - Mathf.Min(i, VisiblePieces - 1) * .043f) * 1.55f);
                ingredient.visual.SetLocalPositionAndRotation(Vector3.Lerp(start, end, fall * fall), rotation);
                ingredient.visual.localScale = Vector3.one * scale;
            }
            if (level < 1) return;
            place.full = true;
            Filled++;
            place.bite = place.contents.Count > 0 ? place.contents[0].visual : null;
            if (place.bite) place.biteHome = place.bite.localPosition;
            if (Filled == places.Length) kitchen.game.Toast("Set all ten bowls on the serving trays.", 6);
        }

        static float ScaleToFit(Ingredient ingredient, float radius)
        {
            var extents = ingredient.piece.Model.bounds.extents;
            float span = new Vector2(extents.x, extents.z).magnitude;
            return Mathf.Min(1f, radius / Mathf.Max(.001f, span));
        }

        internal bool OwnsBowl(FactoryPhysicsProp prop) =>
            prop && prop.TryGetComponent(out FactoryKitchenControl control) && control.action == KitchenAction.Bowl;

        internal string BowlPrompt(int i) => places[i].onTray ? "SOUP TRAY" : Filled == places.Length
            ? "E - PICK UP SOUP BOWL" : places[i].full ? $"BOWL FILLED  /  {Filled}/{places.Length}" : "FILL WITH THE LADLE";

        internal string TrayPrompt(int i, FactoryPhysicsProp held) => places[i].trayLoaded ? $"SOUP TRAY  /  {Plated}/{places.Length}"
            : OwnsBowl(held) ? "E - SET BOWL ON TRAY" : $"SET A FILLED BOWL HERE  /  {Plated}/{places.Length}";

        internal bool TakeBowl(int i)
        {
            var place = places[i];
            if (Filled != places.Length || place.onTray || !place.bowl.Take(kitchen.game)) return false;
            place.bowl.transform.SetParent(counter, true);
            place.bowl.Body.isKinematic = false;
            place.bowl.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return true;
        }

        internal bool SetBowlOnTray(int i, FactoryPhysicsProp held)
        {
            var tray = places[i];
            if (tray.trayLoaded || Filled != places.Length || !OwnsBowl(held)) return false;
            int bowlIndex = held.GetComponent<FactoryKitchenControl>().slot;
            var bowl = places[bowlIndex];
            kitchen.game.objectCarry.Release(false);
            ParkBowl(held, tray.tray, Vector3.up * .025f);
            held.GetComponent<Collider>().enabled = false;
            tray.trayLoaded = bowl.onTray = true;
            // The tray can be loaded in any order. Its worker follows the bowl that was actually placed there.
            trayBowl[i] = bowlIndex;
            Plated++;
            AudioSource.PlayClipAtPoint(plateTap, tray.tray.position, .40f);
            if (Plated == places.Length) kitchen.game.lunch.BeginKitchenService(this);
            return true;
        }

        internal Vector3 PickupFor(int i)
        {
            Vector3 local = counter.InverseTransformPoint(places[i].tray.position);
            return counter.TransformPoint(new Vector3(local.x, 0, -.98f));
        }

        internal void GiveTray(int i, Transform worker)
        {
            var place = places[i];
            place.trayTarget.enabled = false;
            place.tray.SetParent(worker, false);
            place.tray.SetLocalPositionAndRotation(new Vector3(0, 1.02f, .39f), Quaternion.identity);
        }

        internal void ParkTray(int i, Transform meal)
        {
            var tray = places[i].tray;
            tray.SetParent(counter, true);
            tray.SetPositionAndRotation(meal.position + Vector3.up * .018f, meal.rotation);
        }

        internal void PoseBite(int trayIndex, Vector3 mouth, float lift)
        {
            var bowl = places[trayBowl[trayIndex]];
            if (bowl.bite) bowl.bite.position = Vector3.Lerp(bowl.bowl.transform.TransformPoint(bowl.biteHome), mouth, lift);
        }

        internal void ClearTrays()
        {
            if (Plated != places.Length) return;
            foreach (var place in places)
            {
                place.tray.SetParent(counter, true);
                place.tray.gameObject.SetActive(false);
            }
        }

        static void ParkBowl(FactoryPhysicsProp bowl, Transform parent, Vector3 offset)
        {
            var body = bowl.Body;
            if (!body.isKinematic) body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            body.useGravity = false;
            bowl.transform.SetParent(parent, false);
            bowl.transform.SetLocalPositionAndRotation(offset, Quaternion.identity);
        }

        void AddControl(Transform target, KitchenAction action, int i)
        {
            var control = target.gameObject.AddComponent<FactoryKitchenControl>();
            control.kitchen = kitchen;
            control.action = action;
            control.slot = i;
            target.gameObject.AddComponent<FactoryInteractable>().action = FactoryAction.Kitchen;
        }

        static Transform Shape(string name, Transform parent, PrimitiveType primitive, Vector3 offset,
            Vector3 scale, Material material, bool solid = false)
        {
            var model = GameObject.CreatePrimitive(primitive);
            model.name = name;
            model.transform.SetParent(parent, false);
            model.transform.localPosition = offset;
            model.transform.localScale = scale;
            model.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) model.GetComponent<Collider>().enabled = false;
            return model.transform;
        }

        static AudioClip ServingSound(string name, float length, float tone, bool wet)
        {
            const int rate = 22050;
            var samples = new float[Mathf.RoundToInt(length * rate)];
            var noise = new System.Random(39);
            float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)rate;
                filtered = Mathf.Lerp(filtered, (float)noise.NextDouble() * 2 - 1, wet ? .10f : .52f);
                float envelope = Mathf.Clamp01(time * 560f) * Mathf.Pow(1f - time / length, wet ? 1f : 3f);
                float wave = Mathf.Sin(Mathf.PI * 2f * tone * time + filtered * (wet ? 9f : .9f));
                samples[i] = envelope * (filtered * (wet ? .72f : .38f) + .24f * wave);
            }
            var sound = AudioClip.Create(name, samples.Length, 1, rate, false);
            sound.SetData(samples, 0);
            return sound;
        }

        void OnDestroy()
        {
            if (splash) Destroy(splash);
            if (plateTap) Destroy(plateTap);
        }
    }
}
