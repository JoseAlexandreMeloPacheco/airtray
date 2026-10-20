# AirTray

English · [Português](README.md)

**Real Windows system tray control for compatible Elgin Air appliances.**

Created by **[José Alexandre Melo Pacheco](https://github.com/JoseAlexandreMeloPacheco)** · [Source and feedback](https://github.com/JoseAlexandreMeloPacheco/airtray/issues)

> Independent experimental client. This is not a simulation: it sends real commands using the user's account. It has no manufacturer affiliation or confirmed manufacturer redistribution authorization. Read the [compatibility and third-party notes](THIRD_PARTY.md) before use.

<img src="docs/screenshots/control-off.png" width="300" alt="Powered-off panel"/> <img src="docs/screenshots/control-on.png" width="300" alt="Powered-on panel"/>

Screenshots use synthetic test data, not another person's account or appliance.

## Download and use

1. Download the experimental `AirTray-0.3.0-windows-x64.zip` from [Releases](https://github.com/JoseAlexandreMeloPacheco/airtray/releases) when available.
2. Install **.NET Desktop Runtime 10 x64**, extract the entire ZIP, and open `AirTray.exe` on Windows x64. The executable is not digitally signed.
3. Sign in with **your own Elgin Air account** and choose the account region. It may differ from your country of residence; some Brazilian accounts use Mainland/China.
4. Select an appliance if multiple compatible devices are found. To switch later, use the **Minha conta** context menu.

No shared accounts, passwords, or sample real devices are provided. Your password is stored under your Windows user in Credential Manager, target `ElginTray/ElginAir`. The **Minha conta** context menu can disconnect the session or remove the saved account. Signing in may disconnect the phone app, according to the service's login mode.

## Tested compatibility

- AUX/Elgin product family with an identifier ending in **c0620000**, used by Elgin Air 1.0.1.
- Login, discovery, state reads, a temperature change and restoration were validated with one authorized account and appliance. Other users and variants still need testing.
- Compatibility with all Elgin models, Elgin Smart, other apps or other brands is not claimed. An unsupported account is reported without arbitrarily selecting a different device.
- Requires internet access and available manufacturer cloud services. Service changes may break the integration.

## Controls

Power, temperature from 16 to 32°C, mode, fan speed, sleep, vertical swing, economy and display, according to the returned properties. Each adjustment is followed by a read to display the confirmed state. Automatic refresh every 30 seconds, plus manual refresh.

Click the tray icon to open/hide the panel. **×**, **Recolher**, and **Alt+F4** hide it. **Encerrar aplicativo…** in the tray menu requires confirmation and does not turn off the appliance. The **GitHub** button opens this project in your browser; author attribution is visible on the panel.

## Build and verify

Install .NET SDK 10 and run at the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build.ps1 -Verify
.\artifacts\airtray\AirTray.exe
```

You can also use `Abrir AirTray.cmd` after building. Automated tests run offline: they check protocol parsing/validation, artwork, hiding, closing and reopening without accessing credentials or sending commands. They do not replace hardware testing. See [docs/audit.md](docs/audit.md).

To create a distribution ZIP:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Package-AirTray.ps1
```

The framework-dependent package includes documentation and license and is written to `artifacts/release`. No local credentials are copied. Never distribute a ZIP of the entire development folder. `scripts/Prepare-GitHub.ps1` checks the staged file allowlist but does not commit or upload.

## Structure and security

`AirTray/` contains the WPF/tray UI, Windows credential storage, and an independently reconstructed HTTPS client. No APK, binary SDK, decompiled source, or private reports are distributed. Legacy authentication algorithms reproduce the existing protocol; they are not recommendations for new login systems.

Application compatibility constants are present in the client and are distinct from your password. Their presence in the original app does not establish permission for public reuse. See [THIRD_PARTY.md](THIRD_PARTY.md) and [SECURITY.md](SECURITY.md). Do not post passwords, sessions or raw cloud responses in issues.

## License

Code developed for this project is available under [MIT](LICENSE). This license grants no rights to third-party software, trademarks, application licenses or services. AI-generated artwork was supplied by the project owner; see [provenance](AirTray/Assets/README.md).
