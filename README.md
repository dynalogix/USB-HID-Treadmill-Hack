# USB-HID-Treadmill-Hack
Control speed + incline using Windows PC + 4 channel USB HID Relay board

![App icon](icon.jpg?raw=true "Icon")

Requirements

Hardware:
* Treadmill with accessible buttons for speed +/- incline +/-
* QYF-UR04 Minimum 4 Channel HID Drive-free USB Delay Relay Interface Board Module Optocoupler
* Windows PC near treadmill, 1 USB port

Software:
* Hidapitester https://github.com/todbot/hidapitester/releases/tag/0.1

![Screenshot](setup.jpg?raw=true "setup")

Fields:
* Duration [minutes] (i.e. length of the movie you will watch during workout)
* Speed - desired speed
* Slope - how quickly reach desired speed: speed increase in 10 minutes (e.g. "6" → will go from 3.0 to 6.0 in 5 minutes)
* Sprint - maximum speed during sprint phases
* Hill - incline increase during "hill" phases
* Start button - press again to Stop (prompt)
* Progress - you can edit this and restart a stopped session

![Screenshot](active.jpg?raw=true "active")

* Progress [minutes] - how far into the duration
* Next [seconds] - until the next change (speed or incline)
* Speed - current speed (adjust treadmill if they don't match)
* Incline - current incline (adjust treadmill if they don't match)
* [X] - close app (prompt if session active)
* [ ] - keep window on top

**Workout**

I plan to add more, currently there is one implemented:

* at *slope* increase from 3.0 to desired *speed*
* slowly increase incline from 0 to *hill*
* slowly decrease include from *hill* to 0
* 5 minute *sprint*
* slowly increase incline from 0 to half *hill*
* slowly decrease incline from half *hill* to 0
* 5 minute *sprint*
* at *slope* decrease desired *speed* to 3.0
* end of workout - end of movie

The workout logic is in the function workout1:

* d - duration in seconds
* p - progress
* s - speed
* r - incline
* mx - desired speed
* hl - hill 
* sp - sprint

**Calls**

toggle(switch, state)
* switch: SPEED_UP = 1, SPEED_DOWN = 2, INCL_UP = 3, INCL_DOWN = 4, ALL = 9
* state: ON=1, OFF=0

press(switch)
* it will wait 0.5 sec between ON and OFF

wait(delay)
* delay in seconds
* returns true if stop was pressed

**Plans**
* Make path location of *hidapitester.exe* configurable
* Add more workout programs with selector
* Make window moveable, resizable

...or not. Currently this is what I need, I provide it as a basis for anyone wanting to create something similar.
