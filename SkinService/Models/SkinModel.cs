#if !UNITY_EDITOR

using System;
using System.IO;
using EFT;

namespace SkinService.Models
{
    internal class SkinModel
    {
        public readonly object Id;

        public readonly string Name;

        public readonly string NameLocalizationKey;

        public string LocalizationName
        {
            get
            {
                // NameLocalizationKey is the game's "{id} Name" key, so it has to be resolved
                // with the game localization table (the mod's own dictionary only holds UI labels).
                if (!string.IsNullOrEmpty(NameLocalizationKey))
                {
                    try
                    {
                        var localizationName = LocalizationExtensions.Localized(NameLocalizationKey, null);

                        if (!string.IsNullOrEmpty(localizationName) && localizationName != NameLocalizationKey)
                            return localizationName;
                    }
                    catch
                    {
                        // Fall through to the server-provided name.
                    }
                }

                // Clothing added by other server mods usually has no locale entry; the server
                // sends a display-name table for those items.
                var skinServiceModel = SkinServiceModel.Instance;

                if (skinServiceModel != null && skinServiceModel.TryGetCustomName(Id, out var customName) &&
                    !string.IsNullOrEmpty(customName))
                    return customName;

                if (!string.IsNullOrEmpty(Name) && !LooksLikeMongoId(Name))
                    return Prettify(Name);

                // Hands/voices do not always have a localized name; the bundle name is a
                // readable identifier (e.g. hands_usec_aggressor -> Hands usec aggressor).
                if (Prefab != null && !string.IsNullOrEmpty(Prefab.path))
                {
                    var bundleName = Path.GetFileNameWithoutExtension(Prefab.path);

                    if (bundleName.EndsWith(".skin", StringComparison.OrdinalIgnoreCase))
                        bundleName = bundleName.Substring(0, bundleName.Length - 5);

                    if (!string.IsNullOrEmpty(bundleName))
                        return Prettify(bundleName);
                }

                return Id?.ToString() ?? string.Empty;
            }
        }

        private static bool LooksLikeMongoId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 24)
                return false;

            foreach (var character in value)
            {
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f') ||
                      (character >= 'A' && character <= 'F')))
                    return false;
            }

            return true;
        }

        private static string Prettify(string value)
        {
            var text = value.Replace('_', ' ').Trim();

            if (text.Length == 0)
                return text;

            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        public readonly ResourceKey Prefab;

        public readonly ResourceKey WatchPrefab;

        public SkinModel(object id, string name, string nameLocalizationKey, ResourceKey prefab,
            ResourceKey watchPrefab)
        {
            Id = id;
            Name = name;
            NameLocalizationKey = nameLocalizationKey;
            Prefab = prefab;
            WatchPrefab = watchPrefab;
        }
    }
}

#endif
