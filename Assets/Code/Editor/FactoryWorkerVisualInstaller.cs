using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Cardboard.Editor
{
    public static class FactoryWorkerVisualInstaller
    {
        public static void Apply(Transform root)
        {
            var game=root.GetComponent<CardboardFactoryGame>();
            var expressions=root.GetComponent<FactoryWorkerExpressions>();if(!expressions)expressions=root.gameObject.AddComponent<FactoryWorkerExpressions>();
            expressions.game=game;expressions.workers=game.crowd.runners.ToArray();expressions.people=root.GetComponentsInChildren<FactoryWorkerAppearance>(true);game.expressions=expressions;
            FactoryPhysicsInstaller.InstructionHints(game.instruction);
            EditorUtility.SetDirty(expressions);EditorUtility.SetDirty(game);
        }
        public static void ApplyCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Update worker visuals in Edit Mode.");
            var game=Object.FindFirstObjectByType<CardboardFactoryGame>();
            ImportedCharacterInstaller.Prepare();ImportedCharacterInstaller.ReplaceInScene(game.gameObject.scene);
            Apply(game.transform);FactoryReactionPresentationInstaller.RefreshMessages(game.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            var report=new StringBuilder();report.AppendLine("SUCCESS: Lera and Kolya, three double-sided expression materials, per-shift suspicion group, angry reactions/capture, larger centered messages and 2x instruction text.");
            foreach(var person in game.expressions.people)report.AppendLine(person.name+" normal="+person.normal.mainTexture.name+" suspicious="+person.suspicious.mainTexture.name+" angry="+person.angry.mainTexture.name);
            foreach(var reaction in game.GetComponentsInChildren<FactoryWorkerImpact>())
                report.AppendLine(reaction.name+" message="+reaction.message.GetComponent<MeshFilter>().sharedMesh.bounds+" anchor="+reaction.reactionIcons[0].localPosition);
            File.WriteAllText("Library/FactoryWorkerVisuals.txt",report.ToString());
        }
    }
}
