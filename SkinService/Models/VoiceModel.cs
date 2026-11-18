#if !UNITY_EDITOR

using EFT;
using SkinService.Compat;

namespace SkinService.Models
{
    internal class VoiceModel : SkinModel
    {
        // Resolved lazily: the game's voice bundle paths are populated while starting up,
        // and the customization solver may be constructed before that finishes.
        public ResourceKey VoicePrefab => new ResourceKey
        {
            path = VoiceHelper.TakePhrasePath(Name),
            rcid = Name
        };

        public VoiceModel(object id, string name, string nameLocalizationKey) : base(id, name, nameLocalizationKey,
            null, null)
        {
        }
    }
}

#endif
