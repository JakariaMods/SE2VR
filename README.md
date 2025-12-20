# SE2VR Plugin
This is a plugin that adds [OpenVR](https://github.com/ValveSoftware/openvr) support to [Space Engineers 2](https://store.steampowered.com/app/1133870/Space_Engineers_2/). This project aims to provide a seamless VR experience that can be played from start to finish.

https://www.youtube.com/watch?v=IntVsKmTe7k

https://www.youtube.com/watch?v=CkOQraQ0wsI

Known issues/limitations:
* Render is limited to 30fps per eye, this is because render commands are submitted at 60hz (RenderCommandBuffer.Commit is in main thread)

# Running:

**1.** Download the latest release or compile it yourself

**2.** Extract the zip file into a folder

**3.** Add the following launch argument to steam with the correct path
   
-plugins:"C:/Path/To/SE2VR.dll"

**4.** Launch Space Engineers 2. 

**Please note that this plugin does not auto-update, and is likely to break between updates.**

# Supported controllers

* Valve Index (Mapped, recommended)

* HTC Vive (Mapped, not recommended due to missing buttons)

* Everything else is untested, feel free to create mappings for your controllers and submit a pull request

# How to play

* This is a Standing or Sitting VR experience, make sure you are positioned at the center of the play-space. You can use SteamVR to reset the standing/seated position if it is in a different spot.

* UI interaction is currently done using the VR dashboard. Whenever mouse input is accepted, the VR dashboard will automatically open, where you can use your VR hand to drag and click on the projected screen.

* To close the in-game UI, close the dashboard. If the UI does not automatically close, report it with STR.

# Steps to compile the project yourself:

**1.** Download [.NET 9](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

**2.** Run SetupRepository.bat to create a symlink to the SE2 DLLs

**3.** Open the solution and compile SE2VR in Release configuration (or run "dotnet build SE2VR.sln" in command prompt)

# Frequently Asked Questions

**1.** How can I increase the height of my player? Why are my hands huge?

Change the "PlayerHeight" parameter in the OpenVROptions file to your ACTUAL height. This will rescale your world to fit the SE2 engineer (who is 1.75m tall).

**2.** Why is the image noisy/blurry?

Raytracing/TAA does not behave well with this implementation of VR (It introduces a lot of artifacts and prevents the image from ever stabilizing). You can improve the quality by disabling raytracing and FSR. You can also try changing the Render Resolution in Steam VR settings, increasing it above 100% will result in a sharper image.

**3.** Why is the frame rate low?

The implementation of VR used results in half of the frames being allocated to the left eye, and the other half to the right. This effectively halves the framerate. Enabling motion smoothing in SteamVR should help.

# Settings/Options

VR Options can be configured offline at:

%appdata%\SpaceEngineers2\AppData\EngineOptions\OpenVROptions

You will need to restart the game for them to apply.
