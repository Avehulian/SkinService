#if !UNITY_EDITOR

using System;
using EFT;
using KmyTarkovReflection;

namespace SkinService.Compat
{
    /// <summary>
    ///     Thin wrapper around <see cref="BaseSpeaker.Init" /> (the EFTApi SpeakerData equivalent).
    /// </summary>
    public static class SpeakerHelper
    {
        private static readonly Action<BaseSpeaker, EPlayerSide, int, string, bool> RefInit =
            RefHelper.ObjectMethodDelegate<Action<BaseSpeaker, EPlayerSide, int, string, bool>>(
                typeof(BaseSpeaker).GetMethod("Init", RefTool.Public));

        public static void Init(BaseSpeaker speaker, EPlayerSide side, int playerId, string voiceName,
            bool registerInSpeakerManager = true)
        {
            if (speaker == null)
                return;

            RefInit(speaker, side, playerId, voiceName, registerInSpeakerManager);
        }
    }
}

#endif
