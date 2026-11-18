# SkinService — SPT 4.1.6 port notes

Ported from SPT 3.10 (EFTApi 1.3 / TypeScript server mod) to **SPT 4.1.6**
(BepInEx 5 / Mono client, C# .NET 10 server).

## Reference setup

| Purpose | Path |
|---|---|
| Client reference assemblies | `References/EFT/` (from the SPT 4.1.6 `Managed` folder) |
| KmyTarkovApi 2.0 binaries | `References/KmyTarkov*.dll` |
| Server reference assemblies | `Build/SkinService_ServerMod_CSharp/refs/` (`SPTarkov.Server.Core` 4.1.6.0) |

## Client API mapping (3.10 → 4.1.6)

| 3.10 (EFTApi) | 4.1.6 (KmyTarkovApi 2.0) |
|---|---|
| `EFTApi.EFTHelpers` | `KmyTarkovApi.Helpers.*` (`SessionHelper`, `PlayerHelper`, `GameWorldHelper`, `PoolManagerClassHelper`) |
| `EFTReflection.RefHelper` | `KmyTarkovReflection.RefHelper` |
| `EFTUtils.CustomLocalized` | `KmyTarkovUtils.CustomLocalized` |
| `SessionHelper.Profile` / `ProfileOfPet` | `SessionHelper.Session.Profile` / `.ProfileOfPet` (`IEftSession : IProfileSession`) |
| `PlayerBody.Init(..., BindableState<Item>, ...)` | 8-parameter `PlayerBody.Init(BodyCustomization, InventoryEquipment, BindableState<Item>, int, EPlayerSide, string, Dictionary<EquipmentSlot, Transform>, bool)` |
| `PoolManagerClass` | `ObjectsFactory` (`PoolsCategory.Raid`, `AssemblyType.Local`) |
| `Player.Speaker` + `Speaker.Init(side, id, voice, register)` | unchanged type/signature: `EFT.BaseSpeaker.Init` |
| `Profile.Info.Voice` (string) | `Profile.Customization[EBodyModelPart.Voice]` (MongoID) |
| `CustomizationClass.Templates` | `CustomizationSolver.ClothingTemplates` (`CustomizationClothing[]`) |
| `CustomizationClass.Voices` | `CustomizationSolver._voices` (`Dictionary<MongoID, CustomizationPlayerVoice>`) |
| `_SpeakerHelper` / `_RequestHandlerHelper` | `Compat/SpeakerHelper.cs`, `Compat/RequestHandlerHelper.cs` |

### Customization model (4.1.6)

- `Profile.Customization` is `EFT.BodyCustomization : Dictionary<EBodyModelPart, MongoID>`.
- `EBodyModelPart` includes `Body`, `Feet`, `Head`, `Hands`, `DogTag`, **`Voice`**.
- Clothing templates (`EFT.Customization.CustomizationClothing`) carry `BodyPart`, `Prefab`,
  `WatchPrefab`, plus the inherited `Id`/`Name`/`NameLocalizationKey` members.
  `ClothingTemplates` only contains body/feet/hands; heads live in the separate
  `_headTemplates` dictionary, so the mod merges both.
- Voice templates (`EFT.Customization.CustomizationPlayerVoice`) carry `Id`/`Name`;
  the prefab path is resolved by `EFT.InGameBundles.TakePhrasePath(name)`.

### Display names

`NameLocalizationKey` is generated as `"{id} Name"` by the game, so it must be resolved with the
game localization table via `EFT.LocalizationExtensions.Localized(key, null)`, not the mod's own
`localized/*.json` UI strings. When a key is missing (several hands/voices have no localized
name), `SkinModel.LocalizationName` falls back to a server-provided name, then to the template's
`Name` field (for example `usec_hands_aggressor`), then to the bundle file name, prettified for
display.

Clothing added by other **server** mods usually has no client locale entry. The server mod
exposes `GET /skin-service/names`, which returns `{ "<item id>": "Display name" }` for every
entry in the live `TemplateTable.Customization` dictionary (so items added by any mod are
included). The client caches this map and uses it after game localization fails.

WTT-ServerCommonLib custom clothing stores locale keys (for example `<id>_name`) in
`Properties.Name` and only adds a localized name for the outfit/suite id (`<suiteId> Name`),
so the item ids themselves have no usable locale entry. The server therefore also scans every
mod's `db/CustomClothing/*.json` and maps `topId`, `handsId`, `bottomId`, `feetId`, `suiteId`
and `outfitId` to the config's localized outfit name. If no config/name is found, it resolves
the item's locale keys through `LocaleService.GetLocaleDb(...)`, then falls back to the bundle
file name.

### Voices

`GET /skin-service/voices` returns every voice item in `TemplateTable.Customization` (parent
`5fc100cf95572123ae738483`), including bot-only/modded voices. The client fetches this once and
merges any missing ids into `SkinServiceModel.Voices`, so voices that the client's
`CustomizationSolver` does not surface are still selectable. Voice ids are converted back to
`MongoID` values so they match the profile customization dictionary.

### KmyTarkovApi compatibility

SkinService now ships the supplied `kmyuhkyuk-KmyTarkovApi` 2.0 build (same assembly version,
plugin GUID and public API). That build's `SessionHelper.CreateBackend` predicate was written
for 4.1.0: on 4.1.6 the matching IL moved into the compiler-generated
`CG_GetGlobalConfigAndClientSettingsConfig.MoveNext` state machine, so the helper can fail to
construct. SkinService therefore no longer calls `SessionHelper`, `PlayerHelper` or
`GameWorldHelper`:

- It creates its own `RefHelper.HookRef`s for `TarkovApplication.OnApplicationLoaded`,
  `Player.Init` and `GameWorld.Dispose`.
- The session/profile is read directly from
  `TarkovApplication.GetClientBackEndSession()` (`IEftSession : IProfileSession`).
- Only `KmyTarkovReflection`, `KmyTarkovUtils` and `PoolManagerClassHelper` are used from the
  API build.

This makes SkinService work with the bundled build as well as a patched/rebuilt one. For other
mods that need `SessionHelper` on 4.1.6, `KmyTarkovApi-4.1.6.patch` contains the optional
SessionHelper/PlayerHelper fixes.

Verified lookups against the 4.1.6 `Assembly-CSharp.dll`:

- `EFT.CustomizationSolver.GetAnyCustomizationItem(MongoID)` / `ClothingTemplates` /
  `_headTemplates` / `_voices`
- `EFT.PlayerBody.Init` 8-parameter overload
- `EFT.Player._itemInHands`, `EFT.Player.Speaker`
- `EFT.BaseSpeaker.Init(EPlayerSide, int, string, bool)`
- `EFT.InGameBundles.TakePhrasePath(string)`
- `EFT.Profile.Customization`, `EFT.Profile.Info`
- `EFT.ObjectsFactory.LoadBundlesAndCreatePools(...)` 6-parameter overload
- `EFT.TarkovApplication.OnApplicationLoaded`, `EFT.Player.Init`, `EFT.GameWorld.Dispose`

### UI initialization in 4.1.6

Unity did not invoke `Start` on the `SkinServiceView` component created from the asset bundle
during the plugin's `Awake` (the prefab's `Awake` did run, but `Start` never fired). The mod no
longer relies on `Start`:

- `SkinServiceModel.CreateUi()` instantiates the prefab and calls `SkinServiceView.Initialize()`
  immediately; it can also recreate the UI if the object was destroyed.
- The F8 shortcut is checked in `SkinServicePlugin.Update()` and calls `SkinServiceView.Toggle()`
  directly.
- `Initialize()` is guarded with a flag, so a later Unity `Start` call cannot wire the view twice.
- Dropdown selections are saved when the window closes / the game quits in hidden config
  entries (`Last Body Index`, `Last Feet Index`, `Last Head Index`, `Last Hands Index`,
  `Last Voice Index`, `Last Other Player Index`) and restored the next time the window opens.

## Server mod (SPT 4.1.6)

SPT 4.0+ server mods are C# assemblies loaded from `SPT_Runtime/user/mods/<folder>/`.
There is no `package.json`; metadata comes from an `IModMetadata` implementation in the
assembly. The mod targets `net10.0` and references the 4.1.6 server assemblies directly.

Implementation:

- `ModMetadata` (`com.kmyuhkyuk.SkinService`, version 1.6.0, SPT range `~4.1.6`).
- `SkinServiceDynamicRouter : DynamicRouter` registered at
  `OnLoadOrder.Routers + 1`.
- `SkinServiceCallbacks` uses `ProfileHelper.GetPmcProfile` / `GetScavProfile` and
  `SaveServer.SaveProfileAsync` to persist `Customization.Body/Feet/Head/Hands/Voice`.
- Route actions are `RouteAction<SkinRequestData>` / `RouteAction<VoiceRequestData>`
  (typed `IRequestData` bodies, 4.1 signature with `CancellationToken`).

## Packaging

`build_release.bat` produces:

```
Release/
  BepInEx/plugins/kmyuhkyuk-SkinService/
      SkinService.dll
      bundles/skinservice.bundle     (prebuilt UI bundle from release 1.3.2)
      localized/{en,zh}.json
  BepInEx/plugins/kmyuhkyuk-KmyTarkovApi/
      KmyTarkovApi.dll
      KmyTarkovReflection.dll
      KmyTarkovUtils.dll
      KmyTarkovConfiguration.dll
      Crc32.NET.dll
      HtmlAgilityPack.dll
      bundles/kmytarkovconfiguration.bundle
      localized/{en,zh}.json
  SPT_Runtime/user/mods/kmyuhkyuk-SkinService/
      SkinService.dll
```

The UI asset bundle is not built from this repository; it is the bundle shipped with the
last public release (1.3.2) and is kept in `bundles/skinservice.bundle`.
