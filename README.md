# BeamNGSimFeedback


ABOUT
=====
Beam NG.Drive Plugin for motion telemetry through SimFeedback.

https://opensfx.com/

https://github.com/SimFeedback/SimFeedback-AC-Servo


RELEASE NOTES
=============
v1.0 - First release.

INSTALLATION INSTRUCTIONS 
=========================

1. Ensure you have the .NET Framework v4.8 runtime installed.

Download: https://dotnet.microsoft.com/download/dotnet-framework/net48

2. Download and extract the latest release zip of BeamNGSimFeedback.

Download: https://github.com/PHARTGAMES/BeamNGSimFeedback/tree/master/Releases

3. Copy the contents of the SimFeedbackPlugin folder within the BeamNGSimFeedback .zip into your SimFeedback root folder.

4. Within BeamNG.drive 'Options > OTHER', set the following options:
  
 * MotionSim enabled [x]
 * IP: 127.0.0.1
 * Port: 4444
 * Update rate: 50
 * Acceleration Smoothing: X, Y, Z all set to 0. (This is a personal preference, however these values are also programatically smoothed within the plugin code.)

AUTHOR
======

PEZZALUCIFER


SUPPORT
=======

Support available through SimFeedback owner's discord

https://opensfx.com/simfeedback-setup-and-tuning/#modes
