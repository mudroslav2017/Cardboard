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
    public static class FactoryWindowsBuilder
    {
        const string Pending="Cardboard.WindowsBuild.Pending", Destination="Cardboard.WindowsBuild.Destination";
        const string Report="Library/FactoryWindowsBuildReport.txt";
        static FactoryWindowsBuilder(){EditorApplication.update+=Tick;}
        public static void Request(string output)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)throw new InvalidOperationException("Stop Play Mode before building Windows.");
            output=Path.GetFullPath(output);
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))throw new InvalidOperationException("Windows x64 build support is not installed.");
            AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
            SessionState.SetString(Destination,output);SessionState.SetBool(Pending,true);
            File.WriteAllText(Report,"SWITCHING TO WINDOWS X64\nDestination: "+output+"\nRequested: "+DateTime.Now.ToString("O")+"\n");
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64 && !EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))
            {SessionState.SetBool(Pending,false);File.AppendAllText(Report,"FAILED: could not switch to Windows x64.\n");}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false) || EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer || EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64)return;
            SessionState.SetBool(Pending,false);Build(SessionState.GetString(Destination,""));
        }
        static void Build(string output)
        {
            try
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Report,"BUILDING WINDOWS X64\nDestination: "+output+"\nStarted: "+DateTime.Now.ToString("O")+"\n");
                var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
                if(scenes.Length==0)throw new InvalidOperationException("No enabled scenes in Build Settings.");
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=Path.Combine(output,"Cardboard.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                var summary=result.summary;var report=new StringBuilder();
                report.AppendLine("RESULT: "+summary.result);report.AppendLine("Destination: "+summary.outputPath);
                report.AppendLine("Scenes: "+string.Join(", ",scenes));report.AppendLine("Total bytes: "+summary.totalSize);
                report.AppendLine("Duration: "+summary.totalTime);report.AppendLine("Errors: "+summary.totalErrors+"; warnings: "+summary.totalWarnings);
                foreach(var step in result.steps)foreach(var message in step.messages)if(message.type==LogType.Error || message.type==LogType.Exception)report.AppendLine(message.content);
                File.WriteAllText(Report,report.ToString());
            }
            catch(Exception error){File.AppendAllText(Report,"FAILED: "+error+"\n");Debug.LogException(error);}
        }
    }
}
