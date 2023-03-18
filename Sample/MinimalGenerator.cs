using System.Text;
using SatorImaging.UnitySourceGenerator;

namespace Sample
{
    // NOTE: Copy this file to Assets/ folder to enable source generator.
    //       USG process files in Assets/ folder only.
    [UnitySourceGenerator(OverwriteIfFileExists = false)]
    class MinimalGenerator
    {
        static string OutputFileName() => "Test.cs";  // USG automatically update to -> Test.<ClassName>.g.cs

        static bool Emit(USGContext context, StringBuilder sb)
        {
            // write content into passed StringBuilder.
            sb.AppendLine($"Asset Path: {context.AssetPath}");
            sb.AppendLine($"Hello World from {typeof(MinimalGenerator)}");

            // you can modify output path. initial file name is that USG updated.
            // NOTE: USG doesn't care the modified path is valid or not.
            context.OutputPath += "_MyFirstTest.txt";

            // return true to tell USG to write content into OutputPath. false to do nothing.
            return true;
        }
    }


    // NOTE: "abstract static" definition in interface requires C# 11.0 or later.
    //       following code is just for reference for future enhancement.
#if UNITY_2025_1_OR_NEWER

    public interface IUnitySourceGenerator
    {
        ///<summary>Return true if write StringBuilder content to file.</summary>
        abstract static bool Emit(in USGContext context, in StringBuilder sb);

        ///<summary>Return just only filename with extension. SourceGenerator will automatically arrange output path.</summary>
        abstract static string OutputFileName();

    }

#endif


}
