# FPP Song Info

FPP Song Info consumes MQTT song metadata from FPP and
[Radio Automation](https://github.com/jeffu231/Radio-Automation), then writes
`CurrentSong.txt` for downstream consumers such as TinyRDS. Radio Automation
publishes the `songinfo` message consumed by this service.

It can also update a **Pira MiniRDS encoder using the MRDS192 chip** directly.
This RDS feature exclusively supports MiniRDS/MRDS192 over its bidirectional
COM control-line interface. It does **not** support MRDS1322 devices or their
UART command framing. It was tested specifically with the EDM-LCD-RDS FM
transmitter.

## Build

```powershell
dotnet publish FPPSongInfo/FPPSongInfo.csproj -p:PublishProfile=FolderProfile.pubxml
```

## Deploy as a Windows service

Deploy the published files, then create the service from an elevated
PowerShell session:

```powershell
sc.exe create "FPP Song Info" binpath= "C:\Services\FppSongInfo\FppSongInfo.exe" start= delayed-auto
```

## Configuration

Committed `appsettings.json` contains safe examples only and has MQTT disabled.
Supply deployment-specific values in service configuration—preferably via
machine environment variables, a deployment-managed `appsettings.json`, or an
environment-specific configuration file. Never commit broker hostnames, topic
roots, or credentials.

Environment-variable names use double underscores (`__`) in place of JSON
section separators. Set the values before starting the service, then restart it
after any change.

| Environment variable | Purpose |
| --- | --- |
| `Mqtt__Enabled` | Set to `true` to start MQTT consumers. |
| `Mqtt__Broker` | MQTT broker hostname or address. |
| `Mqtt__Port` | MQTT broker TCP port. |
| `Mqtt__RootTopic` | Deployment-specific root topic. |
| `Mqtt__ClientId` | MQTT client identifier. |
| `FPP__SongTopic` | FPP song metadata topic fragment. |
| `RadioAutomation__SongTopic` | Radio automation `songinfo` topic fragment. |
| `Output__FilePath` | Directory containing `CurrentSong.txt`. |
| `Output__FileName` | Name of the output file. |
| `Output__Enabled` | Set to `false` to disable file output. Defaults to `true`. |

For example, set a machine-level value with PowerShell:

```powershell
[Environment]::SetEnvironmentVariable("Mqtt__Broker", "mqtt.example.net", "Machine")
[Environment]::SetEnvironmentVariable("Mqtt__Enabled", "true", "Machine")
```

The service validates configuration at startup. When MQTT is enabled, broker,
port, root topic, client ID, topic fragments, and output settings must all be
valid before it starts processing messages.

## Direct MiniRDS/MRDS192 output

RDS is disabled by default. When enabled, the service publishes the configured
static and dynamic Program Service values, and sends complete song metadata as
RadioText in the format `Artist - Title`. RadioText immediately returns to the
current song after a song change, then rotates through configured supplemental
messages.

MiniRDS does not use ordinary serial/UART payloads. Its bidirectional COM cable
emulates I²C with DTR as SDA output, CTS as SDA input, and RTS as SCL. Close
TinyRDS before starting this service because only one application can own the
COM port at a time.

Example configuration for the deployed FTDI adapter:

```json
"Rds": {
  "Enabled": true,
  "PortName": "COM3",
  "Slow": false,
  "ControlLineTransport": "Win32",
  "DtrEnabledIsSdaHigh": true,
  "RtsEnabledIsSclHigh": true,
  "CtsHighIsSdaHigh": true,
  "StaticProgramService": "LTSHOW",
  "DynamicProgramService": "Compound Radio",
  "DynamicPsMode": 3,
  "LabelPeriod": "00:00:02.500",
  "LoopDelay": "00:00:05.400",
  "RadioTextRotationInterval": "00:00:15",
  "AdditionalRadioTextMessages": [
    "lightshow.onthecompound.org"
  ]
}
```

`Win32` uses direct Windows COM control-line calls and is recommended for the
tested FTDI adapter. `SerialPort` selects the .NET `System.IO.Ports`
implementation for comparison or fallback. `Slow` selects a conservative I²C
timing profile; the COM baud rate does not clock the MRDS192 bus.

The RDS implementation updates volatile MRDS192 registers only. It never sends
the EEPROM persistence command, so service restarts and configuration updates
do not wear the encoder's EEPROM.
