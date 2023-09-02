using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace SatorImaging.UnitySourceGenerator
{
    public static class USGReflection
    {
        public readonly static BindingFlags PUBLIC_INSTANCE_FIELD_OR_PROPERTY
            = BindingFlags.Public | BindingFlags.Instance
            | BindingFlags.DeclaredOnly
            | BindingFlags.GetProperty | BindingFlags.SetProperty
            | BindingFlags.GetField | BindingFlags.SetField
            ;


        ///<summary>Note that constructor (`.ctor`) is always ignored.</summary>
        public static MemberInfo[] GetAllPublicInstanceFieldAndProperty(Type cls, params string[] namesToIgnore)
            => GetMembers(cls, PUBLIC_INSTANCE_FIELD_OR_PROPERTY, namesToIgnore ?? new string[] { });

        static MemberInfo[] GetMembers(Type cls, BindingFlags flags, string[] namesToIgnore)
        {
            if (cls == null) throw new ArgumentNullException(nameof(cls));


            var members = cls.GetMembers(flags)
                .Where(x =>
                {
                    if (namesToIgnore.Contains(x.Name) || x.Name == ".ctor")
                        return false;
                    return true;
                })
                .ToArray();
            ;

            return members;
        }


        ///<summary>Try get value type of field or property.</summary>
        public static bool TryGetFieldOrPropertyType(MemberInfo info, out Type outType)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));


            outType = null;
            if (info is FieldInfo field)
            {
                outType = field.FieldType;
            }
            else if (info is PropertyInfo property)
            {
                outType = property.PropertyType;
            }


            if (outType == null)
                return false;

            return true;
        }


        ///<summary>Enum names and values as a dictionary.</summary>
        ///<typeparam name="TValue">int, uint, long or something</typeparam>
        public static Dictionary<string, TValue> GetEnumNamesAndValuesAsDictionary<TValue>(Type enumType)
            where TValue : struct
        {
            if (enumType?.IsEnum != true) throw new ArgumentException(nameof(enumType));


            var names = enumType.GetEnumNames();
            var dict = new Dictionary<string, TValue>(capacity: names.Length);

            int iter = -1;
            foreach (TValue val in enumType.GetEnumValues())
            {
                iter++;
                dict.Add(names[iter], val);
            }

            return dict;
        }


#if UNITY_2021_3_OR_NEWER
        ///<summary>Enum names and values as a tuple.</summary>
        ///<typeparam name="TValue">int, uint, long or something</typeparam>
        public static (string[], TValue[]) GetEnumNamesAndValuesAsTuple<TValue>(Type enumType)
            where TValue : struct
        {
            if (enumType?.IsEnum != true) throw new ArgumentException(nameof(enumType));

            return (enumType.GetEnumNames(), enumType.GetEnumValues().Cast<TValue>().ToArray());
        }
#endif


    }
}
