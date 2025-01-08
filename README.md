# ![Logo](./Source/FTS_64.png) Free Train Simulator

[![Join the chat at https://gitter.im/ORTS-MG/community](https://badges.gitter.im/ORTS-MG/community.svg)](https://gitter.im/ORTS-MG/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

FreeTrainSimulator (FTS) has once been [forked](https://github.com/openrails/openrails) from [OpenRails](http://www.openrails.org). With consistent focus on platform improvements, and a different pace of development efforts, FTS has diverged much from the original OpenRails source base, so it became an independent application, which also led to and is expressed by the new branding and name.  
FTS is running on .NET 8, based on the [Monogame patch](http://www.elvastower.com/forums/index.php?/topic/30924-going-beyond-the-4-gb-of-memory/page__view__findpost__p__237281), and includes many other changes and improvements, such as completely rewritten RailDriver input, new Signalscript-Parser engine, and much more.

## Feature overview

In addition to features from OpenRails, Free Train Simulator includes:  

- Most recent version of Monogame (3.8.2)
- build on .NET 8, which generally allows for cross platform use (see [wiki](https://github.com/perpetualKid/FreeTrainSimulator/wiki/Linux-Wine) for Linux support)
- full 64bit support, removing out-of-memory situations and allows to use all available system memory also beyond 3GB/4GB barrier as with 32bit software
- rewritten SignalScript parsing engine for faster loading time
- [Standalone Multiplayer server](https://github.com/perpetualKid/FreeTrainSimulator/wiki#2021-12-05-multiplayer-standalone-server) simplifying multi-player games
- New [TrackViewer Toolbox](https://github.com/perpetualKid/FreeTrainSimulator/wiki#2021-03-09-new-trackviewer-preview), also handling large routes smoothly
- Rewritten RailDriver support, with built-in unit calibration support, no need to to handle extra files&tools
- New Translation engine, allowing for simpler Localization (support for PoEdit .mo files removes the need for compiled resource dlls)
- Many performance optimizations

## Download

[![GitHub All Releases](https://img.shields.io/github/downloads/perpetualKid/orts-mg/total)](https://github.com/perpetualKid/ORTS-MG/releases/)

If you came here just to download the software, please see the [Releases](https://github.com/perpetualKid/FreeTrainSimulator/releases) section to download a recent version of the software. Simply unzip the download folder and start FreeTrainSimulator.exe (or OpenRails.exe in older releases).  
Once started, please be aware there may be further updates available for download, as the release page tracks major version releases only. Minor updates are available through the Auto-Updater, as well the developer builds which are shared each time when code is updated.  
Also check the [News](https://https://github.com/perpetualKid/FreeTrainSimulator/wiki#news) section in the wiki for other announcements.

## Content

Please refer to content from Open Rails

- [OpenRails currated content](https://www.openrails.org/download/explore-content/index.html)

Other Train Simulator forums and content developers

- [Indian Railways Train Simulator](https://irts.in/)
- [TrainSim.com](https://www.trainsim.com/forums/filelib-home)
- [TSSF.eu](https://tssf.eu/forum/)
- [Das MSTS Forum](https://kunifuchs.com/burningboard/)

## Contributing

If you are interested in more information, documentation and news regarding recent updates, please check the [Wiki](https://github.com/perpetualKid/FreeTrainSimulator/wiki).

If you have bright ideas, questions or other topics to discuss, please share them in the [Discussions](https://github.com/perpetualKid/FreeTrainSimulator/discussions) section.

To report bugs or other issues, use the [Issue Tracker](https://github.com/perpetualKid/FreeTrainSimulator/issues). Please provide as much input possible, ideally attaching log files or other relevant and supporting information.

Anyone is welcome to contribute, and this is not limited to programmers writing code. There are many areas which would benefit from a wide range of skills, experience or purely passion, such as improvements to visual designs, documentation and translations, support with project management and feature planning, software architecture and design, or research for new technologies and frameworks. Check the contribution guidelines for further details, submit change proposals through [pull requests](https://github.com/perpetualKid/FreeTrainSimulator/pulls), or introduce your thoughts for contribution in a [Discussion](https://github.com/perpetualKid/FreeTrainSimulator/discussions).

## Installation Requirements

Running on Windows 10 with recent patch status (version 1809 or higher), the only separate download needed may be [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).  
If not installed already, trying to start the program will guide through necessary downloads. Please also see [this article](https://github.com/perpetualKid/FreeTrainSimulator/wiki/.NET-Framework) in our [wiki](https://github.com/perpetualKid/FreeTrainSimulator/Wiki).  

You will need to have an DirectX 11.0 compatible graphics adapter (GPU).

To install on Linux, please see the [Wiki](https://github.com/perpetualKid/ORTS-MG/wiki/Linux-Wine).

## Build Information

|Release Type|Build Status|Build Version|
|------------|------------|-------------|
|Release|[![Build Status](https://dev.azure.com/perpetualKid/ORTS-MG/_apis/build/status/Build/Azure%20Cloud%20Build?branchName=development)](https://dev.azure.com/perpetualKid/ORTS-MG/_build/latest?definitionId=17&branchName=main)|![Release Build](https://img.shields.io/endpoint?url=https://orts.blob.core.windows.net/releases/badges/v/freetrainsimulator.json)|
|Release Candidate|[![Build Status](https://dev.azure.com/perpetualKid/ORTS-MG/_apis/build/status/Build/Azure%20Cloud%20Build?branchName=development)](https://dev.azure.com/perpetualKid/ORTS-MG/_build/latest?definitionId=17&branchName=release/*)|![Release Build](https://img.shields.io/endpoint?url=https://orts.blob.core.windows.net/releases/badges/vpre/freetrainsimulator.json)|
|Developer Builds|[![Build Status](https://dev.azure.com/perpetualKid/ORTS-MG/_apis/build/status/Build/Azure%20Cloud%20Build?branchName=development)](https://dev.azure.com/perpetualKid/ORTS-MG/_build/latest?definitionId=17&branchName=development)|![Release Build](https://img.shields.io/endpoint?url=https://orts.blob.core.windows.net/builds/badges/vpre/freetrainsimulator.json)|
