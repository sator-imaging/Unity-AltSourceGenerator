using System;

namespace SatorImaging.UnitySourceGenerator
{
    public static class USGFullNameOf
    {
        ///<summary>Refactor-ready full name generator.</summary>
        ///<remarks>Ex: usg&lt;MyClass.InnerClass&gt;(nameof(Something), "Generated")</remarks>
        ///<returns>global::Full.Namespace.To.MyClass.InnerClass.Something.Generated</returns>
        public static string usg<T>(params string[] memberNames)
        {
            var ret = GetTypeDef(typeof(T), true);
            if (memberNames == null)
                return ret;

            for (int i = 0; i < memberNames.Length; i++)
            {
                if (memberNames[i] == null || memberNames[i].Length == 0)
                    continue;
                ret += '.' + memberNames[i];
            }
            return ret;
        }

        ///<summary>Get type definition literal of supplied object.</summary>
        ///<param name="valueOrType">Throw when valueOrType is null.</param>
        ///<returns>Ex: Dictionary&lt;int, List&lt;Dictionary&lt;string, float[][]&gt;[]&gt;&gt;[]</returns>
        public static string usg(object valueOrType, bool isFullName = true)
        {
            if (valueOrType == null)
                throw new ArgumentNullException();

            if (valueOrType is Type t)
                return GetTypeDef(t, isFullName);

            return GetTypeDef(valueOrType.GetType(), isFullName);
        }


        //internals
        static string GetTypeDef(Type t, bool isFullName)// Immutable collections cannot use new(){...}, bool isNew)
        {
            var ns = t.Namespace;
            ns = string.IsNullOrEmpty(ns) ? string.Empty : ns + '.';
            // NOTE: FullName sometimes returns AssemblyQualifiedName.
            var ret = t.FullName;
            if (ns.Length > 0)
                ret = ret.Substring(ns.Length, ret.Length - ns.Length);
            // remove assembly info
            int idx = ret.IndexOf(',');
            if (idx > -1)
                ret = ret.Substring(0, idx);

            // NOTE: class or struct defined in other class is separated with + sign.
            //       ex: Namespace.MyClass+InnerClass
            ret = ret.Replace('+', '.');

            int arrayDim = 0;
            while (t.IsArray)
            {
                arrayDim++;
                ret = ret.Substring(0, ret.Length - 2);  //[]
                t = t.GetElementType();
            }

            bool isBuiltinType = TryGetBuiltinDef(ref ret);
            if (!isBuiltinType && isFullName && ns.Length > 0)
                ret = ns + ret;

            if (t.IsGenericType)
            {
                idx = ret.IndexOf('`');
                ret = ret.Substring(0, idx) + '<';
                bool comma = false;
                foreach (var nested in t.GetGenericArguments())
                {
                    ret += (comma ? ", " : string.Empty) + GetTypeDef(nested, isFullName);
                    comma = true;
                }
                ret += '>';
            }

            while (arrayDim > 0)
            {
                ret += "[]";
                arrayDim--;
            }

            if (!isBuiltinType && isFullName)
                ret = "global::" + ret;

            return ret;
        }

        static bool TryGetBuiltinDef(ref string name)
        {
            string ret = name switch
            {
                "SByte" => "sbyte",
                "Byte" => "byte",
                "Int16" => "short",
                "UInt16" => "ushort",
                "Int32" => "int",
                "UInt32" => "uint",
                "Int64" => "long",
                "UInt64" => "ulong",

                "Single" => "float",
                "Double" => "double",
                "Decimal" => "decimal",

                "Boolean" => "bool",
                "Char" => "char",
                "String" => "string",
                "Object" => "object",

                _ => null,
            };

            if (ret == null)
                return false;

            name = ret;
            return true;
        }

    }
}
