using System;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    internal static class FactoryTrendExport
    {
        internal enum Kind { NastyaLera, Forehead, Lucia, Bees }

        sealed class Format
        {
            public readonly string BaseName, Report;
            public readonly int Width, Height, FrameRate, FrameCount, CutFrame;
            public readonly uint TargetBitRate;
            public readonly Func<CardboardFactoryGame, IFactoryTrendRecording> Recording;

            public Format(string baseName, string report, int width, int height, int frameRate, int frameCount,
                Func<CardboardFactoryGame, IFactoryTrendRecording> recording, uint targetBitRate = 0, int cutFrame = -1)
            {
                BaseName = baseName;
                Report = report;
                Width = width;
                Height = height;
                FrameRate = frameRate;
                FrameCount = frameCount;
                Recording = recording;
                TargetBitRate = targetBitRate;
                CutFrame = cutFrame;
            }
        }

        const string Pending = "Cardboard.TrendExport.Action";
        const string Selected = "Cardboard.TrendExport.Selection";
        const string OwnsPlayMode = "Cardboard.TrendExport.OwnsPlayMode";
        static readonly Format[] Formats =
        {
            new Format("Nastya and Lera", "Library/NastyaLeraTrendRender.txt", 1080, 1920,
                FactoryNastyaLeraTrend.FrameRate, FactoryNastyaLeraTrend.FrameCount, game => game.nastyaLeraTrend),
            new Format("Nastya and Lera - Factory Sweep 9x16", "Library/ForeheadTrendRender.txt", 1080, 1920,
                FactoryForeheadTrend.FrameRate, FactoryForeheadTrend.FrameCount, game => game.foreheadTrend, 27000000),
            new Format("He Me and Lucia - 9x16 Lucy GTA 6", "Library/LuciaTrendRender.txt", 1080, 1920,
                FactoryLuciaTrend.FrameRate, FactoryLuciaTrend.FrameCount, game => game.luciaTrend, 27000000),
            new Format("Nastya and Bees - Final Look 9x16", "Library/BeesTrendRender.txt", 1080, 1920,
                FactoryBeesTrend.FrameRate, FactoryBeesTrend.FrameCount, game => game.beesTrend, 20000000, FactoryBeesTrend.CloseFrame)
        };
        static Format Current => Formats[SessionState.GetInt(Selected, 0)];

        static IFactoryTrendRecording movie;
        static RenderTexture target;
        static Texture2D frame;
        static MediaEncoder encoder;
        static string destination, partial;
        const int WarmupFrameCount = 4;
        static int nextFrame, warmupFrames;
        static bool cutPrepared;

        static FactoryTrendExport()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingPlayMode) return;
                if (encoder != null || SessionState.GetString(Pending, "") != "")
                {
                    SessionState.EraseString(Pending);
                    SessionState.EraseBool(OwnsPlayMode);
                    Finish("Render cancelled.");
                }
            };
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (encoder != null) Finish("Render interrupted by script reload.");
            };
        }

        [MenuItem("Cardboard/Preview forehead trend")]
        public static void PreviewForehead() => Request(Kind.Forehead, false);

        [MenuItem("Cardboard/Export forehead trend (MP4)")]
        public static void ExportForehead() => Request(Kind.Forehead, true);

        [MenuItem("Cardboard/Preview He Me and Lucia")]
        public static void PreviewLucia() => Request(Kind.Lucia, false);

        [MenuItem("Cardboard/Export He Me and Lucia (MP4)")]
        public static void ExportLucia() => Request(Kind.Lucia, true);

        [MenuItem("Cardboard/Preview Nastya and Bees")]
        public static void PreviewBees() => Request(Kind.Bees, false);

        [MenuItem("Cardboard/Export Nastya and Bees (MP4)")]
        public static void ExportBees() => Request(Kind.Bees, true);

        internal static void Request(Kind kind, bool export)
        {
            if (encoder != null || SessionState.GetString(Pending, "") != "") return;
            if (SceneManager.GetActiveScene().path != CardboardFactorySceneBuilder.ScenePath)
            {
                Debug.LogError("Open FactoryShift before starting this recording.");
                return;
            }
            SessionState.SetInt(Selected, (int)kind);
            if (export) File.WriteAllText(Current.Report, "Waiting for Play Mode");
            SessionState.SetString(Pending, export ? "export" : "preview");
            SessionState.SetBool(OwnsPlayMode, export && !EditorApplication.isPlaying);
            if (!EditorApplication.isPlaying)
            {
                FactoryMainMenu.SkipNextMenu = false;
                EditorApplication.isPlaying = true;
            }
        }

        static void Update()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (encoder != null)
            {
                try
                {
                    if (movie == null || !movie.Active) throw new InvalidOperationException("Recording was closed.");
                    if (!cutPrepared && nextFrame == Current.CutFrame)
                    {
                        warmupFrames = WarmupFrameCount;
                        cutPrepared = true;
                    }
                    // Prepare the visible renderers before encoding the first frame of a shot.
                    if (warmupFrames > 0)
                    {
                        Capture(nextFrame, encode: false);
                        warmupFrames--;
                        return;
                    }
                    if (nextFrame < Current.FrameCount) Capture(nextFrame++);
                    if (nextFrame == Current.FrameCount) Finish();
                }
                catch (Exception error) { Finish(error.ToString()); }
                return;
            }

            string action = SessionState.GetString(Pending, "");
            if (action == "") return;
            var game = CardboardFactoryGame.Instance;
            if (!game || !CardboardRunner.Player || !Camera.main) return;
            SessionState.EraseString(Pending);
            movie = Current.Recording(game);
            try
            {
                if (!movie.Begin(action == "preview"))
                    throw new InvalidOperationException("Could not start the recording in the current game state.");
                if (action == "export") StartExport();
            }
            catch (Exception error) { Finish(error.ToString()); }
        }

        static void StartExport()
        {
            const string folder = "Recordings/TikTok";
            Directory.CreateDirectory(folder);
            destination = Path.GetFullPath(Path.Combine(folder, Current.BaseName + ".mp4"));
            if (File.Exists(destination))
                destination = Path.GetFullPath(Path.Combine(folder, Current.BaseName + " " + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".mp4"));
            partial = Path.GetFullPath(Path.Combine(folder, ".Trend-" + Guid.NewGuid().ToString("N") + ".mp4"));

            var clip = movie.Soundtrack;
            var video = new VideoTrackAttributes
            {
                frameRate = new MediaRational(Current.FrameRate),
                width = (uint)Current.Width,
                height = (uint)Current.Height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High
            };
            AudioTrackAttributes audio = default;
            float[] pcm = null;
            if (clip)
            {
                audio = new AudioTrackAttributes
                {
                    channelCount = (ushort)clip.channels,
                    sampleRate = new MediaRational(clip.frequency)
                };
                pcm = new float[clip.samples * clip.channels];
                if (!clip.GetData(pcm, 0)) throw new InvalidOperationException("Could not read the soundtrack PCM data.");
            }

            movie.Seek(0);
            target = new RenderTexture(Current.Width, Current.Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create();
            frame = new Texture2D(Current.Width, Current.Height, TextureFormat.RGBA32, false);
            if (Current.TargetBitRate == 0)
                encoder = !clip ? new MediaEncoder(partial, video) : new MediaEncoder(partial, video, audio);
            else
            {
                var movingShot = new VideoTrackEncoderAttributes(new H264EncoderAttributes
                {
                    profile = VideoEncodingProfile.H264High,
                    gopSize = 30,
                    numConsecutiveBFrames = 0
                })
                {
                    frameRate = video.frameRate,
                    width = video.width,
                    height = video.height,
                    targetBitRate = Current.TargetBitRate,
                    bitRateMode = VideoBitrateMode.High
                };
                encoder = !clip
                    ? new MediaEncoder(partial, movingShot)
                    : new MediaEncoder(partial, movingShot, audio);
            }
            if (pcm != null)
            {
                using var samples = new NativeArray<float>(pcm, Allocator.Temp);
                encoder.AddSamples(samples);
            }
            nextFrame = 0;
            warmupFrames = WarmupFrameCount;
            cutPrepared = false;
            File.WriteAllText(Current.Report, "Rendering " + Current.FrameCount + " frames");
        }

        static void Capture(int number, bool encode = true)
        {
            movie.Seek((float)number / Current.FrameRate);
            var camera = movie.Lens;
            var oldTarget = camera.targetTexture;
            var oldRect = camera.rect;
            var oldActive = RenderTexture.active;
            var oldAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                camera.targetTexture = target;
                camera.rect = new Rect(0, 0, 1, 1);
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else
                    camera.Render();
                if (!encode) return;
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, Current.Width, Current.Height), 0, 0, false);
                frame.Apply(false);
                encoder.AddFrame(frame);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = oldAsyncCompilation;
                camera.targetTexture = oldTarget;
                camera.rect = oldRect;
                RenderTexture.active = oldActive;
            }
        }

        static void Finish(string error = null)
        {
            try
            {
                if (encoder != null) encoder.Dispose();
                if (error == null)
                {
                    File.Move(partial, destination);
                    File.WriteAllText(Current.Report, "DONE " + destination);
                    Debug.Log("Trend exported to " + destination);
                }
            }
            catch (Exception exception) { error = exception.ToString(); }
            finally
            {
                encoder = null;
                if (partial != null && File.Exists(partial)) File.Delete(partial);
                partial = null;
                destination = null;
                if (frame) Object.DestroyImmediate(frame);
                if (target) Object.DestroyImmediate(target);
                frame = null;
                target = null;
                if (movie != null && movie.Active) movie.Stop();
                movie = null;
                if (error != null)
                {
                    File.WriteAllText(Current.Report, "ERROR " + error);
                    Debug.LogError(error);
                }
                if (SessionState.GetBool(OwnsPlayMode, false))
                {
                    SessionState.EraseBool(OwnsPlayMode);
                    EditorApplication.isPlaying = false;
                }
            }
        }
    }
}
