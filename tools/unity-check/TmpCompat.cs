// The editor-bundled uGUI 2.0 the project uses still has TMPro.TMP_FontAsset; the registry's
// 3.0 cut this tool compiles renamed it to UnityEngine.TextCore.Text.FontAsset. Same surface,
// for compiling only: like the reference assemblies, nothing here runs.
namespace TMPro
{
    public class TMP_FontAsset : UnityEngine.TextCore.Text.FontAsset
    {
        public static new TMP_FontAsset CreateFontAsset(UnityEngine.Font font) => throw new System.NotSupportedException();
    }
}
