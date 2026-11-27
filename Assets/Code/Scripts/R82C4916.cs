using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace Cardboard
{
    [DefaultExecutionOrder(100)]
    internal sealed class R82C4916 : MonoBehaviour
    {
        const float Reach = 65f;
        const float ShotInterval = .28f;
        const float TraceDuration = .085f;
        const double EntryWindow = 3;
        const int KeyMask = 0x5b;
        static readonly byte[] Entry = { 78, 84, 120, 122, 122 };
        static readonly Color Pulse = new Color(.52f, .92f, 1f);
        static readonly Vector3 RestPosition = new Vector3(.19f, -.135f, .43f);
        const int ShotLayers = Physics.DefaultRaycastLayers & ~((1 << 10) | (1 << CardboardMarkerHand.HandLayer));

        readonly RaycastHit[] hits = new RaycastHit[64];
        CardboardFactoryGame game;
        R82C4916Model model;
        GameObject view;
        LineRenderer trace;
        AudioSource speaker;
        AudioClip pulseSound;
        GUIStyle prompt;
        int entered, entryDevice;
        double deadline;
        bool equipped, pendingEquip, mouseCaptured;
        float nextShot, traceUntil, hitUntil, recoil, raise;

        internal bool Drawn => equipped && CanUse;
        bool CanUse => CardboardRunner.Player && !game.PlayerBlocked && !game.CaptureActive &&
            !game.CameraOverride && !game.HandsBusy && !game.HasPart && !CardboardMarker.DrawingMode &&
            (!game.craneJob || !game.craneJob.HoldsPlayer);

        void Awake()
        {
            game = GetComponent<CardboardFactoryGame>();
            hideFlags = HideFlags.HideInInspector;
        }

        void OnEnable() => InputSystem.onEvent += ReadEvent;

        void OnDisable()
        {
            InputSystem.onEvent -= ReadEvent;
            Clear();
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            pendingEquip = mouseCaptured = false;
            entered = 0;
        }

        void ReadEvent(InputEventPtr input, InputDevice device)
        {
            if (!(device is Keyboard) || (!input.IsA<StateEvent>() && !input.IsA<DeltaStateEvent>())) return;
            if (!Application.isFocused || !CanUse)
            {
                entered = 0;
                return;
            }
            ReadKeys(input, device);
        }

        void ReadKeys(InputEventPtr input, InputDevice device)
        {
            Key pressed = Key.None;
            foreach (var control in input.EnumerateChangedControls(device, .5f))
            {
                if (!(control is KeyControl key)) continue;
                if (pressed != Key.None)
                {
                    entered = 0;
                    return;
                }
                pressed = key.keyCode;
            }
            if (pressed != Key.None) ReadKey(pressed, device.deviceId, input.time);
        }

        void ReadKey(Key key, int device, double time)
        {
            if (time > deadline || device != entryDevice) entered = 0;
            int encoded = (int)key ^ KeyMask;
            if (encoded != Entry[entered]) entered = 0;
            if (encoded != Entry[entered]) return;
            if (entered == 0)
            {
                deadline = time + EntryWindow;
                entryDevice = device;
            }
            if (++entered != Entry.Length) return;
            entered = 0;
            pendingEquip = true;
        }

        void LateUpdate()
        {
            bool captured = Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            var mouse = Mouse.current;
            bool useMouse = captured && mouseCaptured && mouse != null;
            mouseCaptured = captured;
            if (!CanUse)
            {
                entered = 0;
                pendingEquip = false;
                Hide();
                return;
            }

            var lens = Camera.main;
            if (!lens) { Hide(); return; }
            if (pendingEquip)
            {
                pendingEquip = false;
                if (!equipped)
                {
                    if (!Equip()) return;
                    useMouse = false;
                }
            }
            if (!equipped) return;
            if (useMouse && mouse.rightButton.wasPressedThisFrame)
            {
                Clear();
                return;
            }

            if (view.transform.parent != lens.transform) view.transform.SetParent(lens.transform, false);
            view.SetActive(true);
            float now = Time.time;
            if (useMouse && mouse.leftButton.wasPressedThisFrame && now >= nextShot)
                Fire(lens.ViewportPointToRay(new Vector3(.5f, .5f)), now);
            raise = Mathf.MoveTowards(raise, 0f, Time.deltaTime * 4f);
            recoil *= Mathf.Exp(-Time.deltaTime * 14f);
            view.transform.localPosition = RestPosition + new Vector3(0f, -.15f * raise, -.075f * recoil);
            view.transform.localRotation = Quaternion.Euler(-4f - 7f * recoil + 20f * raise, -6f, -5f);
            if (trace.enabled && now >= traceUntil) trace.enabled = false;
        }

        bool Equip()
        {
            if (!view && !CreateView()) return false;
            equipped = true;
            raise = 1f;
            nextShot = Time.time + .15f;
            return true;
        }

        bool CreateView()
        {
            if (model == null)
            {
                model = R82C4916Model.Load(game.gearPaper ? game.gearPaper.shader : Shader.Find("Universal Render Pipeline/Lit"));
                if (model == null) return false;
            }

            view = new GameObject("R82C4916", typeof(MeshFilter), typeof(MeshRenderer));
            view.hideFlags = HideFlags.HideAndDontSave;
            view.layer = CardboardMarkerHand.HandLayer;
            view.GetComponent<MeshFilter>().sharedMesh = model.Mesh;
            var renderer = view.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = model.Surfaces;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.allowOcclusionWhenDynamic = false;
            AddGrip(new Vector3(.018f, -.047f, -.104f), new Vector3(.10f, -.17f, -.24f));
            AddGrip(new Vector3(-.014f, -.037f, .106f), new Vector3(-.105f, -.24f, -.10f));
            if (trace) return true;

            var effects = new GameObject("R82C4916", typeof(LineRenderer), typeof(AudioSource));
            effects.hideFlags = HideFlags.HideAndDontSave;
            effects.transform.SetParent(transform, false);
            trace = effects.GetComponent<LineRenderer>();
            trace.sharedMaterial = model.Trace;
            trace.startWidth = .027f;
            trace.endWidth = .009f;
            trace.shadowCastingMode = ShadowCastingMode.Off;
            trace.receiveShadows = false;
            trace.enabled = false;
            speaker = effects.GetComponent<AudioSource>();
            speaker.playOnAwake = false;
            speaker.volume = .45f;
            pulseSound = MakePulseSound();
            return true;
        }

        void AddGrip(Vector3 palm, Vector3 elbow)
        {
            Vector3 forearm = elbow - palm;
            var cuff = GripPart(palm + forearm * .58f, new Vector3(.051f, forearm.magnitude * .87f, .046f), model.Sleeve);
            cuff.localRotation = Quaternion.FromToRotation(Vector3.up, forearm);
            var hand = GripPart(palm, new Vector3(.038f, .041f, .048f), model.Paper);
            hand.localRotation = Quaternion.FromToRotation(Vector3.up, forearm);
        }

        Transform GripPart(Vector3 position, Vector3 scale, Material surface)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.hideFlags = HideFlags.HideAndDontSave;
            piece.name = view.name;
            piece.layer = view.layer;
            piece.transform.SetParent(view.transform, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            var collider = piece.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = piece.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = surface;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return piece.transform;
        }

        void Fire(Ray ray, float now)
        {
            nextShot = now + ShotInterval;
            Physics.SyncTransforms();
            bool contact = PickContact(ray, out var hit, out var rat);
            if (contact && ApplyContact(hit, rat, ray.direction)) hitUntil = now + .13f;
            Vector3 end = contact ? hit.point : ray.GetPoint(Reach);
            Vector3 start = view.transform.TransformPoint(model.Muzzle);
            if (Vector3.Dot(end - start, ray.direction) <= 0f) start = ray.origin;
            trace.SetPosition(0, start);
            trace.SetPosition(1, end);
            trace.enabled = true;
            traceUntil = now + TraceDuration;
            recoil = 1f;
            speaker.PlayOneShot(pulseSound);
        }

        bool PickContact(Ray ray, out RaycastHit nearest, out FactoryRatTarget target)
        {
            var buffer = hits;
            int count = Physics.RaycastNonAlloc(ray, buffer, Reach, ShotLayers, QueryTriggerInteraction.Collide);
            if (count == buffer.Length)
            {
                buffer = Physics.RaycastAll(ray, Reach, ShotLayers, QueryTriggerInteraction.Collide);
                count = buffer.Length;
            }
            nearest = default;
            target = null;
            float best = float.PositiveInfinity;
            var player = CardboardRunner.Player.transform;
            for (int i = 0; i < count; i++)
            {
                var hit = buffer[i];
                if (hit.distance >= best || hit.collider.transform.IsChildOf(player)) continue;
                var rat = hit.collider.GetComponentInParent<FactoryRatTarget>();
                if (rat && rat.food != FactoryRatFood.LiveRat) rat = null;
                if (hit.collider.isTrigger && !rat) continue;
                best = hit.distance;
                nearest = hit;
                target = rat;
            }
            return best < float.PositiveInfinity;
        }

        static bool ApplyContact(RaycastHit hit, FactoryRatTarget rat, Vector3 direction)
        {
            if (rat) { rat.Remove(); return true; }
            var worker = hit.collider.GetComponentInParent<FactoryWorkerImpact>();
            if (worker) { worker.ReceiveImpact(direction, 3f); return true; }
            var prop = hit.collider.GetComponentInParent<FactoryPhysicsProp>();
            if (!prop || prop.IsHeld || !prop.Body || prop.Body.isKinematic) return false;
            prop.Body.AddForceAtPosition(direction * 2.2f, hit.point, ForceMode.Impulse);
            return true;
        }

        static AudioClip MakePulseSound()
        {
            const int rate = 22050;
            var samples = new float[rate / 5];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float phase = 2f * Mathf.PI * (1050f * t - 2100f * t * t);
                samples[i] = Mathf.Min(t * 390f, 1f) * Mathf.Exp(-22f * t) *
                    (.55f * Mathf.Sin(phase) + .35f * Mathf.Sin(2.31f * phase));
            }
            var clip = AudioClip.Create("R82C4916", samples.Length, 1, rate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        void OnGUI()
        {
            if (!Drawn || !view || !view.activeSelf || Event.current.type != EventType.Repaint) return;
            var oldColor = GUI.color;
            GUI.color = Time.time < hitUntil ? Pulse : Color.white;
            float x = Screen.width * .5f, y = Screen.height * .5f;
            GUI.DrawTexture(new Rect(x - 8, y - 1, 5, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + 3, y - 1, 5, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1, y - 8, 2, 5), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1, y + 3, 2, 5), Texture2D.whiteTexture);
            GUI.color = oldColor;
            prompt ??= new GUIStyle(GUI.skin.label)
            {
                font = CardboardTypography.DefaultFont,
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(x - 130, y + 16, 260, 28), "LMB - FIRE  /  RMB - STOW", prompt);
        }

        internal void Clear()
        {
            entered = 0;
            pendingEquip = equipped = false;
            hitUntil = recoil = 0f;
            Hide();
        }

        void Hide()
        {
            if (view && view.activeSelf) view.SetActive(false);
            if (trace && trace.enabled) trace.enabled = false;
        }

        void OnDestroy()
        {
            if (view) Destroy(view);
            if (trace) Destroy(trace.gameObject);
            if (pulseSound) Destroy(pulseSound);
            model?.Release();
        }
    }
}
