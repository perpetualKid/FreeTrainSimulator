# FTS implementation notes for the MSTS track database

The format schema, MSTS domain knowledge, compatibility evidence, and import behavior are documented in the wiki's [Track Database (`.tdb`)](https://github.com/perpetualKid/FreeTrainSimulator/wiki/MSTS-Track-Database) and [Track Sections (`tsection.dat`)](https://github.com/perpetualKid/FreeTrainSimulator/wiki/MSTS-Track-Sections) pages.

## FTS invariant

Each source track item must produce one imported item at the same index: `TrackItemIndex == array position`. TDB references and platform/siding linking depend on this identity.

## Known gap

[`ConvertTrackItems`](../../FreeTrainSimulator.Models.Imported/ImportHandler/TrainSimulator/TrackModelImportHandler.cs#L257) can skip adding an output for an item with no location owned by a non-vector node ([line 298](../../FreeTrainSimulator.Models.Imported/ImportHandler/TrainSimulator/TrackModelImportHandler.cs#L298)) and for an unsupported item type ([line 468](../../FreeTrainSimulator.Models.Imported/ImportHandler/TrainSimulator/TrackModelImportHandler.cs#L468)). Either skip shifts later items out of alignment; pair-linking can then overwrite the wrong item or fail. The first branch's comment says it should add an empty placeholder, but does not.

## Review hook

When changing the importer, preserve one output entry per source item. Test that array length and each `TrackItemIndex` remain aligned, including later references or platform/siding links after an unsupported or unrecoverable item. Keep broader MSTS claims and format details on the wiki, scoped to their evidence and producer/version.
