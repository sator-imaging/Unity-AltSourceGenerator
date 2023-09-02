using System;
using System.Text;

namespace SatorImaging.UnitySourceGenerator
{
    ///<summary>
    /// Implement the following methods on generator class.<br/>
    /// - static string OutputFileName()<br/>
    /// - static bool Emit(USGContext, StringBuilder)
    ///</summary>
    // TODO: Implement "IUnitySourceGenerator" (C# 11.0)
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class UnitySourceGeneratorAttribute : Attribute
    {
        readonly Type generatorClass;

        public UnitySourceGeneratorAttribute(Type generatorClass = null)
        {
            this.generatorClass = generatorClass;
        }

        public Type GeneratorClass => generatorClass;

        public bool OverwriteIfFileExists { get; set; } = true;
        public Encoding OutputFileEncoding { get; set; } = Encoding.UTF8;

    }
}
