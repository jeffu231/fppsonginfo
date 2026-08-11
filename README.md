# FPP Song Info

FPP Song Info consumes MQTT song metadata from FPP and
[Radio Automation](https://github.com/jeffu231/Radio-Automation), then writes
`CurrentSong.txt` for downstream consumers such as TinyRDS. Radio Automation
publishes the `songinfo` message consumed by this service.

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

For example, set a machine-level value with PowerShell:

```powershell
[Environment]::SetEnvironmentVariable("Mqtt__Broker", "mqtt.example.net", "Machine")
[Environment]::SetEnvironmentVariable("Mqtt__Enabled", "true", "Machine")
```

The service validates configuration at startup. When MQTT is enabled, broker,
port, root topic, client ID, topic fragments, and output settings must all be
valid before it starts processing messages.
