using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Cardboard.Editor
{
    public static class FactoryWebMouseVerification
    {
        [MenuItem("Cardboard/Verify web mouse look")]
        public static void Run()
        {
            var report=new List<string>();
            void Check(bool passed,string message){report.Add((passed?"PASS ":"FAIL ")+message);}
            GameObject root=null;Mouse mouse=null;var previousMouse=Mouse.current;
            try
            {
                root=new GameObject("Mouse look verification");
                var player=new GameObject("Test player");player.transform.SetParent(root.transform,false);
                var lens=new GameObject("Test camera",typeof(Camera));lens.transform.SetParent(root.transform,false);
                var look=lens.AddComponent<CardboardCamera>();look.player=player.transform;look.sensitivity=.1f;
                look.ResetView(0);look.ApplyLookDelta(new Vector2(20,-10));look.RefreshPlayerPose(0);
                Check(Mathf.Abs(Mathf.DeltaAngle(look.transform.eulerAngles.y,2))<.001f &&
                    Mathf.Abs(Mathf.DeltaAngle(look.transform.eulerAngles.x,1))<.001f,"Relative movement preserves camera sensitivity and vertical direction");
                var view=look.transform.rotation;
                look.ApplyLookDelta(new Vector2(float.NaN,0));look.ApplyLookDelta(new Vector2(0,float.PositiveInfinity));look.RefreshPlayerPose(0);
                Check(Quaternion.Angle(view,look.transform.rotation)<.001f,"Invalid input never corrupts the camera rotation");
                mouse=InputSystem.AddDevice<Mouse>("Web mouse verification");
                foreach(var position in new[]{Vector2.zero,new Vector2(Screen.width,Screen.height),new Vector2(-5000,6000)})
                {
                    look.ResetView(0);
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=position,delta=new Vector2(30,-20)});
                    InputSystem.Update();look.ApplyLookDelta(CardboardWebMouse.ReadDelta());look.RefreshPlayerPose(0);
                    Check(Mathf.Abs(Mathf.DeltaAngle(look.transform.eulerAngles.y,3))<.001f &&
                        Mathf.Abs(Mathf.DeltaAngle(look.transform.eulerAngles.x,2))<.001f,
                        "Desktop/editor relative input is unaffected by absolute cursor position "+position);
                }
                look.ResetView(0);look.ApplyLookDelta(new Vector2(0,-5000));look.RefreshPlayerPose(0);
                Check(Mathf.Abs(Mathf.DeltaAngle(0,look.transform.eulerAngles.x)-80)<.001f,"Vertical input retains the upper view limit");
                look.ApplyLookDelta(new Vector2(0,10000));look.RefreshPlayerPose(0);
                Check(Mathf.Abs(Mathf.DeltaAngle(0,look.transform.eulerAngles.x)+80)<.001f,"Vertical input retains the lower view limit");
                look.transform.rotation=Quaternion.Euler(13,359,0);look.AdoptCurrentView();look.ApplyLookDelta(new Vector2(20,0));look.RefreshPlayerPose(0);
                Check(Mathf.Abs(Mathf.DeltaAngle(look.transform.eulerAngles.y,1))<.001f &&
                    Mathf.Abs(Mathf.DeltaAngle(look.transform.eulerAngles.x,13))<.001f,"Rotation crosses the 360-degree seam without jumping");
                look.ResetView(35);look.RefreshPlayerPose(0);
                Check(Mathf.Abs(Mathf.DeltaAngle(look.transform.eulerAngles.y,35))<.001f &&
                    Mathf.Abs(Mathf.DeltaAngle(0,look.transform.eulerAngles.x))<.001f,"Starting or resuming a view resets the camera cleanly");
            }
            catch(Exception error){report.Add("FAIL "+error);}
            finally
            {
                if(root)UnityEngine.Object.DestroyImmediate(root);
                if(mouse!=null && mouse.added)InputSystem.RemoveDevice(mouse);
                if(previousMouse!=null && previousMouse.added)previousMouse.MakeCurrent();
            }
            int failed=report.Count(line=>line.StartsWith("FAIL"));
            File.WriteAllLines("Library/FactoryWebMouseVerification.txt",new[]{"RESULT: "+report.Count(line=>line.StartsWith("PASS"))+" PASS, "+failed+" FAIL"}.Concat(report));
            if(failed>0)throw new InvalidOperationException("Web mouse camera verification failed.");
        }
        public static void BuildRequestedBatch()
        {
            Run();FactoryWebGLBuilder.BuildBatch();
        }
    }
}