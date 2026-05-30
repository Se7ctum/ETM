# ETM

EVE Thumbnail Manager is a Windows overlay utility for managing multiple EVE Online clients with live DWM thumbnails, click-to-focus switching, hotkey cycling, profiles, and configurable thumbnail appearance.

## Status

ETM is an early public build. It is usable, but still being actively tested and refined.

## Requirements

- Windows
- .NET 8 Desktop Runtime
- EVE Online clients running as `exefile.exe`

## Build

```powershell
dotnet build .\src\ETM.sln
```

## Publish

```powershell
dotnet publish .\src\ETM\ETM.csproj -c Release -o .\publish
```

## Notes

The published executable is currently unsigned. Windows Smart App Control or SmartScreen may warn about or block unsigned builds.

## License

MIT
