using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if !UNITY_EDITOR
using System.Threading.Tasks;
using SkinService.Models;
using SkinService.Compat;
using EFT;
using KmyTarkovApi.Helpers;
using Newtonsoft.Json;

#endif

namespace SkinService.Views
{
    public class SkinServiceView : MonoBehaviour
    {
        [SerializeField] private Transform windowRoot;

        [SerializeField] private Button closeButton;

        [SerializeField] private TMP_Text skinServiceName;

        [SerializeField] private SkinServiceConfig bodySkin;

        [SerializeField] private SkinServiceConfig feetSkin;

        [SerializeField] private SkinServiceConfig headSkin;

        [SerializeField] private SkinServiceConfig handsSkin;

        [SerializeField] private SkinServiceConfig voice;

        [SerializeField] private SkinServiceAction applyPlayerSkin;

        [SerializeField] private SkinServiceAction applyScavPlayerSkin;

        [SerializeField] private SkinServiceConfig otherPlayer;

        [SerializeField] private SkinServiceAction applyOtherPlayerSkin;

        [SerializeField] private SkinServiceAction applyAllOtherPlayerSkin;

        private bool State
        {
            get => windowRoot.gameObject.activeSelf;
            set
            {
                if (State == value)
                    return;

                SkinServicePlugin.Log.LogInfo("Window state -> " + value);

#if !UNITY_EDITOR

                if (value)
                {
                    _windowRect.anchoredPosition = SettingsModel.Instance.KeyDefaultPosition.Value;

                    UpdateSkinService();
                    RestoreSelection();
                }
                else
                {
                    SaveSelection();
                }

#endif

                windowRoot.gameObject.SetActive(value);
            }
        }

        private RectTransform _windowRect;

        private bool _initialized;

#if !UNITY_EDITOR

        private void Awake()
        {
            SkinServicePlugin.Log.LogInfo("SkinServiceView.Awake windowRoot=" + (windowRoot != null));

            _windowRect = windowRoot.GetComponent<RectTransform>();

            closeButton.onClick.AddListener(() => State = false);

            UpdateLocalized();

            bodySkin.Init("Body", () => UpdateSkinIndex(bodySkin, EBodyModelPart.Body));
            feetSkin.Init("Feet", () => UpdateSkinIndex(feetSkin, EBodyModelPart.Feet));
            headSkin.Init("Head", () => UpdateSkinIndex(headSkin, EBodyModelPart.Head));
            handsSkin.Init("Hands", () => UpdateSkinIndex(handsSkin, EBodyModelPart.Hands));
            voice.Init("Voice", () => UpdateVoiceIndex(voice));
            otherPlayer.Init("Other Player");
            applyPlayerSkin.Init("Apply Current Player Skin", () =>
            {
                ChangeSelfSkin(bodySkin.CurrentIndex, feetSkin.CurrentIndex, headSkin.CurrentIndex,
                    handsSkin.CurrentIndex, false);
                ChangeSelfVoice(voice.CurrentIndex, false);
            });
            applyScavPlayerSkin.Init("Apply Scav Player Skin", () =>
            {
                ChangeSelfSkin(bodySkin.CurrentIndex, feetSkin.CurrentIndex, headSkin.CurrentIndex,
                    handsSkin.CurrentIndex, true);
                ChangeSelfVoice(voice.CurrentIndex, true);
            });
            applyOtherPlayerSkin.Init("Apply Other Player Skin",
                () =>
                {
                    var otherPlayerIndex = otherPlayer.CurrentIndex;

                    ChangeOtherSkin(otherPlayerIndex, bodySkin.CurrentIndex, feetSkin.CurrentIndex,
                        headSkin.CurrentIndex,
                        handsSkin.CurrentIndex);
                    ChangeOtherVoice(otherPlayerIndex, voice.CurrentIndex);
                });
            applyAllOtherPlayerSkin.Init("Apply All Other Player Skin",
                () =>
                {
                    ChangeAllOtherSkin(bodySkin.CurrentIndex, feetSkin.CurrentIndex, headSkin.CurrentIndex,
                        handsSkin.CurrentIndex);
                    ChangeAllOtherVoice(voice.CurrentIndex);
                });
        }

        private void Start()
        {
            Initialize();
        }

        private void OnApplicationQuit()
        {
            SaveSelection();
        }

        public void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            SkinServicePlugin.Log.LogInfo("Initialize, activeInHierarchy=" + gameObject.activeInHierarchy);

            var skinServiceModel = SkinServiceModel.Instance;

            skinServiceModel.UpdateSkinService = UpdateSkinService;
            skinServiceModel.UpdateOtherPlayer = () =>
            {
                var oldIndex = otherPlayer.CurrentIndex;

                otherPlayer.UpdateDropdown(skinServiceModel.OtherPlayerSkinList.Select(x => x.Nickname).ToList());

                otherPlayer.CurrentIndex = oldIndex;
            };
            skinServiceModel.ClearOtherPlayer = otherPlayer.ClearDropdown;

            skinServiceModel.OpenSkinServiceView = () =>
            {
                State = false;
                State = true;
            };

            Helpers.LocalizedHelper.Instance.LanguageChange += UpdateLocalized;
        }

        public void Toggle()
        {
            SkinServicePlugin.Log.LogInfo("Toggle called, activeInHierarchy=" + gameObject.activeInHierarchy +
                                          ", State=" + State);

            if (!gameObject.activeInHierarchy)
            {
                SkinServicePlugin.Log.LogWarning("View is inactive, activating UI root");

                var publicRoot = SkinServiceModel.Instance.SkinServicePublic;

                if (publicRoot != null)
                    publicRoot.SetActive(true);

                gameObject.SetActive(true);
            }

            State = !State;
        }

        private void UpdateLocalized()
        {
            skinServiceName.text = Helpers.LocalizedHelper.Instance.Localized("SkinService");
        }

        private void UpdateSkinService()
        {
            SkinServiceModel.Instance.LoadCustomNames();
            SkinServiceModel.Instance.LoadServerVoices();

            UpdateSkin(bodySkin, EBodyModelPart.Body);
            UpdateSkin(feetSkin, EBodyModelPart.Feet);
            UpdateSkin(headSkin, EBodyModelPart.Head);
            UpdateSkin(handsSkin, EBodyModelPart.Hands);
            UpdateVoice(voice);
        }

        private void SaveSelection()
        {
            var settings = SettingsModel.Instance;

            settings.KeyLastBodyIndex.Value = bodySkin.CurrentIndex;
            settings.KeyLastFeetIndex.Value = feetSkin.CurrentIndex;
            settings.KeyLastHeadIndex.Value = headSkin.CurrentIndex;
            settings.KeyLastHandsIndex.Value = handsSkin.CurrentIndex;
            settings.KeyLastVoiceIndex.Value = voice.CurrentIndex;
            settings.KeyLastOtherPlayerIndex.Value = otherPlayer.CurrentIndex;
        }

        private void RestoreSelection()
        {
            var settings = SettingsModel.Instance;
            var model = SkinServiceModel.Instance;

            ApplySavedIndex(bodySkin, settings.KeyLastBodyIndex.Value, model.BodySkins?.Length ?? 0);
            ApplySavedIndex(feetSkin, settings.KeyLastFeetIndex.Value, model.FeetSkins?.Length ?? 0);
            ApplySavedIndex(headSkin, settings.KeyLastHeadIndex.Value, model.HeadSkins?.Length ?? 0);
            ApplySavedIndex(handsSkin, settings.KeyLastHandsIndex.Value, model.HandsSkins?.Length ?? 0);
            ApplySavedIndex(voice, settings.KeyLastVoiceIndex.Value, model.Voices?.Length ?? 0);
            ApplySavedIndex(otherPlayer, settings.KeyLastOtherPlayerIndex.Value,
                model.OtherPlayerSkinList.Count);
        }

        private static void ApplySavedIndex(SkinServiceConfig config, int index, int optionCount)
        {
            if (index >= 0 && index < optionCount)
                config.CurrentIndex = index;
        }

        private static void ChangeAllOtherSkin(int bodySkinIndex, int feetSkinIndex,
            int headSkinIndex, int handsSkinIndex)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            if (!skinServiceModel.TryGetFullSkinModel(bodySkinIndex, feetSkinIndex, headSkinIndex, handsSkinIndex,
                    out var bodySkin, out var feetSkin, out var headSkin, out var handsSkin))
                return;

            foreach (var playerSkinModel in skinServiceModel.OtherPlayerSkinList)
            {
                InitSkin(playerSkinModel, bodySkin, feetSkin, headSkin, handsSkin);
            }
        }

        private static void ChangeOtherSkin(int otherPlayerIndex, int bodySkinIndex, int feetSkinIndex,
            int headSkinIndex, int handsSkinIndex)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            if (skinServiceModel.TryGetFullSkinModel(bodySkinIndex, feetSkinIndex, headSkinIndex, handsSkinIndex,
                    out var bodySkin, out var feetSkin, out var headSkin, out var handsSkin) &&
                skinServiceModel.TryGetOtherPlayerSkinModel(otherPlayerIndex, out var playerSkinModel))
            {
                InitSkin(playerSkinModel, bodySkin, feetSkin, headSkin, handsSkin);
            }
        }

        private static void ChangeSelfSkin(int bodySkinIndex, int feetSkinIndex, int headSkinIndex, int handsSkinIndex,
            bool isScav)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            if (!skinServiceModel.TryGetFullSkinModel(bodySkinIndex, feetSkinIndex, headSkinIndex, handsSkinIndex,
                    out var bodySkin, out var feetSkin, out var headSkin, out var handsSkin))
                return;

            RequestSkin(bodySkin.Id, feetSkin.Id, headSkin.Id, handsSkin.Id, isScav);
            InitSkin(isScav ? skinServiceModel.ScavPlayerSkin : skinServiceModel.CurrentPlayerSkin, bodySkin,
                feetSkin, headSkin, handsSkin);
        }

        private static void RequestSkin(object bodySkinId, object feetSkinId,
            object headSkinId, object handsSkinId, bool isScav)
        {
            Compat.RequestHandlerHelper.PutJson(isScav ? "/skin-service/scav/change" : "/skin-service/pmc/change",
                JsonConvert.SerializeObject(new
                {
                    bodyId = bodySkinId,
                    feetId = feetSkinId,
                    headId = headSkinId,
                    handsId = handsSkinId
                }));
        }

        private static async void InitSkin(PlayerSkinModel playerSkinModel, SkinModel bodySkinModel,
            SkinModel feetSkinModel,
            SkinModel headSkinModel, SkinModel handsSkinModel)
        {
            playerSkinModel.Customization[EBodyModelPart.Body] = bodySkinModel.Id;
            playerSkinModel.Customization[EBodyModelPart.Feet] = feetSkinModel.Id;
            playerSkinModel.Customization[EBodyModelPart.Head] = headSkinModel.Id;
            playerSkinModel.Customization[EBodyModelPart.Hands] = handsSkinModel.Id;

            if (playerSkinModel.Player == null)
                return;

            var resourceKeyList = new List<ResourceKey>
            {
                bodySkinModel.Prefab,
                feetSkinModel.Prefab,
                headSkinModel.Prefab,
                handsSkinModel.Prefab
            };

            if (!string.IsNullOrEmpty(handsSkinModel.WatchPrefab.path))
            {
                resourceKeyList.Add(handsSkinModel.WatchPrefab);
            }

            await LoadBundlesAndCreatePools(resourceKeyList.ToArray());

            await ReflectionModel.Instance.PlayerBodyInit(playerSkinModel.PlayerBody, playerSkinModel.Customization,
                playerSkinModel.Equipment, playerSkinModel.ItemInHands, LayerMask.NameToLayer("Player"),
                playerSkinModel.Side);

            playerSkinModel.PlayerBody.UpdatePlayerRenders(playerSkinModel.PointOfView, playerSkinModel.Side);
        }

        private static void ChangeOtherVoice(int otherPlayerIndex, int voiceIndex)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            if (skinServiceModel.TryGetVoiceModel(voiceIndex, out var voiceModel) &&
                skinServiceModel.TryGetOtherPlayerSkinModel(otherPlayerIndex, out var playerSkinModel))
            {
                InitVoice(playerSkinModel, voiceModel);
            }
        }

        private static void ChangeAllOtherVoice(int voiceIndex)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            if (!skinServiceModel.TryGetVoiceModel(voiceIndex, out var voiceModel))
                return;

            foreach (var playerSkinModel in skinServiceModel.OtherPlayerSkinList)
            {
                InitVoice(playerSkinModel, voiceModel);
            }
        }

        private static void ChangeSelfVoice(int voiceIndex, bool isScav)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            if (!skinServiceModel.TryGetVoiceModel(voiceIndex, out var voiceModel))
                return;

            RequestVoice(voiceModel.Id, isScav);
            InitVoice(isScav ? skinServiceModel.ScavPlayerSkin : skinServiceModel.CurrentPlayerSkin, voiceModel);
        }

        private static void RequestVoice(object voice, bool isScav)
        {
            Compat.RequestHandlerHelper.PutJson(isScav ? "/skin-service/scav/voice/change" : "/skin-service/pmc/voice/change",
                JsonConvert.SerializeObject(new
                {
                    voiceId = voice
                }));
        }

        private static async void InitVoice(PlayerSkinModel playerSkinModel, VoiceModel voiceModel)
        {
            // SPT 4.1.6 stores the voice in the customization dictionary (EBodyModelPart.Voice).
            playerSkinModel.Customization[EBodyModelPart.Voice] = voiceModel.Id;

            if (playerSkinModel.Player == null)
                return;

            await LoadBundlesAndCreatePools(new[] { voiceModel.VoicePrefab });

            SpeakerHelper.Init(playerSkinModel.Player.Speaker, playerSkinModel.Side, playerSkinModel.PlayerId,
                voiceModel.Name);
        }

        private static Task LoadBundlesAndCreatePools(ResourceKey[] resources)
        {
            return PoolManagerClassHelper.Instance.LoadBundlesAndCreatePools(PoolManagerClassHelper.Instance.PoolManagerClass,
                ObjectsFactory.PoolsCategory.Raid, ObjectsFactory.AssemblyType.Local, resources,
                PoolManagerClassHelper.JobPriorityHelper.Low);
        }

        private static void UpdateSkin(SkinServiceConfig config, EBodyModelPart bodyPart)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            if (skinServiceModel.TryGetSkinModelArray(bodyPart, out var skinModelArray) &&
                skinServiceModel.TryGetCurrentSkin(bodyPart, out var currentSkin))
            {
                config.UpdateConfig(GetLocalizationNameList(skinModelArray),
                    FindSkinIndex(skinModelArray, currentSkin));
            }
        }

        private static void UpdateVoice(SkinServiceConfig config)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            if (skinServiceModel.TryGetVoiceModelArray(out var voiceModelArray) &&
                skinServiceModel.TryGetCurrentVoice(out var currentVoice))
            {
                config.UpdateConfig(GetLocalizationNameList(voiceModelArray),
                    FindVoiceIndex(voiceModelArray, currentVoice));
            }
        }

        private static void UpdateSkinIndex(SkinServiceConfig config, EBodyModelPart bodyPart)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            if (skinServiceModel.TryGetSkinModelArray(bodyPart, out var skinModelArray) &&
                skinServiceModel.TryGetCurrentSkin(bodyPart, out var currentSkin))
            {
                config.CurrentIndex = FindSkinIndex(skinModelArray, currentSkin);
            }
        }

        private static void UpdateVoiceIndex(SkinServiceConfig config)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            if (skinServiceModel.TryGetVoiceModelArray(out var voiceModelArray) &&
                skinServiceModel.TryGetCurrentVoice(out var currentVoice))
            {
                config.CurrentIndex = FindVoiceIndex(voiceModelArray, currentVoice);
            }
        }

        private static int FindSkinIndex(IEnumerable<SkinModel> skinModelEnumerable, object id)
        {
            return FindIndex(skinModelEnumerable, x => x.Id.Equals(id));
        }

        private static int FindVoiceIndex(IEnumerable<VoiceModel> skinModelEnumerable, object id)
        {
            return FindIndex(skinModelEnumerable, x => x.Id.Equals(id));
        }

        private static int FindIndex(IEnumerable<SkinModel> skinModelEnumerable, Func<SkinModel, bool> skinModelFunc)
        {
            var index = -1;

            foreach (var skinModel in skinModelEnumerable)
            {
                index++;

                if (skinModelFunc(skinModel))
                    return index;
            }

            return -1;
        }

        private static List<string> GetLocalizationNameList(IEnumerable<SkinModel> skinModelEnumerable)
        {
            return skinModelEnumerable.Select(x => x.LocalizationName).ToList();
        }

#endif
    }
}