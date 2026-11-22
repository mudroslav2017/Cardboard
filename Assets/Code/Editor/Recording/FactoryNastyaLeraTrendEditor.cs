using UnityEditor;

namespace Cardboard.Editor
{
    public static class FactoryNastyaLeraTrendEditor
    {
        [MenuItem("Cardboard/Preview Nastya and Lera trend")]
        public static void Preview() => FactoryTrendExport.Request(FactoryTrendExport.Kind.NastyaLera, false);

        [MenuItem("Cardboard/Export Nastya and Lera trend (MP4)")]
        public static void Export() => FactoryTrendExport.Request(FactoryTrendExport.Kind.NastyaLera, true);
    }
}
