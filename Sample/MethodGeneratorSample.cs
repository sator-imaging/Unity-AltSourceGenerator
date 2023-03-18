using SatorImaging.UnitySourceGenerator;

namespace Sample
{
    // NOTE: Copy this file to Assets/ folder to enable source generator.
    //       USG process files in Assets/ folder only.
    [UnitySourceGenerator(typeof(Sample.PanicMethodGenerator), OverwriteIfFileExists = false)]
    internal partial class MethodGeneratorSample
    {
    }

}
