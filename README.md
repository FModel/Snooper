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
  <img width="100%" alt="Valorant in Snooper" src="https://github.com/user-attachments/assets/e2c5c5c4-d3fc-409a-9c5f-eafb07b30777" />
</p>
<table>
  <tr>
    <td width="50%"><img alt="Stray in Snooper" src="https://github.com/user-attachments/assets/682d5d59-8f0f-4f94-89d3-662ffdcd344e" /></td>
    <td width="50%"><img alt="MultiVersus in Snooper" src="https://github.com/user-attachments/assets/4005737c-f463-4a59-9c6c-d6c00d2ed019" /></td>
  </tr>
  <tr>
    <td width="50%"><img alt="Fortnite in Snooper" src="https://github.com/user-attachments/assets/0ea411e2-f542-4865-b807-bfcf315a4d05" /></td>
    <td width="50%"><img alt="Marvel Rivals in Snooper" src="https://github.com/user-attachments/assets/c240b674-7b7c-4939-82ca-724f878eb43d" /></td>
  </tr>
</table>

### License:
Snooper is licensed under [GPL-3](https://github.com/FModel/Snooper/blob/opengl/LICENSE), and licenses of third-party libraries used are listed [here](https://github.com/FModel/Snooper/blob/opengl/NOTICE).
