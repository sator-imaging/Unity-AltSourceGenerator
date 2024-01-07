#if false == UNITY_2022_1_OR_NEWER

// NOTE: to use record & init accessor in Unity 2021
//       https://stackoverflow.com/questions/62648189/testing-c-sharp-9-0-in-vs2019-cs0518-isexternalinit-is-not-defined-or-imported

namespace System.Runtime.CompilerServices
{
    [ComponentModel.EditorBrowsable(ComponentModel.EditorBrowsableState.Never)]
    /*internal*/ class IsExternalInit
    {
    }
}

#endif
