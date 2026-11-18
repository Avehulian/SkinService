#if !UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Threading.Tasks;
using EFT;
using HarmonyLib;
using KmyTarkovReflection;
using EFT.InventoryLogic;
using UnityEngine;

namespace SkinService.Models
{
    internal class ReflectionModel
    {
        private static readonly Lazy<ReflectionModel> Lazy = new Lazy<ReflectionModel>(() => new ReflectionModel());

        public static ReflectionModel Instance => Lazy.Value;

        public readonly RefHelper.FieldRef<object, EBodyModelPart> RefBodyPart;

        public readonly RefHelper.FieldRef<object, object> RefId;

        public readonly RefHelper.FieldRef<object, string> RefName;

        public readonly RefHelper.FieldRef<object, ResourceKey> RefPrefab;

        public readonly RefHelper.FieldRef<object, ResourceKey> RefWatchPrefab;

        public readonly RefHelper.FieldRef<Profile, IDictionary> RefCustomization;

        public readonly RefHelper.FieldRef<Player, object> RefItemInHands;

        public readonly RefHelper.PropertyRef<object, object[]> RefTemplates;

        public readonly RefHelper.PropertyRef<object, string> RefNameLocalizationKey;

        public readonly RefHelper.FieldRef<object, IDictionary> RefHeadTemplates;

        public readonly RefHelper.FieldRef<object, IDictionary> RefVoices;

        public readonly RefHelper.HookRef CustomizationClassConstructor;

        public readonly RefHelper.HookRef AfterApplicationLoaded;

        public readonly RefHelper.HookRef PlayerInit;

        public readonly RefHelper.HookRef GameWorldDispose;

        private readonly
            Func<PlayerBody, object, object, object, int, EPlayerSide, string,
                Dictionary<EFT.InventoryLogic.EquipmentSlot, Transform>, bool, Task> _refPlayerBodyInit;

        private ReflectionModel()
        {
            var customizationClassType =
                RefTool.GetEftType(x => x.GetMethod("GetAnyCustomizationItem", RefTool.Public) != null);

            RefTemplates = RefHelper.PropertyRef<object, object[]>.Create(customizationClassType, "ClothingTemplates");

            var templateType = RefTemplates.PropertyType.GetElementType();

            RefBodyPart = RefHelper.FieldRef<object, EBodyModelPart>.Create(templateType, "BodyPart");
            RefId = RefHelper.FieldRef<object, object>.Create(templateType, "Id");
            RefName = RefHelper.FieldRef<object, string>.Create(templateType, "Name");
            RefPrefab = RefHelper.FieldRef<object, ResourceKey>.Create(templateType, "Prefab");
            RefWatchPrefab = RefHelper.FieldRef<object, ResourceKey>.Create(templateType, "WatchPrefab");

            RefNameLocalizationKey = RefHelper.PropertyRef<object, string>.Create(templateType, "NameLocalizationKey");

            RefCustomization = RefHelper.FieldRef<Profile, IDictionary>.Create("Customization");

            // SPT 4.1.6: heads live in their own dictionary and are not part of ClothingTemplates.
            RefHeadTemplates = RefHelper.FieldRef<object, IDictionary>.Create(customizationClassType, "_headTemplates");

            // SPT 4.1.6: voices live in a private dictionary (the old public Voices property is gone).
            RefVoices = RefHelper.FieldRef<object, IDictionary>.Create(customizationClassType, "_voices");

            CustomizationClassConstructor = RefHelper.HookRef.Create(customizationClassType.GetConstructors()[0]);

            // Session/player/game-world hooks are owned by SkinService so it can run with any
            // KmyTarkovApi 2.0 build, including ones whose SessionHelper does not match 4.1.6.
            AfterApplicationLoaded = RefHelper.HookRef.Create(typeof(TarkovApplication),
                x => x.GetParameters().Length == 0 && x.ReturnType == typeof(void) &&
                     x.ReadMethodBody().ContainsIL(OpCodes.Callvirt, AccessTools.Method(typeof(Action), "Invoke")));

            PlayerInit = RefHelper.HookRef.Create(typeof(Player), "Init");
            GameWorldDispose = RefHelper.HookRef.Create(typeof(GameWorld), "Dispose");

            RefItemInHands = RefHelper.FieldRef<Player, object>.Create("_itemInHands");

            // SPT 4.1.6 exposes the 8-parameter Init overload
            _refPlayerBodyInit =
                RefHelper
                    .ObjectMethodDelegate<
                        Func<PlayerBody, object, object, object, int, EPlayerSide, string,
                            Dictionary<EquipmentSlot, Transform>, bool, Task>>(
                        RefTool.GetEftMethod(typeof(PlayerBody), RefTool.Public,
                            x => x.Name == "Init" && x.GetParameters().Length == 8));
        }

        // ReSharper disable once InconsistentNaming
        public Task PlayerBodyInit(PlayerBody instance, object customization, object equipment,
            object itemInHands, int layer, EPlayerSide playerSide, string playerProfileID = "",
            Dictionary<EFT.InventoryLogic.EquipmentSlot, Transform> alternativeBones = null, bool isYourPlayer = false)
        {
            return _refPlayerBodyInit(instance, customization, equipment, itemInHands, layer, playerSide,
                playerProfileID, alternativeBones, isYourPlayer);
        }
    }
}

#endif
