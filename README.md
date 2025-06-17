# FPP Song Info

### Description

This code takes information about the current song being played within FPP from the MQTT published information and writes it to a file that can be consumed from 
something like a RDS receiver. In my case TinyRDS reads the song info from a file, so this produces the file with that data. Updates when a new message is posted to the queue.

### Build

Build at the command line as a single file exe.

dotnet publish -c Release -p:PublishSingleFile=True

### Deploy

Deploy as a windows service. - Use branch windows-service

From admin powershell where binpath is the location of the service exe.

sc.exe create "FPP Song Info" binpath= "c:\Services\FppSongInfo\FppSongInfo.exe" start= delayed-auto
