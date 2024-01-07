using System;


namespace SatorImaging.UnitySourceGenerator
{
    public sealed class USGContext
    {
        public Type TargetClass { get; init; }
        public string AssetPath { get; init; }
        public string OutputPath { get; set; }

    }
}
