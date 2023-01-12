# ![](./Source/FTS_64.png) Free Train Simulator  
[![Join the chat at https://gitter.im/ORTS-MG/community](https://badges.gitter.im/ORTS-MG/community.svg)](https://gitter.im/ORTS-MG/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This is a fork of [OpenRails](http://www.openrails.org), running on .NET Core and based on [Monogame patch](http://www.elvastower.com/forums/index.php?/topic/30924-going-beyond-the-4-gb-of-memory/page__view__findpost__p__237281) and many other improvements and performance tweaks like completely rewritten RailDriver input, Signalscript-Parser and much more, at the same time adopting most of the updates and features from [OpenRails Source Code](https://github.com/openrails/openrails) as well.

## Feature overview
In addition to all the features from OpenRails, this version includes:  
- Most recent version of Monogame (3.8) 
- build on .NET 6, which generally allows for cross platform use (see [wiki](https://github.com/perpetualKid/ORTS-MG/wiki/Linux-Wine) for Linux support)
- full 64bit support, removing out-of-memory situations and allows to use all available system memory also beyond 3GB/4GB barrier as with 32bit software
- rewritten SignalScript parsing engine for faster loading time
- [Standalone Multiplayer server](https://github.com/perpetualKid/ORTS-MG/wiki#2021-12-05-multiplayer-standalone-server) simplifying multi-player games
- New [TrackViewer Toolbox](https://github.com/perpetualKid/ORTS-MG/wiki#2021-03-09-new-trackviewer-preview), also handling large routes smoothly
- Rewritten RailDriver support, with built-in unit calibration support, no need to to handle extra files&tools
- New Translation engine, allowing for simpler Localization (support for PoEdit .mo files removes the need for compiled resource dlls)
- Many performance optimizations

## Download
[![GitHub All Releases](https://img.shields.io/github/downloads/perpetualKid/orts-mg/total)](https://github.com/perpetualKid/ORTS-MG/releases/)

If you came here just to download the software, please see the [Releases](https://github.com/perpetualKid/ORTS-MG/releases) section to download a recent version of the software. Simply unzip the download folder and start OpenRails.exe. 
Once started, please be aware there may be updates available immediately, as the release page only has major release packages. Minor updates are only available through the Auto-Updater, as well developer builds which are updated each time some code is updated. Also check the [News](https://github.com/perpetualKid/ORTS-MG/wiki#news) section in the wiki.
## Contributing
If you are interested in more information, documentation and news regarding recent updates, please check the [Wiki](https://github.com/perpetualKid/ORTS-MG/wiki).

If you have bright ideas, questions or other topics to discuss, please share them in the [Discussions](https://github.com/perpetualKid/ORTS-MG/discussions) section.

To report bugs or other issues, use the [Issue Tracker](https://github.com/perpetualKid/ORTS-MG/issues). Please provide as much input possible, ideally attaching log files or other relevant and supporting information.

Anyone is welcome to contribute, and this is not limited to programmers writing code. There are many areas which would benefit from a wide range of skills, experience or purely passion, such as improvements to visual designs, documentation and translations, support with project management and feature planning, software architecture and design, or research for new technologies and frameworks. Check the contribution guidelines for further details, submit change proposals through [pull requests](https://github.com/perpetualKid/ORTS-MG/pulls), or introduce your thoughts for contribution in a [Discussion](https://github.com/perpetualKid/ORTS-MG/discussions).


## Installation Requirements

Running on Windows 10 with recent patch status (version 1809 or higher), the only separate download needed may be [.NET 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0). If not installed already, trying to start the program will guide through necessary downloads.  

You will need to have an DirectX 11.0 compatible graphics adapter (GPU).

To install on Linux, please see the [Wiki](https://github.com/perpetualKid/ORTS-MG/wiki/Linux-Wine).

## Build Information

|Release Type|Build Status|Build Version|
|------------|------------|-------------|
|Release|[![Build Status](https://dev.azure.com/perpetualKid/ORTS-MG/_apis/build/status/Build/Azure%20Cloud%20Build?branchName=development)](https://dev.azure.com/perpetualKid/ORTS-MG/_build/latest?definitionId=17&branchName=main)|![Release Build](https://img.shields.io/endpoint?url=https://orts.blob.core.windows.net/releases/badges/v/orts-mg.json)|
|Release Candidate|[![Build Status](https://dev.azure.com/perpetualKid/ORTS-MG/_apis/build/status/Build/Azure%20Cloud%20Build?branchName=development)](https://dev.azure.com/perpetualKid/ORTS-MG/_build/latest?definitionId=17&branchName=release/*)|![Release Build](https://img.shields.io/endpoint?url=https://orts.blob.core.windows.net/releases/badges/vpre/orts-mg.json)|
|Developer Builds|[![Build Status](https://dev.azure.com/perpetualKid/ORTS-MG/_apis/build/status/Build/Azure%20Cloud%20Build?branchName=development)](https://dev.azure.com/perpetualKid/ORTS-MG/_build/latest?definitionId=17&branchName=development)|![Release Build](https://img.shields.io/endpoint?url=https://orts.blob.core.windows.net/builds/badges/vpre/orts-mg.json)|
