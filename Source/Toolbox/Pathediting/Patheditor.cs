
using System.Linq;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Models.Simplified;
using Orts.Toolbox;



namespace Orts.Toolbox.Pathediting
{
    internal class Patheditor
    {

        /// <summary>
        /// Delegate definition to allow adding events for when a path is changed.
        /// </summary>
        public delegate void ChangedPathHandler();


        #region Public members
        /// <summary>The current path that we are editing</summary>
        public Trainpath CurrentTrainPath { get; private set; }
        /// <summary>Editing is active or not</summary>
        public bool EditingIsActive
        {
            get => _editingIsActive;
            set { _editingIsActive = value; OnActiveOrPathChanged(); }
        }

        /// <summary>Name of the file with the .pat definition</summary>
        public string FileName { get; private set; }
        /// <summary>Does the editor have a path</summary>
        public bool HasValidPath => CurrentTrainPath.FirstNode != null;
        /// <summary>Does the editor have a path that is broken</summary>
        public bool HasBrokenPath => CurrentTrainPath.IsBroken;
        /// <summary>Does the editor have a path that has an end</summary>
        public bool HasEndingPath => CurrentTrainPath.HasEnd;
        /// <summary>Does the editor have a path that has been modified</summary>
        public bool HasModifiedPath => CurrentTrainPath.IsModified;
        /// <summary>Does the editor have a path that has a stored tail</summary>
        public bool HasStoredTail => CurrentTrainPath.FirstNodeOfTail != null;

        /// <summary>A description of the current action that will be done when the mouse is clicked</summary>
        public string CurrentActionDescription { get; private set; } = "";

        // some redirections to the drawPath
        /// <summary>Return current node (last drawn) node</summary>
        //public TrainpathNode CurrentNode => drawPath.CurrentMainNode;
        /// <summary>Return the location of the current (last drawn) node</summary>
        //public WorldLocation CurrentLocation => CurrentNode != null ? CurrentNode.Location : WorldLocation.None;
        #endregion

        #region Private members

        private readonly TrackDB trackDB;
        private readonly TrackSectionsFile tsectionDat;

        private const int practicalInfinityInt = int.MaxValue / 2; // large, but not close to overflow
        private int numberToDraw = practicalInfinityInt; // number of nodes to draw, start with all

        // Editor actions that are not via the menu
        //private EditorActionNonInteractive nonInteractiveAction;

        // Editor actions that are via keyboard commands

        private bool _editingIsActive;
        

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor. This will actually load the .pat from file and create menus as needed
        /// </summary>
        /// <param name="routeData">The route information that contains track data base and track section data</param>
        /// <param name="drawTrackDB">The drawn tracks to know about where the mouse is</param>
        /// <param name="path">Path to the .pat file</param>
        public Patheditor(Path path)
        {
            trackDB = RuntimeData.Instance.TrackDB;
            tsectionDat = RuntimeData.Instance.TSectionDat;

            TrackExtensions.Initialize(trackDB.TrackNodes, tsectionDat); // we might be calling this more than once, but so be it.

            FileName = path.FilePath.Split('\\').Last();
            CurrentTrainPath = new Trainpath(trackDB, tsectionDat, path.FilePath);
            EditingIsActive = false;
            OnPathChanged();
        }
        #endregion

        #region Context menu and its callbacks

        private void UpdateAfterEdits(int nodesAdded)
        {
            numberToDraw += nodesAdded;
            OnPathChanged();
        }

        #endregion

        #region Drawing, active node & track location

        #endregion

        #region Actions to take when editing is first enabled

        /// <summary>
        /// Once the editing becomes active for this path, we make sure the path is 'clean' according to our standards
        /// </summary>
        private void OnActiveOrPathChanged()
        {
            if (!EditingIsActive)
            { return; }
            SnapAllJunctionNodes();
            AddMissingDisambiguityNodes();
            OnPathChanged();
        }

        /// <summary>
        /// Make sure the junction nodes of have the exact location of the junctions in the track database.
        /// This is to make sure changes in the track database are taken over in the path
        /// </summary>
        private void SnapAllJunctionNodes()
        {
            TrainpathNode mainNode = CurrentTrainPath.FirstNode;
            while (mainNode != null)
            {
                //siding path. For this routine we do not care if junctions are done twice
                TrainpathNode sidingNode = mainNode.NextSidingNode;
                while (sidingNode != null)
                {
                    TrainpathJunctionNode sidingNodeAsJunction = sidingNode as TrainpathJunctionNode;
                    if ((sidingNodeAsJunction != null) && !sidingNode.IsBroken)
                    {
                        sidingNode.Location = trackDB.TrackNodes[sidingNodeAsJunction.JunctionIndex].UiD.Location;
                    }
                    sidingNode = sidingNode.NextSidingNode;
                }

                TrainpathJunctionNode mainNodeAsJunction = mainNode as TrainpathJunctionNode;
                if ((mainNodeAsJunction != null) && !mainNode.IsBroken)
                {
                    mainNode.Location = trackDB.TrackNodes[mainNodeAsJunction.JunctionIndex].UiD.Location;
                }
                mainNode = mainNode.NextMainNode;
            }
        }

        /// <summary>
        /// Not all paths have enough disambiguity nodes to distinghuish between two possible paths. Here we add them
        /// </summary>
        private void AddMissingDisambiguityNodes()
        {
 //           nonInteractiveAction.AddMissingDisambiguityNodes(CurrentTrainPath, UpdateAfterEdits);
        }


        #endregion

        #region Metadata, saving, reversing, fix all

        #endregion

        #region Extending and reducing path drawing

        #endregion

        #region Undo / Redo

        #endregion

        #region Events

        /// <summary>
        /// Event to be called whenever the path has changed
        /// </summary>
        public event ChangedPathHandler ChangedPath;

        private void OnPathChanged()
        {
            ChangedPath?.Invoke();
        }

        #endregion
    }
}
