About scene:
We set all plants and grass as tree objects as  unity HD RP doesn't support terrain grass systems. 

No grass system affect the performance. That's why number of saved by baching is so huge but...performance at scene should be good anyway. About 60 FPS+
We will change this as soon unity will support terrain grass or something that we could use to buiuld proper scene. (It should be very soon)

BEFORE YOU START:
- you need Unity  2019.2 
- you need HD SRP pipline 6.9 if you use higher etc custom shaders will not work. 
- Engine change exposure calculation and it broke all water shader. It's raported bug and we wait for fix. This basicly killed distortion.
You can bring it back by remove: *0.001 at lines with: GetInverseCurrentExposureMultiplier(). In our demo scene it will work fine but at other it mcould be broken.

Be patient this tech is so fluid... we coudn't fallow ever beta version

Step 1 - Setup Shadows and other render setups. Find File "HDRenderPipelineAsset" 
    - Change shadow atlas width and height to 2048 or 4096, Rather this first one.
	- Find Material section at "HDRenderPipelineAsset" and drag and drop our SSS settings diffusion profiles for foliage into Diffusion profile list:
		  NM_SSSSettings_Skin_Foliage
		  NM_SSSSettings_Skin_NM Foliage
		  NM_SSSSettings_Skin_NM Foliage Trees
		  Without this foliage materials will not become affected by scattering and they will look wrong.	
	- Optionaly turn on "increase resolution of volumetrics (a bit expensive but I didn't notice big drop so..) 
	- Check if volumetric light is turned on.
	- Check if you got Deferred only in Lit Shader mode.
	- Check if contact shadows are turned on
	- Turn On SRP Batcher 

Step 2 Go to project settings and quality and set:
	- Set VSync to don't sync
	- LOD Bias to 1. We setup whole asset to this value which is default not like in LW and Standard rendering

Step 3 Find "HD RP Forest Demo Scene" and open it.

Step 4 Find "Scene Post-process (1)" at scene hierarchy and turn "on" depth of field - make note that in windows 7 it could make sky black - seams unity engine bug 


Step 5 - chose way of movment. Movie track or free movment.
	Chose camera and turn on or off "playable directior" and "animation" or leave free camera movment turned on.

Step 6 - HIT PLAY!:)

TIPS
	IF ANY SHADER IS PINK PLEASE right click on it and  click - reimport;) We found that sometimes shaders didn't compile at first import.
	Like we said HD RP is really fluid now.

	About scene construction:
		- There are post process profiles: Default Post-process and Scene Post-process. Manage post process by scene post process object.
		- There is Render Settings object, It's are important like hell because basicly it's core of rendering and light managment.
		- There are Density Volume objects which manage volumetric fog density in forest
		- Prefab wind manage wind speed and direction at the scene

Play with it give us feedback and lern about hd srp power.

