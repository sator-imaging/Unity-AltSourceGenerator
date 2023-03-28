using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SatorImaging.UnitySourceGenerator
{
    public static class USGFullNameOf
    {
        ///<summary>usage: usg&lt;MyClass&gt;(nameof(MyClass.MyProperty))</summary>
        ///<remarks>each memberNames will be prepended with '.'</remarks>
        ///<returns>full name of the class or method</returns>
        public static string usg<T>(params string[] memberNames)
            => usg(typeof(T), memberNames);

        public static string usg(Type cls, params string[] memberNames)
        {
            if (cls == null) throw new ArgumentNullException(nameof(cls));


            // TODO: support generic class.
            //       --> Generic`1 -> Generic<TSomething>
            // NOTE: class or struct in class is separated with + sign. ex: Namespace.MyClass+ClassInClass
            var ret = cls.FullName.Replace('+', '.');

            for (int i = 0; i < memberNames.Length; i++)
            {
                ret += "." + memberNames[i];
            }
            return ret;
        }


    }
}
