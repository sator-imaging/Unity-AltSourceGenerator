using System;
using System.Runtime.CompilerServices;
using System.Text;


namespace SatorImaging.UnitySourceGenerator
{
    public static class StringBuilderExtension
    {
        public static char CurrentIndentChar = ' ';
        public static int CurrentIndentSize = 4;
        public static int CurrentIndentLevel = 0;


        private static int s_lastIndentLevel = int.MinValue;  // must be different from CurrentLevel to initialize indentString

        private static string s_indentString = string.Empty;
        public static string IndentString
        {
            get
            {
                if (s_lastIndentLevel == CurrentIndentLevel)
                    return s_indentString;

                s_lastIndentLevel = CurrentIndentLevel;
                s_indentString = new string(CurrentIndentChar, CurrentIndentLevel * CurrentIndentSize);
                return s_indentString;
            }
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void IndentChar(this StringBuilder sb, char value) => CurrentIndentChar = value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void IndentSize(this StringBuilder sb, int size) => CurrentIndentSize = Math.Max(0, size);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void IndentLevel(this StringBuilder sb, int level) => CurrentIndentLevel = Math.Max(0, level);


        ///<summary>Shorthand for IndentLine(); IndentLevel++;</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IndentBegin(this StringBuilder sb, string value = null)
        {
            sb.IndentLine(value);
            CurrentIndentLevel = Math.Max(0, CurrentIndentLevel + 1);
        }

        ///<summary>Shorthand for IndentLevel--; IndentLine();</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IndentEnd(this StringBuilder sb, string value = null)
        {
            CurrentIndentLevel = Math.Max(0, CurrentIndentLevel - 1);
            sb.IndentLine(value);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IndentLine(this StringBuilder sb, string value = null)
        {
            sb.IndentAppend(value);
            sb.AppendLine();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IndentAppend(this StringBuilder sb, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            sb.Append(IndentString);
            sb.Append(value);
        }


    }
}
