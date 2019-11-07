Open Rails, Monogame version (unofficial) README - Release NewYear - Rev.39
November 7th, 2019

Please note the installation and use of Open Rails software, even of its unofficial versions, is governed by the Open Rails End User License Agreement. 

INSTALLATION
- the requirements for installation of the official Open Rails version apply, with the precisions of next lines
- XNA 3.1 Redistributable is not needed
- you must have at least a Windows Vista computer. Windows XP is not supported
- start openrails simply by clicking on Openrails.exe
- don't try to update the pack by using the link on the upper right side of the main menu window: 
you would return to the official OR version.

RELEASE NOTES
This unofficial version has been derived from the official Open Rails unstable revision U2019.10.26-0321 (which includes Monogame).
It includes some features not (yet) available in the Open Rails unstable official version, that is:
- addition of track sounds in the sound debug window (by dennisat)
- F5 HUD scrolling (by mbm_or)
- checkbox in General Options tab to enable or disable watchdog
- increase of remote horn sound volume level
- when car is ( cted through the F9 window, the car's brake line in the extended brake HUD is highlighted in yellow (by mbm_or)
- improved distance management in roadcar camera
- signal script parser (by perpetualKid): reduces CPU time needed for signal management
- true 64-bit management, allowing to use more than 4 GB of memory, if available, in Win64 systems (mainly by perpetualKid)
- general options checkbox for optional run at 32 bit on Win64 (to avoid slight train shaking bug)
- reverted bug fix about rain in 3DCab, to avoid lack of display of digital indicators (see http://www.elvastower.com/forums/index.php?/topic/24040-3d-cabs/page__view__findpost__p__252352 )
- added trace log for precipitation crash (see http://www.elvastower.com/forums/index.php?/topic/33462-potential-problem-with-activity-in-monogame-v36/ )
- NEW: added tentative bug fix for precipitation crash (suggested by roeter)

CREDITS
This unofficial version couldn't have been created without following contributions:
- the whole Open Rails Development Team and Open Rails Management Team, that have generated the official Open Rails version
- the Monogame Development Team
- Peter Gulyas, who created the first Monogame version of Open Rails
- perpetualKid, which now manages the process of refining the MG porting
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

