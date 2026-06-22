# New in this version

## Features

- Importing Track Sections (tsection.dat) and Track Database (*.tdb and *.rdb). Used in Toolbox, other applications will follow
- Toolbox has been reworked into a modern, dockable IDE-style desktop application, with movable, floating, auto-hiding, and tabbed tool windows arranged around the central map view:
  - Tool windows for Routes, Settings, Route Navigation, Train Path details, Location, Logging, Help, and Debug information
  - Settings tool window with separate tabs for general options, item colors, and item visibility, plus a "Reset to Defaults" action
  - Route Navigation to center the map on stations, platforms, and sidings by name, or to locate track items and track nodes by index
  - Always-on status bar showing tile, track, and item information under the mouse pointer
  - Window layout, undocked tool window sizes, and main window placement are remembered between sessions and can be reset to a clean default
  - User interface language can be switched at runtime
- Toolbox Path Editor allows to create and save new train paths. This is still work in progres.
- Game will be started based on profile-selections, not passing commandline arguments (still possible though for debugging purposes)
- Multiple configuration profiles, available through Menu application, allow to have all settings profile-dependent
- Log files are stored in the user's application data folder (i.e. C:\Users\USERNAME\AppData\Roaming\Free Train Simulator\Logs). 
- New Menu-toolbar to manage user profiles and access the Log file folder
- Existing content folder settings are imported from OpenRails

## Updates
- Improve text readability for named track items by using fount outlining in contrast color
- Visual enhancements in Toolbox views:
  - Track segments in Toolbox view are drawn narrower, giving better track overview, but can be changed in toolbox settings (requires reloading the current route if changed to become visible)
  - Track end nodes rendered narrower

## Bug Fixes

- Fixing regression in Toolbox to also show invalid Paths (where path nodes are not on track)

## Maintenance

- Update to Monogame 3.8.4.1
- Removing support for contributed software
  - Contrib.DataCollector
  - Contrib.DataConverter
  - Contrib.DataValidator

## Known Issues

- Toolbox settings are also stored with the current profile, but not copied when cloning profiles