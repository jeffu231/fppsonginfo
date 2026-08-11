# Direct RDS Updating

## Overview

Currently this writes a file to a directory that TinyRDS uses to update the RDS data on a FM transmitter using a MiniRDS (MRDS192) 
chipset. TinyRDS sends commands over a serial port to send the RDS song update data to the radio on intervals. The goal of this project
is to replace the pass through TinyRDS with direct calls over the serial port to send the data for the core functions that TinyRDS 
provides for Dynamic PS and radiotext. 

## Data Types

Dynamic PS
Radio Text

## Current song info should be sent to the RDS device whenever an update comes in from any of the sources.

## References

RDS chip datasheets.
docs/references/mrds192.pdf
docs/references/mrds1322.pdf

Code that can send Radio Text to the referenced devices. There may be other examples on the web.
https://github.com/VixenLights/Vixen/blob/master/src/Vixen.Modules/Controller/RDSController/RdsSerialPort.cs
https://github.com/VixenLights/Vixen/blob/master/src/Vixen.Modules/Controller/RDSController/Module.cs

## Configuration

- The user should be able to configure the core serial information. 
- The user should be able to configure the Dynamic PS slot messages. TinyRDS allows 9 slots.
- The scrolling PS speed should ne set to low.
- The user should be able to configure the dynamic PS mode with one of the following options.
  - 0 - Fixed 8 characters
  - 1 - Scrolling text
  - 2 - Word alignment
  - 3 - Space seperated scrolling text 
- The user should be able to set the deplay between text loops of the Dynamic PS. 
- The user should be able to configure radio text slot messages to rotate besides slot one which will be the current song. RDS provides 4 slots including the current text slot.
- The user should be able to configure the milliseconds to wait before rotating to the next radio text slot. 
- The user should be able to configure if the RDS updater is enabled. Can be enabled concurrently with the file writer.
- The user should be able to configure if the existing file writer is enabled.


