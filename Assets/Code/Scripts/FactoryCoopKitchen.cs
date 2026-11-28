#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cardboard
{
    internal sealed partial class FactoryCoopShift
    {
        const int KitchenMeshLimit = 600;
        FactoryKitchen kitchen;
        Transform kitchenLens;
        Transform[] kitchenProps;
        Transform[] kitchenParents;
        FactoryCoopObjectPose[] kitchenHome;
        bool[] movedKitchenProps, incomingKitchenProps;
        readonly List<FactoryCoopKitchenProp> kitchenChanges = new List<FactoryCoopKitchenProp>();
        Rigidbody[] kitchenBodies;
        int kitchenOwner;
        bool kitchenPreparing;
        string kitchenHint;
        FactoryCoopObjectPose receivedPeeler;
        Vector2 receivedPeelPointer;
        bool receivedPeelerPressed;
        readonly Dictionary<FactoryKitchenFood, int> kitchenIds = new Dictionary<FactoryKitchenFood, int>();
        readonly List<string> kitchenMeshes = new List<string>();
        readonly Dictionary<int, Mesh> receivedKitchenMeshes = new Dictionary<int, Mesh>();
        readonly Dictionary<int, Renderer> ingredients = new Dictionary<int, Renderer>();
        MaterialPropertyBlock kitchenColor;

        public int AppliedKitchenMesh { get; private set; }
        public bool LocalUsesKitchen => kitchenOwner == localRole;
        public bool LocalPeels => LocalUsesKitchen && kitchenPreparing;
        public string KitchenHint => kitchenHint;
        public bool NearKitchen(Vector3 position) => kitchen && (position - kitchen.transform.position).sqrMagnitude <= 30;

        public void DisposeMeshes()
        {
            foreach (var mesh in receivedKitchenMeshes.Values)
                if (mesh) UnityEngine.Object.Destroy(mesh);
            receivedKitchenMeshes.Clear();
        }

        void SetUpKitchen(bool host)
        {
            kitchen = game.kitchen;
            if (!kitchen) return;
            game.CoopKitchenAuthority = host;
            kitchenProps = kitchen.GetComponentsInChildren<Transform>(true)
                .Where(item => item != kitchen.transform).ToArray();
            kitchenParents = kitchenProps.Select(item => item.parent).ToArray();
            kitchenHome = kitchenProps.Select(FactoryCoopObjectPose.ReadLocal).ToArray();
            movedKitchenProps = new bool[kitchenProps.Length];
            incomingKitchenProps = new bool[kitchenProps.Length];
            kitchenBodies = kitchen.GetComponentsInChildren<Rigidbody>(true);
            world.Kitchen = new FactoryCoopKitchenState();
            if (host)
            {
                kitchenLens = new GameObject("Co-op kitchen grip").transform;
                kitchenLens.SetParent(game.transform);
            }
        }

        void ResetKitchenReplication()
        {
            kitchenOwner = 0;
            kitchenPreparing = false;
            kitchenHint = "";
            game.CoopActionLens = null;
            kitchenIds.Clear();
            kitchenMeshes.Clear();
            AppliedKitchenMesh = 0;
            foreach (var item in ingredients.Values)
                if (item) UnityEngine.Object.Destroy(item.gameObject);
            ingredients.Clear();
            DisposeMeshes();
            if (localRole == Host || !kitchen) return;
            foreach (var body in kitchenBodies)
            {
                if (!body) continue;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.isKinematic = true;
            }
        }

        void PositionKitchenGrip(FactoryCoopPlayer player)
        {
            if (kitchen.Preparing)
                kitchenLens.SetPositionAndRotation(kitchen.PrepCameraPosition, kitchen.PrepCameraRotation);
            else
                kitchenLens.SetPositionAndRotation(player.Position + Vector3.up * (1.62f + player.Jump), player.Look);
            game.CoopActionLens = kitchenLens;
        }

        bool UseKitchen(int role, FactoryCoopPlayer actor, FactoryInteractable target)
        {
            if (!kitchen || (kitchenOwner != 0 && kitchenOwner != role) ||
                (actor.Position - kitchen.transform.position).sqrMagnitude > 225) return false;
            if (kitchenOwner == 0 && HandsBusy(actor)) return false;
            PositionKitchenGrip(actor);
            if (kitchen.Preparing)
            {
                kitchen.EndPrep();
                return true;
            }
            if (kitchen.ReturnUtensil()) return true;
            if (!target) return false;
            bool used = false;
            if (target.TryGetComponent(out FactoryKitchenControl control))
                used = kitchen.Use(control.action, control.slot);
            else if (target.TryGetComponent(out FactoryKitchenFood food))
                used = kitchen.ResumePeeling(food) || food.Prop.Take(game);
            else if (target.TryGetComponent(out FactoryPhysicsProp prop) && kitchen.OwnsHeld(prop))
                used = prop.Take(game);
            if (used && (kitchen.CoopHolding || kitchen.Preparing)) kitchenOwner = role;
            if (kitchenOwner == 0) game.CoopActionLens = null;
            return used;
        }

        void StepKitchen()
        {
            if (!kitchen) return;
            kitchenHint = kitchen.CoopHint;
            kitchenPreparing = kitchen.Preparing;
            if (kitchenOwner == 0) return;
            var actor = Player(kitchenOwner);
            if (game.State != FactoryState.Working || (actor.Position - kitchen.transform.position).sqrMagnitude > 225)
            {
                ReleaseKitchen();
                return;
            }
            if (!kitchen.Preparing && !kitchen.CoopHolding)
            {
                kitchenOwner = 0;
                game.CoopActionLens = null;
                return;
            }
            PositionKitchenGrip(actor);
        }

        void ReleaseKitchen()
        {
            if (kitchen.Preparing) kitchen.EndPrep();
            if (kitchen.CoopHolding) game.objectCarry.Release(false);
            kitchenOwner = 0;
            game.CoopActionLens = null;
        }

        void ReadKitchen()
        {
            if (!kitchen) return;
            var state = world.Kitchen;
            state.Owner = kitchenOwner;
            state.Preparing = kitchen.Preparing;
            state.Holding = kitchen.CoopHolding;
            state.Stock = kitchen.RatStock;
            state.Portions = kitchen.Portions;
            state.Filled = kitchen.Service.Filled;
            state.Plated = kitchen.Service.Plated;
            state.RatMass = kitchen.RatInPot;
            state.PotatoMass = kitchen.PotatoInPot;
            state.Peel = kitchen.CoopPeel;
            state.PeelPointer = kitchen.CoopPeelPointer;
            state.PeelerPressed = kitchen.CoopPeelerPressed;
            state.Hint = kitchen.CoopHint;
            if (state.Preparing)
                state.View = new FactoryCoopPose { Position = kitchen.PrepCameraPosition, Rotation = kitchen.PrepCameraRotation };
            kitchenChanges.Clear();
            for (int i = 0; i < kitchenProps.Length; i++)
            {
                var item = kitchenProps[i];
                if (!item) continue;
                bool reparented = item.parent != kitchenParents[i];
                var pose = reparented ? FactoryCoopObjectPose.Read(item) : FactoryCoopObjectPose.ReadLocal(item);
                if (reparented || !pose.Matches(kitchenHome[i]))
                    kitchenChanges.Add(new FactoryCoopKitchenProp { Index = i, WorldSpace = reparented, Pose = pose });
            }
            state.Props = kitchenChanges.ToArray();
            var food = kitchen.GetComponentsInChildren<FactoryKitchenFood>(true);
            state.Pieces = new FactoryCoopIngredient[food.Length];
            int written = 0;
            foreach (var item in food)
            {
                if (!kitchenIds.TryGetValue(item, out int id))
                {
                    if (kitchenMeshes.Count >= KitchenMeshLimit) continue;
                    kitchenMeshes.Add(FactoryCoopKitchenMesh.Encode(item.Model));
                    id = kitchenMeshes.Count;
                    kitchenIds.Add(item, id);
                }
                state.Pieces[written++] = new FactoryCoopIngredient
                {
                    Id = id, Rat = item.Rat, Browning = item.FryProgress, Burnt = item.Burnt, Peeled = item.Peeled,
                    Pose = FactoryCoopObjectPose.Read(item.transform), Visible = item.gameObject.activeInHierarchy
                };
            }
            if (written != state.Pieces.Length) System.Array.Resize(ref state.Pieces, written);
        }

        void SelectKitchenMesh(int acknowledgedMesh)
        {
            if (!kitchen) return;
            var state = world.Kitchen;
            state.MeshId = Mathf.Clamp(acknowledgedMesh, 0, kitchenMeshes.Count) + 1;
            state.MeshData = state.MeshId <= kitchenMeshes.Count ? kitchenMeshes[state.MeshId - 1] : null;
        }

        bool ApplyKitchen(FactoryCoopKitchenState state)
        {
            if (!kitchen) return state == null;
            if (state == null || state.Props == null || state.Props.Length > kitchenProps.Length || state.Pieces == null ||
                state.Pieces.Length > KitchenMeshLimit || state.Owner < 0 || state.Owner > MaximumPlayers) return false;
            System.Array.Clear(incomingKitchenProps, 0, incomingKitchenProps.Length);
            foreach (var prop in state.Props)
            {
                if (prop.Index < 0 || prop.Index >= kitchenProps.Length || incomingKitchenProps[prop.Index]) return false;
                incomingKitchenProps[prop.Index] = true;
            }
            kitchenOwner = state.Owner;
            kitchenPreparing = state.Preparing;
            kitchenHint = state.Hint;
            kitchen.ApplyCoopCounters(state);
            if (LocalPeels)
            {
                game.CoopToolView = true;
                game.CoopToolCamera = state.View;
            }
            for (int i = 0; i < kitchenProps.Length; i++)
                if (movedKitchenProps[i] && !incomingKitchenProps[i] && kitchenProps[i]) kitchenHome[i].ApplyLocal(kitchenProps[i]);
            foreach (var prop in state.Props)
            {
                var item = kitchenProps[prop.Index];
                if (!item) continue;
                if (prop.WorldSpace) prop.Pose.Apply(item);
                else prop.Pose.ApplyLocal(item);
            }
            (movedKitchenProps, incomingKitchenProps) = (incomingKitchenProps, movedKitchenProps);
            if (LocalPeels)
            {
                receivedPeeler = FactoryCoopObjectPose.Read(kitchen.peeler);
                receivedPeelPointer = state.PeelPointer;
                receivedPeelerPressed = state.PeelerPressed;
            }
            if (state.MeshId > AppliedKitchenMesh && !string.IsNullOrEmpty(state.MeshData))
            {
                if (state.MeshId != AppliedKitchenMesh + 1 || state.MeshId > KitchenMeshLimit) return false;
                var mesh = FactoryCoopKitchenMesh.Decode(state.MeshData);
                if (!mesh) return false;
                receivedKitchenMeshes.Add(state.MeshId, mesh);
                AppliedKitchenMesh = state.MeshId;
            }
            foreach (var item in state.Pieces)
            {
                if (!receivedKitchenMeshes.TryGetValue(item.Id, out var mesh)) continue;
                if (!ingredients.TryGetValue(item.Id, out var render))
                {
                    var node = new GameObject(item.Rat ? "Shared rat piece" : "Shared potato piece");
                    node.transform.SetParent(game.transform);
                    node.AddComponent<MeshFilter>().sharedMesh = mesh;
                    render = node.AddComponent<MeshRenderer>();
                    var shape = node.AddComponent<BoxCollider>();
                    shape.size = mesh.bounds.size;
                    shape.center = mesh.bounds.center;
                    node.AddComponent<FactoryInteractable>().action = FactoryAction.PhysicsProp;
                    ingredients.Add(item.Id, render);
                }
                item.Pose.Apply(render.transform);
                render.gameObject.SetActive(item.Visible);
                ApplyIngredientAppearance(render, mesh, item);
            }
            foreach (int id in ingredients.Keys.ToArray())
            {
                if (state.Pieces.Any(item => item.Id == id)) continue;
                UnityEngine.Object.Destroy(ingredients[id].gameObject);
                ingredients.Remove(id);
            }
            return true;
        }

        public void PresentPeeler(Vector2 pixels, bool pressed)
        {
            if (!LocalPeels || !kitchen.peeler) return;
            var movement = FactoryKitchen.MovePeelPointer(receivedPeelPointer, pixels) - receivedPeelPointer;
            float height = pressed == receivedPeelerPressed ? 0 : pressed ? -.063f : .063f;
            var offset = receivedPeeler.Rotation * new Vector3(movement.x, height, movement.y);
            kitchen.peeler.position = receivedPeeler.Position + offset;
        }

        void ApplyIngredientAppearance(Renderer render, Mesh mesh, FactoryCoopIngredient item)
        {
            var materials = item.Rat ? kitchen.ratMaterials : kitchen.potatoMaterials;
            var palette = new Material[mesh.subMeshCount];
            kitchenColor ??= new MaterialPropertyBlock();
            for (int i = 0; i < palette.Length; i++)
                palette[i] = !item.Rat && item.Peeled && i < palette.Length - 1 ? kitchen.potatoPaper :
                    materials[Mathf.Min(i, materials.Length - 1)];
            render.sharedMaterials = palette;
            for (int i = 0; i < palette.Length; i++)
            {
                var raw = palette[i].GetColor("_BaseColor");
                kitchenColor.SetColor("_BaseColor", Color.Lerp(raw, item.Burnt ? new Color(.12f, .08f, .05f) :
                    new Color(.66f, .37f, .20f), item.Burnt ? 1 : Mathf.Clamp01(item.Browning)));
                render.SetPropertyBlock(kitchenColor, i);
            }
        }
    }
}
#endif
