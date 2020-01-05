Open Rails, Monogame version (unofficial) README - Release NewYear - Rev.48
January 5th, 2020

Please note that the installation and use of Open Rails software, even of its unofficial versions, is governed by the Open Rails End User License Agreement. 

INSTALLATION
- the requirements for installation of the official Open Rails version apply, with the precisions of next lines
- XNA 3.1 Redistributable is not needed
- you must have at least a Windows Vista computer. Windows XP is not supported
- start openrails simply by clicking on Openrails.exe
- don't try to update the pack by using the link on the upper right side of the main menu window: 
you would return to the official OR version.

RELEASE NOTES
This unofficial version has been derived from the official Open Rails unstable revision U2020.01.05-0906 (which includes Monogame)
and from the official OpenRails testing revision X1.3.1-111.
It includes some features not (yet) available in the Open Rails unstable official version, that is:
- addition of track sounds in the sound debug window (by dennisat)
- F5 HUD scrolling (by mbm_or)
- checkbox in General Options tab to enable or disable watchdog
- increase of remote horn sound volume level
- when car is selected through the F9 window, the car's brake line in the extended brake HUD is highlighted in yellow (by mbm_or)
- improved distance management in roadcar camera
- signal script parser (by perpetualKid): reduces CPU time needed for signal management
- true 64-bit management, allowing to use more than 4 GB of memory, if available, in Win64 systems (mainly by perpetualKid)
- general options checkbox for optional run at 32 bit on Win64 (to avoid slight train shaking bug)
- added translatable Train Driving Info window (see http://www.elvastower.com/forums/index.php?/topic/33401-f5-hud-scrolling/page__view__findpost__p__251671 and following posts), by mbm_OR
- 32bit running set as default, to avoid 64 bit shakings and flickerings
- inserted extended Raildriver setup, as present in OR Ultimate (by perpetualKid)
- set Simple Control and Physics option as default
- NEW: improvements in the Train Driving Info window, by mbm_OR
- NEW: bug fix for https://bugs.launchpad.net/or/+bug/1858298 Sound problems when viewer not created in initial conditions
- NEW: bug fix for https://bugs.launchpad.net/or/+bug/1858323 Unjustified player train initialization differences 

Various bug fixes have been introduced in parallel to the unstable release.

CREDITS
This unofficial version couldn't have been created without following contributions:
- the whole Open Rails Development Team and Open Rails Management Team, that have generated the official Open Rails version
- the Monogame Development Team
- Peter Gulyas, who created the first Monogame version of Open Rails
- perpetualKid
- Dennis A T (dennisat)
- Mauricio (mbm_OR)
- Peter Newell (steamer_ctn)
- Rob Roeterdink (roeter)
- Carlo Santucci

- all those who contributed with ideas and provided contents for testing and pointed to malfunctions.

DISCLAIMER
No testing on a broad base of computer configurations and of contents has been done. Therefore, in addition
to the disclaimers valid also for the official Open Rails version, 
the above named persons keep no responsibility, including on malfunctions, damages, losses of data or time.
It is reminded that Open Rails is distributed WITHOUT ANY WARRANTY, and without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.

