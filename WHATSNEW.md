# New in this version

## Features

- Toolbox is released as the replacement for the legacy TrackViewer. It provides route inspection and navigation, train-path creation and editing, saved-path comparison and combination, repair workflows, path charts, and native FTS model persistence. See the [Toolbox wiki](https://github.com/perpetualKid/FreeTrainSimulator/wiki/Toolbox) for supported workflows and boundaries.
- Updated Monogame version 3.8.6.1
- Toolbox has been reworked into a modern, dockable IDE-style desktop application, with movable, floating, auto-hiding, and tabbed tool windows arranged around the central map view:
  - Tool windows for Routes, Settings, Route Navigation, Train Path details, Location, Logging, Help, and Debug information
  - Settings tool window with separate tabs for general options, item colors, and item visibility, plus a "Reset to Defaults" action
  - Route Navigation to center the map on stations, platforms, and sidings by name, or to locate track items and track nodes by index
  - Always-on status bar showing tile, track, and item information under the mouse pointer
  - Window layout, undocked tool window sizes, and main window placement are remembered between sessions and can be reset to a clean default
  - User interface language can be switched at runtime
- Multiple configuration profiles, available through Menu application, allow to have all settings profile-dependent
- Existing content folder settings are imported from OpenRails

## Updates
- Toolbox
	- Lazy map startup retains the regular docked tool windows around an empty map; opening the last route manually also works when automatic startup restoration is disabled.
	- Improve text readability for named track items by using fount outlining in contrast color
	- Track segments in Toolbox view are drawn narrower, giving better track overview, but can be changed in toolbox settings (requires reloading the current route if changed to become visible)
	- Track end nodes rendered narrower

## Bug Fixes

- Activity Runner cancels loading and waits for workers before disposing the game, preventing viewer initialization against disposed graphics and input resources when closing during loading.
- Fixing regression in Toolbox to also show invalid Paths (where path nodes are not on track)

## Maintenance

- Update to Monogame 3.8.6.1
- Removing support for contributed software
  - Contrib.DataCollector
  - Contrib.DataConverter
  - Contrib.DataValidator
  - Contrib.TrackViewer

## Known Issues

- Toolbox settings are stored with the current profile only, not copied when cloning profiles