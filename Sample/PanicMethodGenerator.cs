using System.Text;
using SatorImaging.UnitySourceGenerator;

namespace Sample
{
    // HOW TO USE: Add the following attribute to *target* class.
    //             Note that target class must be defined as partial.
    // [UnitySourceGenerator(typeof(Sample.PanicMethodGenerator))]
    public class PanicMethodGenerator
    {
        static string OutputFileName() => "PanicMethod.cs";  // -> PanicMethod.<TargetClass>.<GeneratorClass>.g.cs

        static bool Emit(USGContext context, StringBuilder sb)
        {
            if (!context.TargetClass.IsClass || context.TargetClass.IsAbstract)
                return false;  // return false to tell USG doesn't write file.

            // code generation
            sb.Append($@"
namespace {context.TargetClass.Namespace}
{{
    internal partial class {context.TargetClass.Name}
    {{
        public void Panic() => throw new System.Exception();
    }}
}}
");
            return true;
        }

    }
}
