#if !UNITY_EDITOR

using EFT;
using SkinService.Models;

namespace SkinService
{
    public partial class SkinServicePlugin
    {
        private static void PlayerInit(Player __instance)
        {
            var skinServiceModel = SkinServiceModel.Instance;

            if (__instance.IsYourPlayer)
            {
                skinServiceModel.IsScav = __instance.Side == EPlayerSide.Savage;

                var playerSkin = skinServiceModel.CurrentPlayerSkin;

                // Fall back to the player's own profile if the session profile was not available
                // (e.g. the scav profile had not been loaded when the application finished loading).
                if (playerSkin == null)
                {
                    playerSkin = new PlayerSkinModel(__instance.Profile);

                    if (skinServiceModel.IsScav)
                        skinServiceModel.ScavPlayerSkin = playerSkin;
                    else
                        skinServiceModel.PlayerSkin = playerSkin;
                }

                playerSkin.Player = __instance;
            }
            else
            {
                skinServiceModel.OtherPlayerSkinList.Add(new PlayerSkinModel(__instance));

                skinServiceModel.UpdateOtherPlayer?.Invoke();
            }
        }
    }
}

#endif
