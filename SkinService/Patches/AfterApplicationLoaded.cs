#if !UNITY_EDITOR

using System;
using EFT;
using SkinService.Models;

namespace SkinService
{
    public partial class SkinServicePlugin
    {
        private static void AfterApplicationLoaded(TarkovApplication __instance)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            try
            {
                var session = __instance.GetClientBackEndSession();

                if (session != null)
                {
                    // IEftSession implements IProfileSession in SPT 4.1.6.
                    if (session.Profile != null)
                    {
                        skinServiceModel.PlayerSkin = new PlayerSkinModel(session.Profile);
                    }

                    if (session.ProfileOfPet != null)
                    {
                        skinServiceModel.ScavPlayerSkin = new PlayerSkinModel(session.ProfileOfPet);
                    }
                }

                skinServiceModel.UpdateSkinService?.Invoke();
            }
            catch (Exception ex)
            {
                // Never let a profile error crash the game's application-loaded coroutine.
                SkinServicePlugin.Log.LogError("AfterApplicationLoaded failed: " + ex);
            }
        }
    }
}

#endif
