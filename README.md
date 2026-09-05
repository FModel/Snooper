Snooper - An Unreal Engine Packages Renderer in C#
------------------------------------------

[![CI Status](https://img.shields.io/github/actions/workflow/status/FModel/Snooper/build.yml?label=CI)](https://github.com/FModel/Snooper/actions)
[![Activity](https://img.shields.io/github/commit-activity/y/FModel/Snooper?color=yellow)]()
[![Discord](https://discord.com/api/guilds/637265123144237061/widget.png?style=shield)](https://fmodel.app/discord)
***

### Description:
Snooper is a real-time OpenGL 4.6 renderer for [Unreal Engine](https://www.unrealengine.com/en-US/) packages parsed by [CUE4Parse](https://github.com/FabianFG/CUE4Parse). It is built around an actor/component/system framework and a deferred, GPU-driven render pipeline with clustered lighting, cascaded shadows, ambient occlusion and anti-aliasing.

While primarily developed for [FModel](https://github.com/4sval/FModel) as its 3D viewer, Snooper is a standalone project and contributions are always welcome, particularly in the areas of material fidelity, rendering performance, and support for more Unreal Engine features.

### Building
```shell
git clone https://github.com/FModel/Snooper.git --recursive
dotnet run --project Launcher/Launcher.csproj
```
The Launcher is a development entry point: edit the archive directory, AES key, mappings file and `EGame` version at the top of `Launcher/Program.cs` to point at your own game before running it.

Snooper targets .NET 10 and requires a GPU with OpenGL 4.6 and bindless texture support.

### Screenshots:
<p align="center">
  <img width="100%" alt="Snooper" src="https://github.com/user-attachments/assets/a863e6ae-d51f-4dda-89f9-2deab03644c9" />
</p>
<table>
  <tr>
    <td width="50%"><img alt="Snooper" src="https://github.com/user-attachments/assets/1eb0c9f0-3028-4f66-9141-bb93118f935c" /></td>
    <td width="50%"><img alt="Snooper" src="https://github.com/user-attachments/assets/7bcda493-42eb-49a0-938a-c3fd7a47548a" /></td>
  </tr>
  <tr>
    <td width="50%"><img alt="Snooper" src="https://github.com/user-attachments/assets/ef18866a-401d-4b09-9d70-b39b40264c75" /></td>
    <td width="50%"><img alt="Snooper" src="https://github.com/user-attachments/assets/7751be5c-85a3-4440-9c59-27b5e7dbddf6" /></td>
  </tr>
</table>

### License:
Snooper is licensed under [GPL-3](https://github.com/FModel/Snooper/blob/opengl/LICENSE), and licenses of third-party libraries used are listed [here](https://github.com/FModel/Snooper/blob/opengl/NOTICE).
