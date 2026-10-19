using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    public static class FactoryWebGLBuilder
    {
        const string Pending="Cardboard.WebGLBuild.Pending",Destination="Cardboard.WebGLBuild.Destination";
        const string Report="Library/FactoryWebGLBuildReport.txt";
        static FactoryWebGLBuilder(){EditorApplication.update+=Tick;}
        // Unity -batchmode -buildTarget WebGL -executeMethod Cardboard.Editor.FactoryWebGLBuilder.BuildBatch
        public static void BuildBatch()
        {
            Build(Path.GetFullPath("Builds/WebGL"));
            if(!File.ReadAllText(Report).StartsWith("RESULT: Succeeded"))
                throw new InvalidOperationException("WebGL build failed. See "+Report);
        }
        public static void Request(string output)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)throw new InvalidOperationException("Stop Play Mode before building WebGL.");
            output=Path.GetFullPath(output);
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL,BuildTarget.WebGL))throw new InvalidOperationException("Unity WebGL support is not installed.");
            AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
            SessionState.SetString(Destination,output);SessionState.SetBool(Pending,true);
            File.WriteAllText(Report,"SWITCHING TO WEBGL\nDestination: "+output+"\nRequested: "+DateTime.Now.ToString("O")+"\n");
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.WebGL && !EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.WebGL,BuildTarget.WebGL))
            {SessionState.SetBool(Pending,false);File.AppendAllText(Report,"FAILED: could not switch to WebGL.\n");}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false) || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer || EditorUserBuildSettings.activeBuildTarget!=BuildTarget.WebGL)return;
            SessionState.SetBool(Pending,false);Build(SessionState.GetString(Destination,""));
        }
        static void Build(string output)
        {
            bool fallback=PlayerSettings.WebGL.decompressionFallback;var compression=PlayerSettings.WebGL.compressionFormat;int memory=PlayerSettings.WebGL.initialMemorySize;
            try
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Report,"BUILDING WEBGL\nDestination: "+output+"\nStarted: "+DateTime.Now.ToString("O")+"\n");
                // The exported loader can decompress data on ordinary static HTTP hosts.
                PlayerSettings.WebGL.decompressionFallback=true;PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;PlayerSettings.WebGL.initialMemorySize=512;
                var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
                if(scenes.Length==0)throw new InvalidOperationException("No enabled scenes in Build Settings.");
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=output,target=BuildTarget.WebGL,options=BuildOptions.None});
                if(result.summary.result==BuildResult.Succeeded){var html=Path.Combine(output,"index.html");var text=File.ReadAllText(html);text=text.Replace("var config = {","var config = { devicePixelRatio: Math.min(1, 1600 / Math.max(window.innerWidth, window.innerHeight)),");File.WriteAllText(html,text);}
                var summary=result.summary;var report=new StringBuilder();
                report.AppendLine("RESULT: "+summary.result);report.AppendLine("Destination: "+summary.outputPath);
                report.AppendLine("Scenes: "+string.Join(", ",scenes));report.AppendLine("Total bytes: "+summary.totalSize);
                report.AppendLine("Duration: "+summary.totalTime);report.AppendLine("Errors: "+summary.totalErrors+"; warnings: "+summary.totalWarnings);
                foreach(var step in result.steps)foreach(var message in step.messages)if(message.type==LogType.Error || message.type==LogType.Exception)report.AppendLine(message.content);
                File.WriteAllText(Report,report.ToString());
            }
            catch(Exception error){File.AppendAllText(Report,"FAILED: "+error+"\n");Debug.LogException(error);}
            finally{PlayerSettings.WebGL.decompressionFallback=fallback;PlayerSettings.WebGL.compressionFormat=compression;PlayerSettings.WebGL.initialMemorySize=memory;AssetDatabase.SaveAssets();}
        }
    }
}
