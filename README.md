## Setup

1. Install c# development IDE of your choice. Rider or Visual Studio are common options. Rider is recommended, as Godot requires a .sln file and Visual Studio is moving away from those, making it more difficult to
generate a project that uses one.

2. Download latest [Megadot](https://megadot.megacrit.com/) or [Godot](https://godotengine.org/download/windows/) (should still compatible for compilation)
3. Download latest .pck and .dll of [BaseLib](https://github.com/Alchyr/BaseLib-StS2/releases) and copy to `Slay The Spire 2/mods`. You may have to create this folder.
4. Download entire template project and load as a template in Rider (From step 7  here https://www.jetbrains.com/help/rider/Install_custom_project_templates.html#create-custom-project-template), 
   or get template from NuGet with `dotnet new install Alchyr.Sts2.Templates@1.0.3`. The github project will likely be slightly more up-to-date.
5. Create new solution using template. The format should be `.sln`.
   Make sure not to put any spaces in the project name, and enable `Put solution and project in same directory`.
   Expand "Advanced Settings" (in Rider) to adjust author and some other options.
6. Update filepaths at the top of the `.csproj` file. Select the project
<img width="251" height="92" alt="image" src="https://github.com/user-attachments/assets/e9fbc231-da1d-46a7-8f36-b6c5b4bb1369" />
and press F4 to open it in Rider.
7. You can change the mod's display name in `mod_manifest.json`. Do not modify `pck_name` unless you also rename the project and adjust `project.godot`.

## Publish/Build:
Right click the project and choose "Publish". In Rider, choose `Local folder`. Publish options can be left as default. This project is set up for Publish to compile your code and the Godot .pck to the Slay the Spire 2 mods folder which is where they need to be for the game to detect them.

Usually you can just Build rather than Publish. This will only generate your code `.dll` and copy it over to the mods folder, but not the `.pck`, which is much faster.


You can find a WIP character mod using BaseLib [here](https://github.com/Alchyr/Oddmelt)
