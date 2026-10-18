using System;
using UnityEditor;
using UnityEngine;

namespace Cardboard.Editor
{
    public static class CardboardTypographyInstaller
    {
        public static void Prepare()
        {
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/FF_troublemarker.otf");
            if(!font) throw new InvalidOperationException("Missing supplied font: Assets/Font/FF_troublemarker.otf");
            const string path="Assets/Cardboard/Resources/Typography.asset";
            var settings=AssetDatabase.LoadAssetAtPath<CardboardTypography>(path);
            if(!settings) {settings=ScriptableObject.CreateInstance<CardboardTypography>();AssetDatabase.CreateAsset(settings,path);}
            settings.font=font;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
        }
    }
}
