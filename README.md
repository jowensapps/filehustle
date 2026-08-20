# FileHustle (Windows)

A LAN-only file transfer app — send files and folders to nearby devices
over WiFi, no Bluetooth, no AirDrop, no cloud, no accounts. Devices
discover each other automatically via mDNS and transfer over a direct TCP
connection to another device running FileHustle on the same network.

This repo contains the Windows client, built with [Avalonia UI](https://avaloniaui.net/)
(.NET). See [`docs/protocol.md`](docs/protocol.md) for the wire protocol
this client implements.

## Download

The easiest way to get FileHustle is to grab the prebuilt executable from
the [Releases page](https://github.com/jowensapps/filehustle/releases) —
download `FileHustle.exe` and run it, no installation needed.

> **Note:** Windows may show a SmartScreen warning ("Windows protected
> your PC") since the exe isn't code-signed. Click **More info** → **Run
> anyway** to proceed.

## Building and running

If you'd rather build from source instead of using the Releases download:

1. Install the [.NET SDK](https://dotnet.microsoft.com/download) (this
   project targets **.NET 10**).
2. Get the code:
   ```
   git clone https://github.com/jowensapps/filehustle.git
   ```
   or use **Code → Download ZIP** on the GitHub repo page if git isn't
   available.
3. From the `Windows/` folder, publish a self-contained single-file
   executable:
   ```
   dotnet publish -r win-x64 -c Release --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
   ```
   The `.exe` lands in `bin/Release/net10.0/win-x64/publish/FileHustle.exe`.
   Copy that whole `publish` folder anywhere and run it — no installer needed.

For local development instead of a published build, `dotnet run` from the
`Windows/` folder works too.

## First-run notes

- **Windows Firewall** will prompt to allow network access on first launch —
  click **Allow**, or discovery and transfers won't work (both need LAN
  access).
- Some networks (corporate WiFi, guest networks) enable **client
  isolation**, which blocks device-to-device traffic outright. If no
  devices show up in discovery, this is a network policy issue with no
  app-side fix — try a different network (e.g. a home WiFi or a personal
  hotspot).
- When you receive a file, FileHustle will prompt you to choose where to
  save it rather than opening it automatically.

## Support

Questions, bugs, or feature requests — see [`SUPPORT.md`](SUPPORT.md) or
email [j.owens.apps@gmail.com](mailto:j.owens.apps@gmail.com).

## Privacy

FileHustle collects no data — see [`PRIVACY.md`](PRIVACY.md).

## License

GPL-3.0 — see [`LICENSE`](LICENSE).
