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
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class UnitySourceGeneratorAttribute : Attribute
    {
        // NOTE: not follows C# guideline but need read/write access on this field
        //       to support self-emit generator which can be initialized without generator type parameter.
        internal /*readonly*/ Type _generatorClass;

        public UnitySourceGeneratorAttribute(Type generatorClass = null)
        {
            this._generatorClass = generatorClass;
        }

        public Type GeneratorClass => _generatorClass;

        public bool OverwriteIfFileExists { get; set; } = true;
        public Encoding OutputFileEncoding { get; set; } = Encoding.UTF8;

    }
}
