#if !UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EFT;
using KmyTarkovUtils;
using Newtonsoft.Json;
using SkinService.Helpers;
using SkinService.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SkinService.Models
{
    internal class SkinServiceModel
    {
        public static SkinServiceModel Instance { get; private set; }

        public PlayerSkinModel PlayerSkin;

        public PlayerSkinModel ScavPlayerSkin;

        public PlayerSkinModel CurrentPlayerSkin => IsScav ? ScavPlayerSkin : PlayerSkin;

        public bool IsScav;

        public readonly List<PlayerSkinModel> OtherPlayerSkinList = new List<PlayerSkinModel>();

        public SkinServiceView UiView;

        /// <summary>
        ///     Display names sent by the server for customization items that have no usable
        ///     game localization (mostly clothing added by other server mods).
        /// </summary>
        public readonly Dictionary<string, string> CustomNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private bool _namesLoaded;
        private bool _serverVoicesLoaded;
        private List<ServerVoice> _serverVoiceEntries;

        public GameObject SkinServicePublic;

        public readonly string ModPath = Path.Combine(BepInEx.Paths.PluginPath, "kmyuhkyuk-SkinService");

        public SkinModel[] BodySkins;

        public SkinModel[] FeetSkins;

        public SkinModel[] HeadSkins;

        public SkinModel[] HandsSkins;

        public VoiceModel[] Voices;

        public Action UpdateSkinService;

        public Action UpdateOtherPlayer;

        public Action ClearOtherPlayer;

        public Action OpenSkinServiceView;

        private SkinServiceModel()
        {
            CreateRoot();

            foreach (var keyValue in GetLanguageDictionary())
            {
                LocalizedHelper.Instance.LanguageDictionary.Add(keyValue.Key, keyValue.Value);
            }

            SettingsModel.Instance.KeySortingOrder.SettingChanged += (value, value2) => SetSortingOrder();
        }

        private void CreateRoot()
        {
            if (SkinServicePublic != null)
                return;

            SkinServicePublic = new GameObject("SkinServicePublic", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            var canvas = SkinServicePublic.GetComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                                              AdditionalCanvasShaderChannels.Normal |
                                              AdditionalCanvasShaderChannels.Tangent;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(
                SkinServicePublic.transform);

            Object.DontDestroyOnLoad(SkinServicePublic);
        }

        private void SetSortingOrder()
        {
            if (SkinServicePublic == null)
                return;

            var canvas = SkinServicePublic.GetComponent<Canvas>();

            if (canvas != null)
                canvas.sortingOrder = SettingsModel.Instance.KeySortingOrder.Value;
        }

        /// <summary>
        ///     Loads the UI bundle and creates the view. Safe to call again if the UI was destroyed.
        /// </summary>
        public void CreateUi()
        {
            if (UiView != null)
            {
                UiView.gameObject.SetActive(true);
                return;
            }

            if (SkinServicePublic == null)
                CreateRoot();

            SetSortingOrder();

            var bundlePath = Path.Combine(ModPath, "bundles", "skinservice.bundle");
            var assetBundle = AssetBundleHelper.LoadBundle(bundlePath);

            if (assetBundle == null)
            {
                SkinServicePlugin.Log.LogError($"Failed to load {bundlePath}");
                return;
            }

            var prefab = assetBundle.LoadAsset<GameObject>("SkinService");

            if (prefab == null)
            {
                SkinServicePlugin.Log.LogError("The SkinService UI prefab is missing from skinservice.bundle");
                assetBundle.Unload(false);
                return;
            }

            var ui = Object.Instantiate(prefab.ReplaceAllFont(EFTFontHelper.BenderNormal), SkinServicePublic.transform);

            SkinServicePublic.SetActive(true);
            ui.SetActive(true);

            var uiView = ui.GetComponent<SkinServiceView>();
            UiView = uiView;

            SkinServicePlugin.Log.LogInfo($"UI instantiated, view = {uiView != null}, active = {ui.activeInHierarchy}, publicActive = {SkinServicePublic.activeInHierarchy}");

            // Unity's Start is not guaranteed to run here (the UI is created during another Awake),
            // so wire the view up immediately.
            uiView?.Initialize();

            assetBundle.Unload(false);
        }

        /// <summary>
        ///     Fetches the customization-name table from the server once. Safe to call repeatedly;
        ///     a failed request is retried the next time the window is opened.
        /// </summary>
        public void LoadCustomNames()
        {
            if (_namesLoaded)
                return;

            try
            {
                var json = Compat.RequestHandlerHelper.GetJson("/skin-service/names");

                if (string.IsNullOrWhiteSpace(json))
                    return;

                var names = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                if (names == null)
                    return;

                foreach (var keyValue in names)
                {
                    CustomNames[keyValue.Key] = keyValue.Value;
                }

                _namesLoaded = true;

                SkinServicePlugin.Log.LogInfo($"Loaded {CustomNames.Count} customization names from server");
            }
            catch (Exception ex)
            {
                SkinServicePlugin.Log.LogError("Failed to load customization names: " + ex.Message);
            }
        }

        public bool TryGetCustomName(object id, out string name)
        {
            name = null;

            if (id == null)
                return false;

            return CustomNames.TryGetValue(id.ToString(), out name);
        }

        /// <summary>
        ///     Merges voices that exist in the server customization table but were not exposed
        ///     by the client's CustomizationSolver (common for bot-only/modded voices).
        /// </summary>
        public void LoadServerVoices()
        {
            if (_serverVoicesLoaded)
                return;

            try
            {
                var json = Compat.RequestHandlerHelper.GetJson("/skin-service/voices");

                if (string.IsNullOrWhiteSpace(json))
                    return;

                var serverVoices = JsonConvert.DeserializeObject<List<ServerVoice>>(json);

                if (serverVoices == null)
                    return;

                _serverVoiceEntries = serverVoices;
                _serverVoicesLoaded = true;

                MergeServerVoices();
            }
            catch (Exception ex)
            {
                SkinServicePlugin.Log.LogError("Failed to load server voices: " + ex.Message);
            }
        }

        /// <summary>
        ///     Merges the cached server voice list into <see cref="Voices" />. Also called after
        ///     the CustomizationSolver hook rebuilds the list, so server-only voices are not lost.
        /// </summary>
        public void MergeServerVoices()
        {
            if (_serverVoiceEntries == null)
                return;

            var merged = Voices != null ? Voices.ToList() : new List<VoiceModel>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var voice in merged)
            {
                if (voice.Id != null)
                    ids.Add(voice.Id.ToString());
            }

            var added = 0;

            foreach (var voice in _serverVoiceEntries)
            {
                if (string.IsNullOrWhiteSpace(voice.Id) || string.IsNullOrWhiteSpace(voice.Name))
                    continue;

                if (!ids.Add(voice.Id))
                    continue;

                try
                {
                    merged.Add(new VoiceModel(new MongoID(voice.Id), voice.Name, null));
                    added++;
                }
                catch (Exception ex)
                {
                    SkinServicePlugin.Log.LogWarning(
                        $"Invalid voice id {voice.Id} ({voice.Name}): {ex.Message}");
                }
            }

            Voices = merged.ToArray();

            SkinServicePlugin.Log.LogInfo($"Voices: {Voices.Length} ({added} added from server)");
        }

        private sealed class ServerVoice
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }
        }

        private Dictionary<string, Dictionary<string, string>> GetLanguageDictionary()
        {
            var localizedDictionary = new Dictionary<string, Dictionary<string, string>>();

            var localizedDirectory = new DirectoryInfo(Path.Combine(ModPath, "localized"));

            if (!localizedDirectory.Exists)
                return localizedDictionary;

            foreach (var localized in localizedDirectory.EnumerateFiles("*.json"))
            {
                localizedDictionary.Add(Path.GetFileNameWithoutExtension(localized.Name),
                    JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(localized.FullName)));
            }

            return localizedDictionary;
        }

        public bool TryGetSkinModelArray(EBodyModelPart bodyPart, out SkinModel[] skinModelArray)
        {
            skinModelArray = null;

            switch (bodyPart)
            {
                case EBodyModelPart.Body:
                    skinModelArray = BodySkins;
                    break;
                case EBodyModelPart.Feet:
                    skinModelArray = FeetSkins;
                    break;
                case EBodyModelPart.Head:
                    skinModelArray = HeadSkins;
                    break;
                case EBodyModelPart.Hands:
                    skinModelArray = HandsSkins;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bodyPart), bodyPart, null);
            }

            return skinModelArray != null;
        }

        public bool TryGetVoiceModelArray(out VoiceModel[] voiceModelArray)
        {
            voiceModelArray = Voices;

            return voiceModelArray != null;
        }

        public bool TryGetCurrentSkin(EBodyModelPart bodyPart, out object currentSkin)
        {
            currentSkin = null;

            if (CurrentPlayerSkin == null)
                return false;

            var customization = CurrentPlayerSkin.Customization;

            if (customization == null || !customization.Contains(bodyPart))
                return false;

            currentSkin = customization[bodyPart];

            return true;
        }

        public bool TryGetCurrentVoice(out object currentVoice)
        {
            currentVoice = null;

            if (CurrentPlayerSkin == null)
                return false;

            var customization = CurrentPlayerSkin.Customization;

            if (customization == null || !customization.Contains(EBodyModelPart.Voice))
                return false;

            currentVoice = customization[EBodyModelPart.Voice];

            return true;
        }

        public bool TryGetOtherPlayerSkinModel(int otherPlayerIndex, out PlayerSkinModel playerSkinModel)
        {
            playerSkinModel = OtherPlayerSkinList.ElementAtOrDefault(otherPlayerIndex);

            return playerSkinModel != null;
        }

        public bool TryGetFullSkinModel(int bodySkinIndex, int feetSkinIndex, int headSkinIndex, int handsSkinIndex,
            out SkinModel bodySkinModel, out SkinModel feetSkinModel, out SkinModel headSkinModel,
            out SkinModel handsSkinModel)
        {
            return TryGetSkinModel(EBodyModelPart.Body, bodySkinIndex, out bodySkinModel) &
                   TryGetSkinModel(EBodyModelPart.Feet, feetSkinIndex, out feetSkinModel) &
                   TryGetSkinModel(EBodyModelPart.Head, headSkinIndex, out headSkinModel) &
                   TryGetSkinModel(EBodyModelPart.Hands, handsSkinIndex, out handsSkinModel);
        }

        public bool TryGetSkinModel(EBodyModelPart bodyPart, int skinIndex, out SkinModel skinModel)
        {
            skinModel = null;

            if (TryGetSkinModelArray(bodyPart, out var skinModelArray))
            {
                skinModel = skinModelArray.ElementAtOrDefault(skinIndex);
            }

            return skinModel != null;
        }

        public bool TryGetVoiceModel(int voiceIndex, out VoiceModel voiceModel)
        {
            voiceModel = null;

            if (!TryGetVoiceModelArray(out var voiceModelArray))
                return false;

            voiceModel = voiceModelArray.ElementAtOrDefault(voiceIndex);

            return voiceModel != null;
        }

        // ReSharper disable once UnusedMethodReturnValue.Global
        public static SkinServiceModel Create()
        {
            if (Instance != null)
                return Instance;

            var model = new SkinServiceModel();
            Instance = model;

            // The view needs the singleton to be assigned before Initialize() runs.
            model.CreateUi();

            return model;
        }
    }
}

#endif