#if !UNITY_EDITOR

using System;
using System.Reflection;
using HarmonyLib;
using KmyTarkovReflection;

namespace SkinService.Compat
{
    /// <summary>
    ///     Thin wrapper around the game's voice phrase path resolver (the EFTApi VoiceHelper equivalent).
    /// </summary>
    public static class VoiceHelper
    {
        private static readonly Func<string, string> RefTakePhrasePath;

        static VoiceHelper()
        {
            var voiceClassType = RefTool.GetEftType(x =>
                x.GetMethod("TakePhrasePath", BindingFlags.Static | RefTool.Public) != null);

            RefTakePhrasePath = AccessTools.MethodDelegate<Func<string, string>>(
                voiceClassType.GetMethod("TakePhrasePath", BindingFlags.Static | RefTool.Public));
        }

        public static string TakePhrasePath(string name)
        {
            return string.IsNullOrEmpty(name) ? string.Empty : RefTakePhrasePath(name);
        }
    }
}

#endif
