<img alt="Edited image of the Space Engineers 2 Astronaut wearing a Valve Index" src="https://github.com/user-attachments/assets/4fff1b19-1673-4456-91a8-e5a144050720" />

# SE2VR Plugin (VS4.0.0.3165+)
This is a plugin that adds [OpenVR](https://github.com/ValveSoftware/openvr) support to [Space Engineers 2](https://store.steampowered.com/app/1133870/Space_Engineers_2/). This project aims to make the Space Engineers 2 survival experience playable from start to finish in VR.

https://www.youtube.com/watch?v=B1hnT4PmXbg

# Compiling:

**1.** Run **SetupRepository.bat** to create a symlink to the SE2 DLLs

**2.** Open the solution and compile SE2VR in Debug or Release configuration (or run "dotnet build SE2VR.sln" in command prompt)

# Running/Installation:

**1.** Extract the .zip file into a folder of your choice

<img width="774" height="355" alt="image" src="https://github.com/user-attachments/assets/c0a0ab34-2143-4103-901e-e3388262cd73" />

**2.** Add the following launch argument to Space Engineers 2 with the correct path

-plugins:"C:/Path/To/SE2VR.dll"

<img width="1041" height="353" alt="image" src="https://github.com/user-attachments/assets/da3382ef-a9d2-4474-88bd-0c372b3cd2e8" />

**3.** Launch Space Engineers 2. Steam VR will open automatically

**Please note that this plugin does not auto-update, and is likely to break between updates.**

# Supported Headsets

* HTC Vive

* Everything else is untested, please submit pull requests with fixes if it does not work with your headset

# Supported controllers

* Valve Index (Mapped, recommended)

* HTC Vive (Mapped, not recommended due to missing buttons)

* Everything else is untested, please submit pull requests with mappings if you want

# How to play

* This is a Standing or Sitting VR experience, make sure you are positioned at the center of the play-space. You can use SteamVR to reset the standing/seated position if it is in a different spot.

* UI interaction is currently done using the VR dashboard. Whenever mouse input is accepted, the VR dashboard will automatically open, where you can use your VR hand to drag and click on the projected screen.

* To close the in-game UI, close the dashboard. If the UI does not automatically close, report it with repeatable STR, logs, etc.

# Settings/Options

VR Options can be configured offline at:

%appdata%\SpaceEngineers2\AppData\EngineOptions\OpenVROptions

You will need to restart the game for them to apply.
