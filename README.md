# USB-HID-Treadmill-Hack
Control speed + incline using Windows PC + 4-8 channel USB HID Relay board 
(new: Heart Rate controlled workouts via Bluetooth Heart Rate chest band - see update below)

![App icon](walk/usb-hid-treadmill.ico?raw=true "Icon")

Github project: https://github.com/dynalogix/USB-HID-Treadmill-Hack

Video demo of a 6-channel switch hooked up to the control panel of Kondition 4B-550 treadmill: https://youtu.be/e4qM4HLBIr8

Requirements

Hardware:
* Treadmill with accessible buttons for speed +/- incline +/-
* Minimum 4 Channel HID Drive-free USB Delay Relay Interface Board Module Optocoupler:
  * QYF-UR04 ("ucreatefun.com" with CH551G USB chip) e.g. https://www.ebay.co.uk/itm/283294148232
or
  * HW341 ("dcttech.com" with ULN2803 chip) e.g. https://www.ebay.com/itm/1-2-4-8-Channel-USB-Relay-Control-Switch-Computer-Control-for-Intelligent-Home
* Windows PC near treadmill, 1 USB port

![Relay board](relay.jpg?raw=true "board")

Software:
* Place hidapi.dll next to the walk.exe: https://github.com/libusb/hidapi/releases

![Screenshot](setup3.jpg?raw=true "setup") 

Fields:
* Duration [minutes] (i.e. length of the movie you will watch during workout) Note: you can enter expressions (e.g. 24+21*3 for multiple episodes)
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
  * Channel number for each button (set it to "0" if not connected):
    * SPEED_UP, SPEED_DOWN - necessary
    * INCL_UP, INCL_DOWN - if enabled: "hill" field lets you set a repeting climb section in workout
    * START - if enabled the program will start the treadmill if progress is 0
    * STOP - if enabled: show stop button to stop the treadmill
    * MODE - if enabled: it will be pressed twice and a SPEED- before starting (this sets "infinite" time on Kondition treadmills)
    * SPD3 - if enabled: press quick button 3 instead of "manually" advancing to 3kph      

Settings are saved between sessions

![Screenshot](workout.jpg?raw=true "active")

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

A 115 minute workout: you can see warm-up/wind-down and all the 8x3min "sprint" section's effect on the heart rate (measured using a TicWatch Pro 3 Wear OS smart watch)

![Heartrate](SmartSelect_20210129-202149_Mobvoi.jpg?raw=true "heartrate")

Settings:

![Settings](settings2.jpg?raw=true "settings")

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

* If connected program will press the MODE button and Speed- twice before starting to set 99km (=infinite) workout duration before staring (set ButtonMode to non-zero)
* It will also press the Start button if it is also connected (set ButtonStart to non-zero)
* The Stop on screen button will be enabled if it is connected (set ButtonStop to non-zero)
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

      hidapitester --vidpid 0519 --open --send-output 0,241 --close
      output 0,241: shorts channel 1
      output 0,1: opens channel 1
      output 0,242: shorts channel 2
      etc
      finally:
      output 0,249:shorts all channels
      output 0,9: opens all channels
    or
      hidapitester --vidpid 16C0/05DF --open --send-feature 0,253,1 --close
      feature 0,255,1: shorts channel 1
      feature 0,253,1: opens channel 1
      feature 0,255,2: shorts channel 2
      feature 0,253,2: opens channel 2
      etc

**Plans**
* Add more workout programs with selector
* Make window moveable, resizable

...or not. Currently this is what I need, I provide it as a basis for anyone wanting to create something similar.

Git Hub project: https://github.com/dynalogix/USB-HID-Treadmill-Hack

# 2023 update: Bluetooth Heart Rate control and more!

![Heart Rate](hr.png?raw=true "heart rate control")

**What's new:**
* connectivity to standard Bluetooth Heart Rate sensor (e.g. Polar H9)
* HR value displayed during workout
* new workout program (see heart checkbox) where we can specify a lower and upper target HR, and a (maximum) time between adjustments parameter (seconds)
* new graph showing heart rate vs. speed vs. incline
* workout screenshot and summary data is automatically saved into selected folder ("log dir path") a PNG and a TXT file is created with timestamp as filename

**Summary data example:** (e.g. 2023-01-19 18.46.txt, see 2023-01-19 18.46.png above)

     Duration: 25min
     HR Max: 124bps Avg: 103.19683bps Plot range: 85…128bps
     Speed Max: 7.00km/h Avg: 4.81km/h
     Ascend: 9946
     Distance: 1991m
     Calories: 210KCal
     Sections:
      0:00:06 warmup↑100
      0:04:06 ↑115
      0:05:55 →115
      0:06:55 ↓100
      0:08:03 →100
      0:09:05 ↑115
      0:10:50 →115
      0:11:50 ↓100
      0:13:26 →100
      0:13:58 ↑115
      0:15:17 →115
      0:16:01 ↓100
      0:17:35 →100
      0:18:06 ↑115
      0:19:52 →115
      0:20:24 ↓100
      0:22:07 →100
      0:22:07 cool↓3.0

**New settings:**
* **BT** - select bluetooth HR device (it has to be paired in Windows settings)
* **♥ checkbox** - toggle between time controlled or heart rate controlled workouts
* **TBA** - time between adjustments: maximum delay [seconds] between speed / incline adjustments
* **Low ♥** - lower heart rate target
* **High ♥** - upper heart rate target
* **Log dir path** - if specified screenshots (png) and summary text files (txt) are saved in this directory (timestamp as file name)
* **Birth date, gender, weight** - needed for calorie calculation
* **HTTP on start** - provide a webhook to turn on the treadmill
* **Heart rate** - shows the detected HR as soon as the sensor is connected. Turns red if battery<20%. Click into field (via mouse) to re-initialize BT discovery 

**New, heart rate controlled workout:**
* Warm up: speed is increased every (TBA / 4) seconds until lower heart rate target is reached (store time needed and use it to make cooldown the same duration!)
* Increase speed and incline (alternating) until upper heart rate target is reached ("Hill" maximum incline setting is obeyed!)
* Attempt to keep hr at this upper target value by adjusting the speed for 60 seconds (currently hardwired)
* Reduce speed and incline (alterning) until lower heart rate target is reached 
* Attempt to keep hr at this lower target value by adjusting the speed for 60 seconds (currently hardwired)
* Repeat upper / lower target rates until cooldown is scheduled (same duration as it took to "warm up" to the lower HR target)
* Summary screenshot and text file is saved when you exit the app
