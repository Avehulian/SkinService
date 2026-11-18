#if !UNITY_EDITOR

using System.Collections;
using EFT;
using EFT.InventoryLogic;
using KmyTarkovReflection;

namespace SkinService.Models
{
    internal class PlayerSkinModel
    {
        public Player Player;

        public PlayerBody PlayerBody => Player.PlayerBody;

        public readonly Profile Profile;

        public ProfileInfo InfoClass => Profile.Info;

        public IDictionary Customization =>
            ReflectionModel.Instance.RefCustomization.GetValue(Profile);

        public string Nickname => Profile.Nickname;

        public object Inventory => RefInventory.GetValue(Profile);

        public object Equipment => RefEquipment.GetValue(Inventory);

        public object ItemInHands => ReflectionModel.Instance.RefItemInHands.GetValue(Player);

        public EPlayerSide Side => InfoClass.Side;

        public int PlayerId => Player.PlayerId;

        public EPointOfView PointOfView => Player.PointOfView;

        // Reflection helpers for Inventory and Equipment (replacing removed _InventoryHelper)
        private static readonly RefHelper.FieldRef<Profile, object> RefInventory =
            RefHelper.FieldRef<Profile, object>.Create("Inventory");

        private static readonly RefHelper.FieldRef<object, object> RefEquipment =
            RefHelper.FieldRef<object, object>.Create(typeof(EFT.InventoryLogic.Inventory), "Equipment");

        public PlayerSkinModel(Profile profile)
        {
            Profile = profile;
        }

        public PlayerSkinModel(Player player)
        {
            Player = player;
            Profile = player.Profile;
        }
    }
}

#endif
