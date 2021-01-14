# USB-HID-Treadmill-Hack
Control speed + incline using Windows PC + 4 channel USB HID Relay board

![App icon](walk/usb-hid-treadmill.ico?raw=true "Icon")

Github project: https://github.com/dynalogix/USB-HID-Treadmill-Hack

Requirements

Hardware:
* Treadmill with accessible buttons for speed +/- incline +/-
* QYF-UR04 Minimum 4 Channel HID Drive-free USB Delay Relay Interface Board Module Optocoupler (with CH551G USB chip)
  e.g. https://www.ebay.co.uk/itm/283294148232
* Windows PC near treadmill, 1 USB port

![Relay board](relay.jpg?raw=true "board")

Software:
* Place hidapi.dll next to the walk.exe: https://github.com/libusb/hidapi/releases

![Screenshot](setup2.jpg?raw=true "setup") 

Fields:
* Duration [minutes] (i.e. length of the movie you will watch during workout)
* Speed - desired speed
* Warm-up / Wind-down time [minutes] - how quickly reach desired speed
* Sprint - maximum speed during sprint phases
* Sprint duration [minutes] - how long the sprint period is
* Hill - incline increase during "hill" phases
* Reps - how many times the (hill+sprint) section repeated (with decreasing hills)
* Start button - press again to Stop (prompt)
* Progress - you can edit this and restart a stopped session
* Stop button - stops the treadmill (if enabled)
* Gear icon (upper left) - toggles settings:
  * Vidpid - entered as two hex values separted by a forward slash (/) - more info below
  * ButtonPressSec - how long the buttons need to be held down (in seconds, e.g. 0.2)
  * Checkboxes for buttons:
    * START - if enabled the program will start the treadmill if progress is 0
    * STOP - if enabled: show stop button to stop the treadmill
    * MODE - if enabled: it will be pressed twice and a SPEED- before starting (this sets "infinite" time on Kondition treadmills)

Settings are saved between sessions

![Screenshot](active.jpg?raw=true "active")

* Progress [minutes] - how far into the duration
* Next [minutes] - until the next change (speed or incline)
* Speed - current speed (adjust treadmill if they don't match)
* Incline - current incline (adjust treadmill if they don't match)
* [X] - close app (prompt if session active)
* [ ] - keep window on top

**Workout**

I plan to add more, currently there is one implemented:

![Chart](chart.png?raw=true "chart")

* in *warmup* minutes increase from 3.0 to desired *speed*
* 
* slowly increase incline from 0 to *hill*
* slowly decrease include from *hill* / 1 to 0
* Sprint for *sprint duration* at *sprint speed*
*
* slowly increase incline from 0 to half *hill*
* slowly decrease incline from *hill* / 2  to 0
* Sprint for *sprint duration* at *sprint speed*
* 
* ...repeat *reps* times (with decreasing hills: *hill* / 3, *hill* / 4 etc)
* 
* in *winddown* minutes decrease desired *speed* to 3.0
* end of workout - end of movie

Heart rate for a 126 minute workout (movie Shazam)

![Heartrate](heart2.jpg?raw=true "heartrate")

The workout logic is in the function workout1:

* dur - duration in seconds
* p - progress
* s - speed
* r - incline
* speed - desired speed
* hl - hill 
* sp - sprint
* warm - warmup/winddown duration between 0.1 steps (in seconds)

**Calls** 

toggle(switch, state)
* switch: SPEED_UP = 1, SPEED_DOWN = 2, INCL_UP = 3, INCL_DOWN = 4, ALL = 9
* state: ON=1, OFF=0

press(switch)
* it will wait 0.5 sec between ON and OFF

wait(delay)
* delay in seconds
* returns true if stop was pressed

**Set your VIDPID and optional parameters **

After opening the app at least once, find the configuration file under 
C:\Users\???\AppData\Local\walk\walk_???\1.0.0.0\user.config

Add two settings manually (otherwise some incorrect defaults will be used):

     <?xml version="1.0" encoding="utf-8"?>
     <configuration>
         <configSections>
             <sectionGroup name="userSettings" type="System.Configuration.UserSettingsGroup, System.Configuration.ConfigurationManager, Version=4.0.3.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51" >
                <section name="walk.Properties.Settings" type="System.Configuration.ClientSettingsSection, System.Configuration.ConfigurationManager, Version=4.0.3.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51" allowExeDefinition="MachineToLocalUser" requirePermission="false" />
             </sectionGroup>
         </configSections>
         <userSettings>
             <walk.Properties.Settings>
              <setting name="Duration" serializeAs="String"><value>100</value></setting>
              <setting name="Sprint" serializeAs="String"><value>7.5</value></setting>
              <setting name="Warmup" serializeAs="String"><value>8</value></setting>
              <setting name="Speed" serializeAs="String"><value>6.2</value></setting>
              
              <!-- Add these optional parameters -->             
              <setting name="Vidpid" serializeAs="String"><value>0519/2018</value></setting>
              <setting name="ButtonPressSec" serializeAs="float"><value>0.2</value></setting>
              <setting name="ButtonStart" serializeAs="bool"><value>True</value></setting>
              <setting name="ButtonStop" serializeAs="bool"><value>False</value></setting>
              <setting name="ButtonMode" serializeAs="bool"><value>True</value></setting>
              
             </walk.Properties.Settings>
         </userSettings>
     </configuration>

* If connected program will press the MODE button and Speed- twice before starting to set 99km (=infinite) workout duration before staring (set ButtonMode to True)
* It will also press the Start button if it is also connected (set ButtonStart to True)
* The Stop on screen button will be enabled if it is connected (set ButtonStop to True)
* Set buttonPressSec according to your device. Some devices need longer button presses (e.g. 0.5 sec) to be registered, others start repeating the button if it is pressed for too long, set a lower value here (e.g. 0.2 sec)

**How to find vid/pid**

Get the app Hidapitester: https://github.com/todbot/hidapitester/releases/tag/0.1

    Use command:

    hidapitester --list

    Look for the id in the beginning of the line which has "HIDRelay":

    e.g.:
    0519/2018: Ucreatefun.com - HIDRelay
    ↓
    here the vidpid is 0519/2018

    Then you can open, write, close in one command.

    hidapitester --vidpid 0519 --open --send-output 0,241 --close >nul

    output 0,241: shorts channel 1
    output 0,1: opens channel 1
    output 0,242: shorts channel 2
    etc

    finally:
    output 0,249:shorts all channels
    output 0,9: opens all channels

**Plans**
* Add more workout programs with selector
* Make window moveable, resizable

...or not. Currently this is what I need, I provide it as a basis for anyone wanting to create something similar.

Git Hub project: https://github.com/dynalogix/USB-HID-Treadmill-Hack

