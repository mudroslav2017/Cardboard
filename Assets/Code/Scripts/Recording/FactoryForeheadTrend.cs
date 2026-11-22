using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Cardboard
{
    [DefaultExecutionOrder(-70), DisallowMultipleComponent]
    public sealed class FactoryForeheadTrend : MonoBehaviour, IFactoryTrendRecording
    {
        public const int FrameRate = 30;
        public const int FrameCount = 288;
        public const float Duration = (float)FrameCount / FrameRate;

        const float Aspect = 9f / 16;
        const float TurnStart = 6.43f;
        const float TurnEnd = 7.25f;
        const float SweepStart = 7.43f;
        const float SweepEnd = 9.22f;
        const int LeraVariant = 0;
        const int NastyaVariant = 2;
        const string AudioResource = "ForeheadTrend/Forehead soundtrack";
        static readonly int[] ContactFrames = { 18, 40, 61, 82, 104, 126, 149 };

        CardboardFactoryGame game;
        Transform stage, lera, nastya, peekingWorker;
        Vector3 peekPosition;
        Quaternion peekRotation;
        Vector3 leraForehead, nastyaForehead, nastyaEye;
        FactoryWorkerAppearance appearance;
        Light crowdFill;
        Camera lens;
        AudioSource soundtrack;
        bool showControls, playing;
        float time;

        public bool Active { get; private set; }
        public Camera Lens => lens;
        public AudioClip Soundtrack => soundtrack ? soundtrack.clip : null;

        void Awake() => game = GetComponent<CardboardFactoryGame>();

        public bool Begin(bool preview)
        {
            if (Active) return true;
            if (!game || !game.station || !game.filmSession.Begin(preserveSceneLighting: true)) return false;

            Active = true;
            lens = Camera.main;
            showControls = preview;
            try
            {
                stage = new GameObject("Forehead bumps • factory aisle").transform;
                var workstation = game.station.transform;
                stage.SetPositionAndRotation(workstation.TransformPoint(new Vector3(1.52f, 0, -5.65f)),
                    workstation.rotation * Quaternion.Euler(0, -13, 0));
                CreateActors();
                CreateWitnesses();
                game.filmSession.HideOtherRecordingProps();
                var faceLight = Node("Recording face fill").gameObject.AddComponent<Light>();
                faceLight.type = LightType.Point;
                faceLight.transform.localPosition = new Vector3(-.72f, 2.56f, 2.16f);
                faceLight.range = 3.3f;
                faceLight.color = new Color(1, .94f, .81f);
                faceLight.intensity = 1.45f;

                soundtrack = stage.gameObject.AddComponent<AudioSource>();
                soundtrack.clip = Resources.Load<AudioClip>(AudioResource);
                if (!soundtrack.clip) throw new InvalidOperationException("Forehead trend soundtrack is missing.");
                soundtrack.playOnAwake = false;
                soundtrack.ignoreListenerPause = true;
                soundtrack.spatialBlend = 0;

                lens.aspect = Aspect;
                Pose(0);
                if (preview) SetPlaying(true);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Stop(false);
                return false;
            }
        }

        void CreateActors()
        {
            nastya = Node("Настя • камера от первого лица");
            var nastyaModel = ImportedCardboardCharacters.Create(nastya, NastyaVariant);
            nastyaModel.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            nastyaForehead = Forehead(nastyaModel);
            nastyaEye = nastyaForehead + new Vector3(0, -.20f, -.39f);

            lera = Node("Лера");
            lera.localPosition = new Vector3(.075f, 0, 3.18f);
            var leraModel = ImportedCardboardCharacters.Create(lera, LeraVariant);
            leraForehead = Forehead(leraModel);
            appearance = leraModel.GetComponent<FactoryWorkerAppearance>();
        }

        void CreateWitnesses()
        {
            // Use the shift's cast, reserving one Lera and one Nastya for the foreground scene.
            var witnesses = new List<CardboardRunner>();
            bool reservedNastya = false;
            foreach (var worker in game.crowd.runners)
            {
                if (!worker || worker.isPlayer) continue;
                if (!reservedNastya && worker.personality % 4 == NastyaVariant)
                {
                    reservedNastya = true;
                    continue;
                }
                witnesses.Add(worker);
            }

            int frontCount = Mathf.Min(witnesses.Count, Mathf.Max(1, witnesses.Count / 2));
            int backCount = witnesses.Count - frontCount;
            float gap = Mathf.Min(.145f, 1.45f / Mathf.Max(1, witnesses.Count - 1));
            var cameraMark = new Vector3(-.03f, 0, 1.18f);
            peekingWorker = null;
            for (int i = 0; i < witnesses.Count; i++)
            {
                bool front = i < frontCount;
                int rowIndex = front ? i : i - frontCount;
                int slot = front ? 2 * rowIndex + 1 : 2 * rowIndex;
                float bearing = witnesses.Count == 1 ? 0 : (slot - (witnesses.Count - 1) * .5f) * gap;
                bool triesToSee = !front && rowIndex == (backCount - 1) / 2;
                if (triesToSee) bearing = -.023f;
                float z = (front ? -3.95f : -6.26f) - .26f * Mathf.Abs(bearing);
                float x = cameraMark.x + bearing * (cameraMark.z - z);
                var position = new Vector3(x, 0, z);
                var facing = Quaternion.LookRotation(cameraMark - position);
                var mark = Node("Рабочий • " + witnesses[i].personality);
                mark.SetLocalPositionAndRotation(position, facing);
                var model = ImportedCardboardCharacters.Create(mark, witnesses[i].personality);
                model.GetComponent<FactoryWorkerAppearance>().SetState(FactoryFaceState.Angry);
                if (triesToSee)
                {
                    peekingWorker = mark;
                    peekPosition = position;
                    peekRotation = facing;
                }
            }

            crowdFill = Node("Recording crowd fill").gameObject.AddComponent<Light>();
            crowdFill.type = LightType.Point;
            crowdFill.transform.localPosition = new Vector3(-.35f, 3.32f, -1.23f);
            crowdFill.range = 8.7f;
            crowdFill.color = new Color(1, .93f, .79f);
        }

        static Vector3 Forehead(Transform model)
        {
            var mesh = model.GetComponent<MeshFilter>().sharedMesh;
            var bounds = mesh.bounds;
            float height = bounds.min.y + bounds.size.y * .93f;
            float front = float.NegativeInfinity;
            foreach (var vertex in mesh.vertices)
            {
                Vector3 local = model.localRotation * vertex;
                if (local.y >= bounds.min.y + bounds.size.y * .82f)
                    front = Mathf.Max(front, local.z);
            }
            return new Vector3(0, height, float.IsNegativeInfinity(front) ? bounds.extents.z : front);
        }

        static float Ease(float seconds, float from, float to)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, seconds));

        static float Pulse(float seconds, float start, float peak, float end)
            => Ease(seconds, start, peak) * (1 - Ease(seconds, peak, end));

        void Pose(float seconds)
        {
            time = seconds;
            PosePeekingWorker(seconds);
            float suspicionAt = (float)ContactFrames[1] / FrameRate + .10f;
            float angerAt = (float)ContactFrames[4] / FrameRate + .10f;
            appearance.SetState(seconds >= angerAt ? FactoryFaceState.Angry
                : seconds >= suspicionAt ? FactoryFaceState.Suspicious : FactoryFaceState.Normal);
            float suspicion = Ease(seconds, suspicionAt, suspicionAt + .26f);
            float anger = Ease(seconds, angerAt, angerAt + .27f);

            float approach = 0, windup = 0, reaction = 0, pullback = 0, jolt = 0, side = 0;
            for (int i = 0; i < ContactFrames.Length; i++)
            {
                float local = seconds - (float)ContactFrames[i] / FrameRate;
                if (local < -.32f || local > .38f) continue;
                float force = .79f + .046f * i;
                side = i % 2 == 0 ? 1 : -1;
                approach = Pulse(local, -.205f - .006f * i, 0, .235f);
                windup = Pulse(local, -.32f, -.222f, -.11f);
                pullback = Pulse(local, .034f, .16f, .31f);
                reaction = force * (Pulse(local, .013f, .084f, .26f) - .15f * Pulse(local, .213f, .272f, .37f));
                if (local >= 0) jolt = force * Mathf.Sin(local * 83) * Mathf.Exp(-local * 25);
                break;
            }

            float idle = Mathf.Sin(seconds * 3.3f);
            float glare = Ease(seconds, 5.28f, 5.78f);
            lera.localRotation = Quaternion.AngleAxis(7.5f * reaction - 4 * anger - 2 * glare, Vector3.right)
                * Quaternion.AngleAxis(180 - 7 * suspicion * (1 - anger) + 5 * reaction * side, Vector3.up)
                * Quaternion.AngleAxis(5 * suspicion * (1 - anger) + 4 * reaction * side + idle * .6f, Vector3.forward);

            // Nastya moves to Lera's forehead, but Lera's floor mark stays fixed.
            Vector3 contact = stage.InverseTransformPoint(lera.TransformPoint(leraForehead + new Vector3(.095f * side, 0, 0)));
            float turn = Ease(seconds, TurnStart, TurnEnd);
            var nastyaRotation = Quaternion.Euler(9 * approach - 5 * windup, 180 * turn + 2 * approach * side, -5 * approach * side);
            Vector3 touching = contact - nastyaRotation * nastyaForehead - Vector3.forward * .006f;
            Vector3 resting = new Vector3(-.035f + .025f * idle, -.015f + .015f * idle, 1.03f - .08f * windup - .125f * pullback - .13f * anger);
            nastya.SetLocalPositionAndRotation(Vector3.Lerp(resting, touching, approach), nastyaRotation);

            Vector3 joltOffset = stage.TransformVector(new Vector3(.013f * jolt * side, -.025f * jolt, -.013f * Mathf.Abs(jolt)));
            Vector3 eye = nastya.TransformPoint(nastyaEye - Vector3.forward * (.16f * approach)) + joltOffset;
            Vector3 look = lera.TransformPoint(leraForehead + new Vector3(.02f * approach * side, -.29f - .035f * approach, 0));
            var pan = Quaternion.AngleAxis(180 * turn, stage.up) * Quaternion.LookRotation(look - eye, stage.up);
            float sweep = Ease(seconds, SweepStart, SweepEnd);
            // After the half-turn, positive stage X is on Nastya’s left.
            var crowdAim = stage.TransformPoint(new Vector3(Mathf.Lerp(2.68f, -2.68f, sweep), 1.42f, -4.44f));
            var crowdLook = Quaternion.LookRotation(crowdAim - eye, stage.up);
            var view = Quaternion.Slerp(pan, crowdLook, Ease(turn, .78f, 1));
            float turnRoll = 3.1f * Mathf.Sin(turn * Mathf.PI);
            lens.transform.SetPositionAndRotation(eye,
                view * Quaternion.Euler(1.2f * jolt, 0, turnRoll + 2.4f * jolt * side - 2 * approach * side));
            lens.fieldOfView = 49 + 33 * approach + 10 * Ease(turn, .12f, 1);
            crowdFill.intensity = 4.45f * Ease(turn, .28f, .95f);
        }

        void PosePeekingWorker(float seconds)
        {
            if (!peekingWorker) return;
            float flight = Mathf.InverseLerp(8.04f, 8.70f, seconds);
            float height = 4.12f * flight * (1 - flight);
            float ready = Pulse(seconds, 7.91f, 8.00f, 8.08f);
            float recover = Pulse(seconds, 8.68f, 8.76f, 8.89f);
            var rock = Quaternion.Euler(7 * ready + 9 * recover, 0, -4 * height);
            peekingWorker.SetLocalPositionAndRotation(peekPosition + Vector3.up * height, peekRotation * rock);
        }

        Transform Node(string label)
        {
            var node = new GameObject(label);
            node.transform.SetParent(stage, false);
            return node.transform;
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (keys != null && keys.f9Key.wasPressedThisFrame)
            {
                if (Active) Stop();
                else Begin(true);
            }
            if (!Active) return;
            if (keys != null)
            {
                if (keys.hKey.wasPressedThisFrame) showControls = !showControls;
                if (keys.f7Key.wasPressedThisFrame) { Seek(0); SetPlaying(true); return; }
                if (keys.spaceKey.wasPressedThisFrame) SetPlaying(!playing);
            }
            if (!playing) return;
            Pose(Mathf.Min(time + Time.unscaledDeltaTime, Duration));
            if (time >= Duration) SetPlaying(false);
        }

        static Rect Picture
        {
            get
            {
                float width = Mathf.Min(Screen.width, Screen.height * Aspect);
                float height = width / Aspect;
                return new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            }
        }

        void LateUpdate()
        {
            if (!Active || lens.targetTexture) return;
            Rect frame = Picture;
            lens.rect = new Rect(frame.x / Screen.width, frame.y / Screen.height, frame.width / Screen.width, frame.height / Screen.height);
            lens.aspect = Aspect;
        }

        void OnGUI()
        {
            if (!Active) return;
            Rect picture = Picture;
            Color saved = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, picture.x, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(picture.xMax, 0, Screen.width - picture.xMax, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(picture.x, 0, picture.width, picture.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(picture.x, picture.yMax, picture.width, Screen.height - picture.yMax), Texture2D.whiteTexture);
            GUI.color = saved;
            if (!showControls) return;

            float width = Mathf.Min(500, picture.width - 20);
            float left = (Screen.width - width) * .5f;
            float top = picture.yMax - 48;
            GUI.Box(new Rect(left, top, width, 36), GUIContent.none);
            if (GUI.Button(new Rect(left + 5, top + 5, 64, 25), playing ? "Пауза" : "Играть")) SetPlaying(!playing);
            float next = GUI.HorizontalSlider(new Rect(left + 77, top + 11, Mathf.Max(20, width - 160), 18), time, 0, Duration);
            if (!Mathf.Approximately(next, time)) Seek(next);
            if (GUI.Button(new Rect(left + width - 67, top + 5, 62, 25), "F9  Выход")) Stop();
        }

        public void Seek(float seconds)
        {
            if (!Active) return;
            SetPlaying(false);
            Pose(Mathf.Clamp(seconds, 0, Duration));
        }

        void SetPlaying(bool value)
        {
            if (value == playing) return;
            playing = value;
            if (!value) { if (soundtrack) soundtrack.Stop(); return; }
            if (time >= Duration) Pose(0);
            soundtrack.timeSamples = Mathf.Min(soundtrack.clip.samples - 1, Mathf.RoundToInt(time * soundtrack.clip.frequency));
            soundtrack.Play();
        }

        public void Stop(bool restoreMenu = true)
        {
            if (!Active) return;
            SetPlaying(false);
            Active = false;
            if (stage) { stage.gameObject.SetActive(false); Destroy(stage.gameObject); }
            if (game && game.filmSession) game.filmSession.Stop(restoreMenu);
        }

        void OnDisable() => Stop(false);
    }
}
