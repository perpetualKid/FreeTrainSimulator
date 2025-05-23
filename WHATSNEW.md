# New in this version

## Features

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

- Update to Monogame 3.8.3
- Removing support for contributed software
  - Contrib.DataCollector
  - Contrib.DataConverter
  - Contrib.DataValidator

## Known Issues

- Toolbox settings are also stored with the current profile, but not copied when cloning profiles