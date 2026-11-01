# MSTS domain documentation policy

The [FreeTrainSimulator wiki](https://github.com/perpetualKid/FreeTrainSimulator/wiki/MSTS-Content) is the canonical home for MSTS domain knowledge: file formats, data structures, cross-file relationships, authoring procedures, compatibility behavior, observed variants, and unresolved questions.

The source repository should contain only material needed to understand or review FTS itself:

- Keep implementation notes narrow: document current FTS behavior, invariants, diagnostics, limitations, and relevant code or test locations.
- Link to the corresponding wiki page instead of copying its format schema or general MSTS explanations.
- Distinguish FTS/Open Rails implementation behavior from claims about original MSTS. Cite evidence and scope compatibility claims to the named producer and version.
- When a contribution changes an FTS invariant or reveals a compatibility-relevant behavior, update the implementation note and link the related wiki page. Propose broader domain findings for the wiki and retain their evidence and uncertainty there.
- Review changes to both surfaces for consistency; do not make a source implementation note imply that its behavior is an MSTS requirement.

See [FTS implementation notes for the MSTS track database](track-database.md) and the wiki's [Track Database (`.tdb`)](https://github.com/perpetualKid/FreeTrainSimulator/wiki/MSTS-Track-Database).
