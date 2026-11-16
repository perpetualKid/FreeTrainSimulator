using System;
using System.Collections.Immutable;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    /// <summary>Captures menu entries and their editor ownership together before a menu is displayed.</summary>
    internal sealed class PathEditorMenuRequest
    {
        private readonly PathEditor owner;
        private readonly PathModel sourceModel;
        private readonly long sourceRevision;
        private readonly long interactionRevision;
        private readonly long pathLifetime;
        private readonly PathEditorPlacementMode placementMode;
        private readonly GuidedPassingCandidatePreviewWitness previewWitness;

        /// <summary>Menu entries available to the captured owner, in display order.</summary>
        public ImmutableArray<MapContextMenuItem> Items { get; }

        /// <summary>An empty request when no node or editor is available.</summary>
        public static PathEditorMenuRequest Empty { get; } = new PathEditorMenuRequest(null, null, -1, -1, -1,
            PathEditorPlacementMode.None, null, ImmutableArray<MapContextMenuItem>.Empty);

        /// <summary>Captures entries constructed on the owner's thread; an absent owner produces an empty request.</summary>
        public PathEditorMenuRequest(ImmutableArray<MapContextMenuItem> items, PathEditor owner)
            : this(owner, owner?.TryCaptureCurrentPathModel(), owner?.CompositionRevision ?? -1,
                  owner?.PassingBranchInteractionRevision ?? -1, owner?.PathLifetime ?? -1,
                  owner?.PlacementMode ?? PathEditorPlacementMode.None, owner?.CaptureGuidedPassingCandidatePreviewWitness(), items)
        {
        }

        private PathEditorMenuRequest(PathEditor owner, PathModel sourceModel, long sourceRevision, long interactionRevision, long pathLifetime,
            PathEditorPlacementMode placementMode, GuidedPassingCandidatePreviewWitness previewWitness, ImmutableArray<MapContextMenuItem> items)
        {
            this.owner = owner;
            this.sourceModel = sourceModel;
            this.sourceRevision = sourceRevision;
            this.interactionRevision = interactionRevision;
            this.pathLifetime = pathLifetime;
            this.placementMode = placementMode;
            this.previewWitness = previewWitness;
            Items = owner == null || items.IsDefault ? ImmutableArray<MapContextMenuItem>.Empty : items;
        }

        /// <summary>Freezes ownership before menu capabilities are read, rejecting a changed capture.</summary>
        internal static PathEditorMenuRequest Capture(PathEditor owner)
        {
            if (owner == null || owner.IsStopped)
                return Empty;

            PathEditorMenuRequest request = new PathEditorMenuRequest(ImmutableArray<MapContextMenuItem>.Empty, owner);
            return request.IsCurrent(owner) ? request : Empty;
        }

        /// <summary>Attaches collected entries only to unchanged ownership, preserving the original witness.</summary>
        internal PathEditorMenuRequest WithItems(ImmutableArray<MapContextMenuItem> items, PathEditor currentEditor)
            => IsCurrent(currentEditor)
                ? new PathEditorMenuRequest(owner, sourceModel, sourceRevision, interactionRevision, pathLifetime, placementMode, previewWitness, items)
                : Empty;

        /// <summary>Checks menu-construction ownership on the game thread immediately before dispatching an action.</summary>
        internal bool IsCurrent(PathEditor editor)
            => IsCurrent(editor, false);

        /// <summary>Allows only preview progression within the same captured guided draft when explicitly requested.</summary>
        internal bool IsCurrent(PathEditor editor, bool allowSameDraftPreview)
            => owner != null && editor != null && !editor.IsStopped && ReferenceEquals(owner, editor)
                && pathLifetime == editor.PathLifetime && ReferenceEquals(sourceModel, editor.TryCaptureCurrentPathModel())
                && sourceRevision == editor.CompositionRevision
                && (allowSameDraftPreview && previewWitness != null ? editor.IsCurrentGuidedPassingCandidatePreview(previewWitness)
                    : interactionRevision == editor.PassingBranchInteractionRevision)
                && placementMode == editor.PlacementMode;
    }
}
