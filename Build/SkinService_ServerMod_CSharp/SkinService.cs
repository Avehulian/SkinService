using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;

namespace SkinService
{
    /// <summary>
    /// Mod metadata required by the SPT 4.1 mod loader.
    /// </summary>
    public record ModMetadata : IModMetadata
    {
        public string ModGuid { get; init; } = "com.kmyuhkyuk.SkinService";
        public string Name { get; init; } = "SkinService";
        public string Author { get; init; } = "kmyuhkyuk";
        public List<string>? Contributors { get; init; }
        public SemanticVersioning.Version Version { get; init; } = new("1.6.0");
        public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.6");
        public List<string>? Incompatibilities { get; init; }
        public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
        public string? Url { get; init; } = "https://github.com/kmyuhkyuk/SkinService";
        public string License { get; init; } = "GNU GPLv3";
        public bool HasPrepatcher { get; init; } = false;
    }

    /// <summary>
    /// Dynamic router for the SkinService endpoints.
    /// </summary>
    [Injectable(TypePriority = OnLoadOrder.Routers + 1)]
    public class SkinServiceDynamicRouter : DynamicRouter
    {
        public SkinServiceDynamicRouter(JsonUtil jsonUtil, SkinServiceCallbacks callbacks)
            : base(jsonUtil, new RouteAction[]
            {
                new RouteAction<SkinRequestData>("/skin-service/pmc/change",
                    (url, info, sessionId, output, cancellationToken) =>
                        callbacks.ChangeSkin(info, sessionId, false, cancellationToken)),
                new RouteAction<VoiceRequestData>("/skin-service/pmc/voice/change",
                    (url, info, sessionId, output, cancellationToken) =>
                        callbacks.ChangeVoice(info, sessionId, false, cancellationToken)),
                new RouteAction<SkinRequestData>("/skin-service/scav/change",
                    (url, info, sessionId, output, cancellationToken) =>
                        callbacks.ChangeSkin(info, sessionId, true, cancellationToken)),
                new RouteAction<VoiceRequestData>("/skin-service/scav/voice/change",
                    (url, info, sessionId, output, cancellationToken) =>
                        callbacks.ChangeVoice(info, sessionId, true, cancellationToken)),
                // Returns { "<item id>": "Display name" } for every customization item in the
                // database, including clothing added by other server mods.
                new RouteAction<EmptyRequestData>("/skin-service/names",
                    (url, info, sessionId, output, cancellationToken) =>
                        new ValueTask<string>(callbacks.GetNamesJson())),
                // Every voice item in the customization table (base + modded), so the client
                // can offer voices the game's solver may not surface.
                new RouteAction<EmptyRequestData>("/skin-service/voices",
                    (url, info, sessionId, output, cancellationToken) =>
                        new ValueTask<string>(callbacks.GetVoicesJson()))
            })
        {
        }
    }

    /// <summary>
    /// Applies the requested customization to the player's PMC or Scav profile and saves it.
    /// </summary>
    [Injectable]
    public class SkinServiceCallbacks(
        ProfileHelper profileHelper,
        SaveServer saveServer,
        TemplateTable templateTable,
        LocaleService localeService,
        ModHelper modHelper,
        ISptLogger<SkinServiceCallbacks> logger)
    {
        private const string VoiceParentId = "5fc100cf95572123ae738483";

        private Dictionary<string, string>? _customClothingNames;

        /// <summary>
        ///     Builds the display-name table from the live customization template table. This is
        ///     built per request so clothing added by mods after startup is included.
        /// </summary>
        public string GetNamesJson()
        {
            var names = new Dictionary<string, string>();
            var locale = GetLocaleDictionary();

            var customization = templateTable.Customization;

            if (customization != null)
            {
                foreach (var entry in customization)
                {
                    var item = entry.Value;

                    if (item == null)
                        continue;

                    var id = entry.Key.ToString();
                    var properties = item.Properties;

                    // WTT custom clothing configs carry the real outfit names; theirs IDs are
                    // also present in the template table, so check that map first.
                    var displayName = GetCustomClothingNames().TryGetValue(id, out var customName)
                        ? customName
                        : null;

                    // Modded clothing (WTT custom clothing, ISB, etc.) also stores locale keys in
                    // Properties.Name; the actual translations live in the server locale table.
                    if (string.IsNullOrWhiteSpace(displayName))
                    {
                        displayName = LookupLocale(locale, id + " Name") ??
                                      LookupLocale(locale, id) ??
                                      LookupLocale(locale, properties?.Name) ??
                                      LookupLocale(locale, properties?.ShortName);
                    }

                    if (string.IsNullOrWhiteSpace(displayName) && !LooksLikeLocaleKey(properties?.Name, id))
                        displayName = properties?.Name;

                    if (string.IsNullOrWhiteSpace(displayName))
                        displayName = GetBundleName(properties);

                    if (string.IsNullOrWhiteSpace(displayName))
                        displayName = Prettify(item.Name);

                    if (string.IsNullOrWhiteSpace(displayName))
                        continue;

                    names[id] = displayName;
                }
            }

            logger.Info($"[SkinService] Sending {names.Count} customization names");

            return JsonSerializer.Serialize(names);
        }

        /// <summary>
        ///     Returns every voice customization item (base and modded) so the client can
        ///     merge voices that its CustomizationSolver did not expose.
        /// </summary>
        public string GetVoicesJson()
        {
            var voices = new List<object>();

            var customization = templateTable.Customization;

            if (customization != null)
            {
                foreach (var entry in customization)
                {
                    var item = entry.Value;

                    if (item == null ||
                        !string.Equals(item.Parent, VoiceParentId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var id = entry.Key.ToString();
                    var displayName = item.Properties?.Name;

                    if (string.IsNullOrWhiteSpace(displayName) || LooksLikeLocaleKey(displayName, id))
                        displayName = item.Name;

                    voices.Add(new
                    {
                        id,
                        name = item.Name ?? string.Empty,
                        displayName = displayName ?? item.Name ?? id,
                        side = item.Properties?.Side
                    });
                }
            }

            logger.Info($"[SkinService] Sending {voices.Count} voice entries");

            return JsonSerializer.Serialize(voices);
        }

        /// <summary>
        ///     Reads the WTT-ServerCommonLib <c>db/CustomClothing/*.json</c> files from every
        ///     installed server mod. These configs map top/hands/bottom item ids to outfit names
        ///     that are otherwise only available to the outfit (suite) locale key.
        /// </summary>
        private Dictionary<string, string> GetCustomClothingNames()
        {
            if (_customClothingNames != null)
                return _customClothingNames;

            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var ownModFolder = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
                var modsFolder = Directory.GetParent(ownModFolder)?.FullName;

                if (modsFolder == null)
                    return _customClothingNames = names;

                var desiredLocale = localeService.GetDesiredGameLocale();

                foreach (var modFolder in Directory.GetDirectories(modsFolder))
                {
                    var clothingFolder = Path.Combine(modFolder, "db", "CustomClothing");

                    if (!Directory.Exists(clothingFolder))
                        continue;

                    foreach (var file in Directory.GetFiles(clothingFolder, "*.json"))
                    {
                        try
                        {
                            ReadCustomClothingFile(file, desiredLocale, names);
                        }
                        catch (Exception ex)
                        {
                            logger.Warning($"[SkinService] Could not read {file}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warning("[SkinService] Could not scan custom clothing configs: " + ex.Message);
            }

            logger.Info($"[SkinService] Loaded {names.Count} custom clothing item names");

            return _customClothingNames = names;
        }

        private static void ReadCustomClothingFile(string file, string? desiredLocale,
            Dictionary<string, string> names)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));

            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return;

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                var name = GetConfigName(entry, desiredLocale);

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                foreach (var idProperty in new[] { "topId", "handsId", "bottomId", "feetId", "suiteId", "outfitId" })
                {
                    if (!entry.TryGetProperty(idProperty, out var idElement) ||
                        idElement.ValueKind != JsonValueKind.String)
                        continue;

                    var id = idElement.GetString();

                    if (!string.IsNullOrWhiteSpace(id))
                        names[id] = name;
                }
            }
        }

        private static string? GetConfigName(JsonElement entry, string? desiredLocale)
        {
            if (!entry.TryGetProperty("locales", out var locales) || locales.ValueKind != JsonValueKind.Object)
                return null;

            if (!string.IsNullOrWhiteSpace(desiredLocale) &&
                TryGetLocaleName(locales, desiredLocale, out var localized))
                return localized;

            if (TryGetLocaleName(locales, "en", out localized))
                return localized;

            foreach (var locale in locales.EnumerateObject())
            {
                if (TryGetLocaleName(locales, locale.Name, out localized))
                    return localized;
            }

            return null;
        }

        private static bool TryGetLocaleName(JsonElement locales, string localeCode, out string? name)
        {
            name = null;

            if (!locales.TryGetProperty(localeCode, out var locale) || locale.ValueKind != JsonValueKind.Object)
                return false;

            if (!locale.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                return false;

            name = nameElement.GetString();

            return !string.IsNullOrWhiteSpace(name);
        }

        private Dictionary<string, string>? GetLocaleDictionary()
        {
            try
            {
                var locale = localeService.GetDesiredGameLocale();

                return localeService.GetLocaleDb(string.IsNullOrWhiteSpace(locale) ? "en" : locale);
            }
            catch (Exception ex)
            {
                logger.Warning("[SkinService] Could not read server locale table: " + ex.Message);
                return null;
            }
        }

        private static string? LookupLocale(Dictionary<string, string>? locale, string? key)
        {
            if (locale == null || string.IsNullOrWhiteSpace(key))
                return null;

            return locale.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
        }

        private static bool LooksLikeLocaleKey(string? value, string id)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (value.StartsWith("Default", StringComparison.OrdinalIgnoreCase))
                return true;

            if (value.StartsWith(id, StringComparison.OrdinalIgnoreCase))
                return true;

            return value.EndsWith("_name", StringComparison.OrdinalIgnoreCase) ||
                   value.EndsWith("_desc", StringComparison.OrdinalIgnoreCase) ||
                   value.EndsWith("_description", StringComparison.OrdinalIgnoreCase) ||
                   value.EndsWith("_shortName", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetBundleName(SPTarkov.Server.Core.Models.Eft.Common.Tables.CustomizationProperties? properties)
        {
            if (properties == null)
                return null;

            var prefab = properties.Prefab as SPTarkov.Server.Core.Models.Eft.Common.Tables.Prefab ??
                         properties.AssetPath;

            if (prefab == null || string.IsNullOrWhiteSpace(prefab.Path))
                return null;

            var bundleName = Path.GetFileNameWithoutExtension(prefab.Path);

            if (bundleName.EndsWith(".skin", StringComparison.OrdinalIgnoreCase))
                bundleName = bundleName.Substring(0, bundleName.Length - 5);

            return Prettify(bundleName);
        }

        private static string? Prettify(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var text = value.Replace('_', ' ').Trim();

            return text.Length == 0 ? null : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
        public async ValueTask<string> ChangeSkin(SkinRequestData request, MongoId sessionId, bool isScav,
            CancellationToken cancellationToken)
        {
            var profile = isScav
                ? profileHelper.GetScavProfile(sessionId)
                : profileHelper.GetPmcProfile(sessionId);

            if (profile?.Customization == null)
            {
                logger.Warning($"[SkinService] Could not find {(isScav ? "scav" : "pmc")} profile for {sessionId}");
                return Error("profile not found");
            }

            if (!string.IsNullOrWhiteSpace(request.BodyId))
                profile.Customization.Body = request.BodyId;

            if (!string.IsNullOrWhiteSpace(request.FeetId))
                profile.Customization.Feet = request.FeetId;

            if (!string.IsNullOrWhiteSpace(request.HeadId))
                profile.Customization.Head = request.HeadId;

            if (!string.IsNullOrWhiteSpace(request.HandsId))
                profile.Customization.Hands = request.HandsId;

            await saveServer.SaveProfileAsync(sessionId, cancellationToken);

            return Ok();
        }

        public async ValueTask<string> ChangeVoice(VoiceRequestData request, MongoId sessionId, bool isScav,
            CancellationToken cancellationToken)
        {
            var profile = isScav
                ? profileHelper.GetScavProfile(sessionId)
                : profileHelper.GetPmcProfile(sessionId);

            if (profile?.Customization == null)
            {
                logger.Warning($"[SkinService] Could not find {(isScav ? "scav" : "pmc")} profile for {sessionId}");
                return Error("profile not found");
            }

            if (!string.IsNullOrWhiteSpace(request.VoiceId))
                profile.Customization.Voice = request.VoiceId;

            await saveServer.SaveProfileAsync(sessionId, cancellationToken);

            return Ok();
        }

        private static string Ok()
        {
            return "{\"response\":\"OK\"}";
        }

        private static string Error(string message)
        {
            return JsonSerializer.Serialize(new { response = "ERROR", message });
        }
    }
}
