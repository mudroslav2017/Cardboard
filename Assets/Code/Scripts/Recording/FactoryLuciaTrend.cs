using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Cardboard
{
    [DefaultExecutionOrder(-70), DisallowMultipleComponent]
    public sealed class FactoryLuciaTrend : MonoBehaviour, IFactoryTrendRecording
    {
        public const int FrameRate = 30;
        const float TimelineStretch = 1.6f;
        public const int FrameCount = (int)(198 * TimelineStretch + .5f);
        const float Duration = (float)FrameCount / FrameRate;
        const float Aspect = 9f / 16;
        const float ShakeStrength = .43f;
        const float PullBackFrame = 132;
        const string WorkerName = "Cardboard factory worker 10";
        static readonly int[] Cuts = { 40, 70, 100 };
        static readonly int[] CastOrder = { 1, 0, 1, 2 };
        static readonly string[] Captions = { "нас теперь трое", "он", "она", "люсия" };

        readonly Transform[] cast = new Transform[3];
        CardboardFactoryGame game;
        Transform stage;
        Camera lens;
        AudioSource audio;
        Volume focusVolume;
        VolumeProfile focusProfile;
        DepthOfField focusBlur;
        RectTransform captionPlane;
        Text caption;
        bool playing;
        float time;

        public bool Active { get; private set; }
        public Camera Lens => lens;
        public AudioClip Soundtrack => audio ? audio.clip : null;

        void Awake() => game = GetComponent<CardboardFactoryGame>();

        public bool Begin(bool preview)
        {
            if (Active) return true;
            if (!game || !game.station || !game.filmSession.Begin(preserveSceneLighting: true)) return false;

            Active = true;
            lens = Camera.main;
            try
            {
                var workstation = game.station.transform;
                stage = new GameObject("Он, я и Люсия • заводской цех").transform;
                stage.SetPositionAndRotation(workstation.TransformPoint(new Vector3(1.15f, 0, -5.92f)),
                    workstation.rotation * Quaternion.Euler(0, -9, 0));
                game.filmSession.HideOtherRecordingProps();
                Cast();
                CreateFocus();
                CreateCaption();
                var key = Node("Recording face fill").gameObject.AddComponent<Light>();
                key.type = LightType.Point;
                key.transform.localPosition = new Vector3(-.28f, 3.07f, 1.45f);
                key.range = 5;
                key.color = new Color(1, .94f, .81f);
                key.intensity = 2.0f;

                audio = stage.gameObject.AddComponent<AudioSource>();
                audio.clip = Resources.Load<AudioClip>("LuciaTrend/Camera sounds");
                if (!audio.clip) throw new InvalidOperationException("Lucia trend soundtrack is missing.");
                audio.playOnAwake = false;
                audio.ignoreListenerPause = true;
                audio.spatialBlend = 0;
                lens.aspect = Aspect;
                lens.GetUniversalAdditionalCameraData().renderPostProcessing = true;
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

        void Cast()
        {
            FactoryWorkerAppearance source = null;
            foreach (var worker in game.crowd.runners)
                if (worker && worker.name == WorkerName && worker.puppet)
                {
                    source = worker.puppet.GetComponentInChildren<FactoryWorkerAppearance>(true);
                    break;
                }
            if (!source) throw new InvalidOperationException("Could not find the model on " + WorkerName);

            cast[0] = Node("Хуанг • Worker 10");
            var huang = new GameObject("Worker 10 model", typeof(MeshFilter), typeof(MeshRenderer));
            huang.transform.SetParent(cast[0], false);
            huang.transform.localRotation = source.transform.localRotation;
            huang.transform.localScale = source.transform.localScale;
            huang.GetComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            // This material uses the same authored face as Worker 10's scene override.
            var drama = Resources.Load<FactoryPaperDramaSettings>("PaperDrama/Settings");
            huang.GetComponent<MeshRenderer>().sharedMaterial = drama && drama.boss
                ? drama.boss : source.GetComponent<Renderer>().sharedMaterial;

            cast[1] = Node("Настя");
            cast[2] = Node("Лера • Люсия");
            ImportedCardboardCharacters.Create(cast[1], 2).GetComponent<FactoryWorkerAppearance>().SetState(FactoryFaceState.Normal);
            var luciaMaterial = Resources.Load<Material>("LuciaTrend/Lera Lucia");
            if (!luciaMaterial) throw new InvalidOperationException("Could not load Lera's Lucia material.");
            ImportedCardboardCharacters.Create(cast[2], 0).GetComponent<Renderer>().sharedMaterial = luciaMaterial;

            cast[0].localPosition = new Vector3(-1.085f, 0, 3.10f);
            cast[1].localPosition = new Vector3(0, 0, 2.76f);
            cast[2].localPosition = new Vector3(1.085f, 0, 3.06f);
        }

        void CreateFocus()
        {
            focusProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            focusBlur = focusProfile.Add<DepthOfField>();
            focusBlur.mode.Override(DepthOfFieldMode.Bokeh);
            focusBlur.aperture.Override(3.2f);
            focusBlur.focalLength.Override(78);
            focusBlur.focusDistance.Override(4);
            focusVolume = Node("Brief autofocus slip").gameObject.AddComponent<Volume>();
            focusVolume.isGlobal = true;
            focusVolume.priority = 80;
            focusVolume.weight = 0;
            focusVolume.sharedProfile = focusProfile;
        }

        void CreateCaption()
        {
            var layer = new GameObject("Монтажные титры", typeof(RectTransform), typeof(Canvas));
            layer.transform.SetParent(stage, false);
            var canvas = layer.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = lens;
            canvas.sortingOrder = 25;
            captionPlane = layer.GetComponent<RectTransform>();
            captionPlane.sizeDelta = new Vector2(1080, 1920);

            var title = new GameObject("Подпись", typeof(RectTransform), typeof(Text), typeof(Outline));
            title.transform.SetParent(captionPlane, false);
            caption = title.GetComponent<Text>();
            caption.font = CardboardTypography.DefaultFont;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.color = Color.white;
            caption.raycastTarget = false;
            caption.rectTransform.sizeDelta = new Vector2(1030, 224);
            var outline = title.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(6, -6);
        }

        void PlaceCaption(int shot, float frame, float sinceCut)
        {
            bool intro = shot == 0;
            bool visible = (intro ? frame >= 6 : sinceCut >= 5) && frame < PullBackFrame;
            caption.gameObject.SetActive(visible);
            if (!visible) return;

            const float planeDistance = .28f;
            captionPlane.SetPositionAndRotation(lens.transform.position + lens.transform.forward * planeDistance,
                lens.transform.rotation);
            float worldPerPixel = 2 * planeDistance * Mathf.Tan(lens.fieldOfView * Mathf.Deg2Rad / 2) / 1920;
            captionPlane.localScale = Vector3.one * worldPerPixel;
            caption.text = Captions[shot];
            caption.fontSize = intro ? 94 : 156;
            caption.rectTransform.anchoredPosition = new Vector2(0, -.292f * 1920);
            float appear = intro ? frame - 6 : sinceCut - 5;
            float pop = Mathf.Lerp(.86f, 1, Ease(appear, 0, 4)) + .07f * Pulse(appear, 1, 3, 8);
            caption.rectTransform.localScale = Vector3.one * pop;
        }

        void Pose(float seconds)
        {
            time = seconds;
            float actionTime = seconds / TimelineStretch;
            float frame = actionTime * FrameRate;
            for (int i = 0; i < cast.Length; i++)
            {
                var actor = cast[i];
                float sway = Mathf.Sin(3.1f * actionTime + 1.9f * i);
                actor.localRotation = Quaternion.LookRotation(new Vector3(-actor.localPosition.x * .46f, 0, -3))
                    * Quaternion.Euler(sway * .28f, sway * .65f, sway * .8f);
            }

            int shot = 0;
            while (shot < Cuts.Length && frame >= Cuts[shot]) shot++;
            float sinceCut = shot == 0 ? frame : frame - Cuts[shot - 1];
            var subject = cast[CastOrder[shot]];
            Vector3 target = subject.localPosition + Vector3.up * (shot == 0 ? 1.64f : 1.77f);
            float nearFov = shot == 0 ? 49 : 38.7f;
            float push = 0;
            float whip = 0;
            if (shot > 0)
            {
                var previous = cast[CastOrder[shot - 1]];
                Vector3 from = previous.localPosition + Vector3.up * (shot == 1 ? 1.64f : 1.77f);
                push = Ease(sinceCut, 0, 6);
                whip = Pulse(sinceCut, 0, 2.7f, 7);
                float overshoot = .115f * Pulse(sinceCut, 4.8f, 7.1f, 13);
                target = Vector3.LerpUnclamped(from, target, push + overshoot);
                nearFov = Mathf.Lerp(shot == 1 ? 49 : 38.7f, nearFov, push)
                    + 4.5f * whip - 4.4f * Pulse(sinceCut, 4.2f, 7.1f, 15);
            }

            float wide = Ease(frame, PullBackFrame, 166);
            var eye = new Vector3(ShakeStrength * .033f * Mathf.Sin(actionTime * 14.2f),
                1.75f + ShakeStrength * .024f * Mathf.Sin(actionTime * 18), shot == 0 ? .04f : .23f * push);
            eye = Vector3.Lerp(eye, new Vector3(0, 1.69f, -3.16f), wide);
            target = Vector3.Lerp(target, new Vector3(0, 1.18f, 3.0f), wide);
            float settle = Mathf.Lerp(1, .30f, wide);
            float roll = settle * (ShakeStrength * 1.24f * Mathf.Sin(actionTime * 14.6f) + whip * (shot == 1 ? -1.8f : 1.8f));
            var aim = Quaternion.LookRotation(stage.TransformPoint(target) - stage.TransformPoint(eye), stage.up);
            lens.transform.SetPositionAndRotation(stage.TransformPoint(eye),
                aim * Quaternion.Euler(settle * ShakeStrength * .53f * Mathf.Sin(actionTime * 23f),
                    settle * ShakeStrength * .58f * Mathf.Sin(actionTime * 17f), roll));
            lens.fieldOfView = Mathf.Lerp(nearFov, 52, wide);
            float slip = Pulse(frame, 143, 158, 178);
            focusVolume.weight = Mathf.Clamp01(slip * 7.5f);
            focusBlur.focusDistance.value = Mathf.Lerp((target - eye).magnitude, .76f, slip);
            PlaceCaption(shot, frame, sinceCut);
        }

        static float Ease(float seconds, float from, float to)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, seconds));

        static float Pulse(float seconds, float start, float peak, float end)
            => Ease(seconds, start, peak) * (1 - Ease(seconds, peak, end));

        Transform Node(string name)
        {
            var node = new GameObject(name).transform;
            node.SetParent(stage, false);
            return node;
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (keys != null && keys.f10Key.wasPressedThisFrame)
            {
                if (Active) Stop();
                else Begin(true);
            }
            if (!Active) return;
            if (keys != null && keys.f7Key.wasPressedThisFrame) { Seek(0); SetPlaying(true); return; }
            if (keys != null && keys.spaceKey.wasPressedThisFrame) SetPlaying(!playing);
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
            var frame = Picture;
            var color = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, frame.x, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.xMax, 0, Screen.width - frame.xMax, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.x, 0, frame.width, frame.y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.x, frame.yMax, frame.width, Screen.height - frame.yMax), Texture2D.whiteTexture);
            GUI.color = color;
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
            if (!value) { if (audio) audio.Stop(); return; }
            if (time >= Duration) Pose(0);
            audio.timeSamples = Mathf.Min(audio.clip.samples - 1, Mathf.RoundToInt(time * audio.clip.frequency));
            audio.Play();
        }

        public void Stop(bool restoreMenu = true)
        {
            if (!Active) return;
            SetPlaying(false);
            Active = false;
            if (stage) { stage.gameObject.SetActive(false); Destroy(stage.gameObject); }
            if (focusProfile) Destroy(focusProfile);
            if (game && game.filmSession) game.filmSession.Stop(restoreMenu);
        }

        void OnDisable() => Stop(false);
    }
}
