# SkinService

Change your own and other players' skins and voices in SPT.

Ported to **SPT 4.1.6 (SPTushonka 4.1.6 / EFT 0.16.9.5.40743)**.

## Version

- **SPT:** 4.1.6
- **Client API:** KmyTarkovApi 2.0.0 (SPT 4.1 port, included in `Release/`)
- **Mod version:** 1.6.0

## Dependencies

- BepInEx 5 (bundled with SPT 4.1.6)
- [KmyTarkovApi](https://github.com/kmyuhkyuk/KmyTarkovApi) 2.0.0 - included, required
- .NET 10 runtime (bundled with the SPT 4.1.6 server)

Do **not** install the old EFTApi / KmyTarkovApi 1.x builds next to this version.

## Installation

Copy the contents of `Release/` into the game folder:

```
BepInEx/plugins/kmyuhkyuk-SkinService       (client plugin + UI bundle)
BepInEx/plugins/kmyuhkyuk-KmyTarkovApi      (required API + settings UI)
SPT_Runtime/user/mods/kmyuhkyuk-SkinService (server mod)
```

The default shortcut is unbound; set it in the KmyTarkovApi settings (F12) or open
the window from the settings page.

## Building

### Requirements

- Visual Studio 2022 (MSBuild, .NET Framework 4.7.2 targeting pack) for the client plugin
- .NET 10 SDK for the server mod
- The SPT 4.1.6 client assemblies in `References/EFT/` (already committed)

### Build

Run `build_release.bat`. It builds:

1. `SkinService/SkinService.csproj` - BepInEx client plugin
2. `Build/SkinService_ServerMod_CSharp/SkinService_ServerMod.csproj` - SPT server mod

and assembles the game-ready folder in `Release/`.

`References/KmyTarkov*.dll` are the prebuilt KmyTarkovApi 2.0 assemblies (built from the
SPT 4.1 branch of [KmyTarkovApi](https://github.com/kmyuhkyuk/KmyTarkovApi)). Rebuild them
against a newer client by building the four KmyTarkov projects and copying the DLLs into
`References/` (and `Output/KmyTarkovApi_Plugin/`).

## What changed in the 4.1.6 port

Client:

- KmyTarkovApi 2.0 replaced EFTApi/KmyTarkovApi 1.5 (the SPT 4.1 port of the API).
- `CustomizationSolver` skin/voice templates are read from `ClothingTemplates` and the
  private `_voices` dictionary (the 3.x `Templates`/`Voices` members no longer exist).
- Voices are now stored in `Profile.Customization[EBodyModelPart.Voice]` (MongoID) instead
  of `Profile.Info.Voice` (string).
- Voice prefab paths come from the game's `InGameBundles.TakePhrasePath`.
- Speaker initialization uses `BaseSpeaker.Init`.
- Server requests go through `SPT.Common.Http.RequestHandler` (same as the game's SPT client).
- `PlayerBody.Init` uses the 8-parameter 4.x overload.
- Profile/session access uses `IEftSession.Profile` / `IEftSession.ProfileOfPet`.
- Clothing names use the game localization table, with server-provided names for modded
  clothing that has no locale entry.

Server:

- Replaced the SPT 3.x TypeScript mod with a C# SPT 4.1.6 server mod
  (`DynamicRouter` + `ProfileHelper` + `SaveServer`).
- Routes: `/skin-service/pmc/change`, `/skin-service/pmc/voice/change`,
  `/skin-service/scav/change`, `/skin-service/scav/voice/change`.
- `/skin-service/names` returns display names from the live
  `TemplateTable.Customization` dictionary, including clothing added by other server mods.
- Writes Body/Feet/Head/Hands/Voice into the saved profile customization and persists it.

See [PORT_NOTES.md](PORT_NOTES.md) for the detailed API mapping.

## License

See the [LICENSE](LICENSE) file.
