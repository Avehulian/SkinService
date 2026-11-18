#if !UNITY_EDITOR

using BepInEx;
using SkinService.Attributes;
using SkinService.Models;

namespace SkinService
{
    [BepInPlugin("com.kmyuhkyuk.SkinService", "SkinService", "1.6.0")]
    [BepInDependency("com.dustinmoon78.kmytarkovapi", "2.0.0")]
    [EFTConfigurationPluginAttributes("https://hub.sp-tarkov.com/files/file/781-skin-service")]
    public partial class SkinServicePlugin : BaseUnityPlugin
    {
        internal static readonly BepInEx.Logging.ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource("SkinService");

        private void Awake()
        {
            Log.LogInfo("Awake");

            SettingsModel.Create(Config);

            SkinServiceModel.Create();
        }

        private void Start()
        {
            try
            {
                var reflectionModel = ReflectionModel.Instance;

                reflectionModel.CustomizationClassConstructor.Add(this, nameof(CustomizationClassConstructor));
                reflectionModel.AfterApplicationLoaded.Add(this, nameof(AfterApplicationLoaded));
                reflectionModel.PlayerInit.Add(this, nameof(PlayerInit));
                reflectionModel.GameWorldDispose.Add(this, nameof(GameWorldDispose));

                Log.LogInfo("Hooks registered");
            }
            catch (System.Exception ex)
            {
                Log.LogError("Start failed: " + ex);
            }
        }

        private void Update()
        {
            if (SettingsModel.Instance.KeySkinServiceShortcut.Value.IsDown())
            {
                Log.LogInfo("Shortcut pressed");

                var model = SkinServiceModel.Instance;
                var view = model.UiView;

                if (view == null)
                {
                    Log.LogWarning("UI view missing, recreating");

                    model.CreateUi();
                    view = model.UiView;
                }

                if (view != null)
                {
                    view.Toggle();
                }
                else
                {
                    model.OpenSkinServiceView?.Invoke();
                }
            }
        }
    }
}

#endif
