﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common.Calc;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public abstract class WorldObject: IWorldPosition
    {
        internal protected class PositionHolder
        {
            public Vector3 Location { get; internal protected set; }
            public Quaternion Direction { get; internal protected set; }
            public Matrix3x3 Position { get; internal protected set; }
            public bool LocationSet { get; internal protected set; }
            public bool DirectionSet { get; internal protected set; }
            public bool PositionSet { get; internal protected set; }

            public int TileX { get; internal protected set; }
            public int TileZ { get; internal protected set; }

            /// <summary>
            /// MSTS WFiles represent some location with a position, 3x3 matrix and tile coordinates
            /// This converts it to the ORTS WorldPosition representation
            /// </summary>
            internal static WorldPosition WorldPositionFromMSTSLocation(PositionHolder holder, uint uid)
            {
                if (holder.LocationSet && holder.PositionSet)
                {
                    holder.Location = new Vector3(holder.Location.X, holder.Location.Y, holder.Location.Z * -1);
                    Matrix xnaMatrix = new Matrix(
                        holder.Position.M00, holder.Position.M01, -holder.Position.M02, 0,
                        holder.Position.M10, holder.Position.M11, -holder.Position.M12, 0,
                        -holder.Position.M20, -holder.Position.M21, holder.Position.M22, 0,
                        0, 0, 0, 1);

                    return new WorldPosition(holder.TileX, holder.TileZ, MatrixExtension.Multiply(xnaMatrix, Matrix.CreateTranslation(holder.Location)));
                }
                else if (holder.LocationSet && holder.DirectionSet)
                {
                    holder.Direction = new Quaternion(holder.Direction.X, holder.Direction.Y, holder.Direction.Z * -1, holder.Direction.W);
                    holder.Location = new Vector3(holder.Location.X, holder.Location.Y, holder.Location.Z * -1);
                    return new WorldPosition(holder.TileX, holder.TileZ, MatrixExtension.Multiply(Matrix.CreateFromQuaternion(holder.Direction), Matrix.CreateTranslation(holder.Location)));

                }
                else
                {
                    Trace.TraceWarning($"Scenery object UiD {uid} is missing Matrix3x3 and QDirection");
                    return WorldPosition.None;
                }
            }
        }

        private protected WorldPosition worldPosition;
        public string FileName { get; protected set; }
        public uint UiD { get; protected set; }
        public int DetailLevel { get; protected set; }
        public uint StaticFlags { get; protected set; }

        public ref readonly WorldPosition WorldPosition => ref worldPosition;

        internal void AddOrModifyObj(SBR subBlock)
        {
            PositionHolder holder = new PositionHolder()
            {
                TileX = worldPosition.TileX,
                TileZ = worldPosition.TileZ,
            };
            AddOrModifyObj(subBlock, holder);

            if (holder.LocationSet && (holder.PositionSet || holder.DirectionSet))
                worldPosition = PositionHolder.WorldPositionFromMSTSLocation(holder, UiD);
        }

        private protected virtual void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.FileName:
                    FileName = subBlock.ReadString(); break;
                case TokenID.Position:
                    //Position = new STFPositionItem(subBlock);
                    ReadLocation(subBlock, holder);
                    break;
                case TokenID.QDirection:
                    //QDirection = new STFQDirectionItem(subBlock);
                    ReadDirection(subBlock, holder);
                    break;
                case TokenID.Matrix3x3:
                    //Matrix3x3 = ReadMatrix3x3(subBlock);
                    ReadPosition(subBlock, holder);
                    break;
                case TokenID.VDbId:
                    //ViewDbId =
                    subBlock.ReadUInt(); break;
                case TokenID.StaticFlags:
                    StaticFlags = subBlock.ReadFlags(); break;
                default:
                    subBlock.Skip(); break;
            }
        }

        private protected void ReadBlock(SBR block, int tileX, int tileZ)
        {
            PositionHolder holder = new PositionHolder()
            {
                TileX = tileX,
                TileZ = tileZ,
            };

            while (!block.EndOfBlock())
            {
                using (SBR subBlock = block.ReadSubBlock())
                {
                    if (subBlock.ID == TokenID.UiD)
                        UiD = subBlock.ReadUInt();
                    else
                    {
                        AddOrModifyObj(subBlock, holder);
                    }
                }
            }
            worldPosition = PositionHolder.WorldPositionFromMSTSLocation(holder, UiD);
            if (this is HazardObject hazard)  //remember the Quaternation component
            {
                hazard.Direction = holder.Direction;
            }
        }

        private static void ReadLocation(SBR block, PositionHolder holder)
        {
            block.VerifyID(TokenID.Position);
            holder.Location = new Vector3(block.ReadFloat(), block.ReadFloat(), block.ReadFloat());
            holder.LocationSet = true;
            block.VerifyEndOfBlock();
        }

        private static void ReadDirection(SBR block, PositionHolder holder)
        {
            block.VerifyID(TokenID.QDirection);
            holder.Direction = new Quaternion(block.ReadFloat(), block.ReadFloat(), block.ReadFloat(), block.ReadFloat());
            holder.DirectionSet = true;
            block.VerifyEndOfBlock();
        }

        private static void ReadPosition(SBR block, PositionHolder holder)
        {
            block.VerifyID(TokenID.Matrix3x3);
            holder.Position = new Matrix3x3(block.ReadFloat(), block.ReadFloat(), block.ReadFloat(),
                block.ReadFloat(), block.ReadFloat(), block.ReadFloat(), block.ReadFloat(), block.ReadFloat(), block.ReadFloat());
            holder.PositionSet = true;
            block.VerifyEndOfBlock();
        }

    }

    public class WorldObjects : List<WorldObject>
    {
        private static readonly HashSet<TokenID> UnknownBlockIDs = new HashSet<TokenID>()
        {
            TokenID.VDbIdCount,
            TokenID.ViewDbSphere,
        };

        internal WorldObjects(SBR block, HashSet<TokenID> allowedTokens, int tileX, int tileZ)
        {
            block.VerifyID(TokenID.Tr_Worldfile);
            int detailLevel = 0;
            while (!block.EndOfBlock())
            {
                using (SBR subBlock = block.ReadSubBlock())
                {
                    try
                    {
                        if (allowedTokens == null || allowedTokens.Contains(subBlock.ID))
                        {
                            switch (subBlock.ID)
                            {
                                case TokenID.CollideObject:
                                case TokenID.Static:
                                    Add(new StaticObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.TrackObj:
                                    Add(new TrackObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.CarSpawner:
                                case (TokenID)357:
                                    Add(new CarSpawnerObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.Siding:
                                case (TokenID)361:
                                    Add(new SidingObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.Platform:
                                    Add(new PlatformObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.Forest:
                                    Add(new ForestObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.LevelCr:
                                    Add(new LevelCrossingObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.Dyntrack:
                                case (TokenID)306:
                                    Add(new DynamicTrackObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.Transfer:
                                case (TokenID)363:
                                    Add(new TransferObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.Gantry:
                                case (TokenID)356:
                                    // TODO: Add real handling for gantry objects.
                                    Add(new BasicObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.Pickup:
                                case (TokenID)359:
                                    Add(new PickupObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.Hazard:
                                    //case (TokenID)359:
                                    Add(new HazardObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.Signal:
                                    Add(new SignalObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.Speedpost:
                                    Add(new SpeedPostObject(subBlock, detailLevel, tileX, tileZ));
                                    break;
                                case TokenID.Tr_Watermark:
                                    detailLevel = subBlock.ReadInt();
                                    break;
                                default:
                                    if (!UnknownBlockIDs.Contains(subBlock.ID))
                                    {
                                        UnknownBlockIDs.Add(subBlock.ID);
                                        Trace.TraceWarning("Skipped unknown world block {0} (0x{0:X}) first seen in {1}", subBlock.ID, subBlock.FileName);
                                    }
                                    subBlock.Skip();
                                    break;
                            }
                        }
                        else
                            subBlock.Skip();
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception error)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        Trace.WriteLine(new FileLoadException(subBlock.FileName, error));
                    }
                }
            }
        }

        internal void InsertORSpecificData(SBR block, IReadOnlyCollection<TokenID> allowedTokens)
        {
            block.VerifyID(TokenID.Tr_Worldfile);
            while (!block.EndOfBlock())
            {
                using (SBR subBlock = block.ReadSubBlock())
                {
                    try
                    {
                        if (allowedTokens == null || allowedTokens.Contains(subBlock.ID))
                        {
                            WorldObject origObject;
                            bool wrongBlock = false;
                            if (!subBlock.EndOfBlock())
                            {
                                SBR subSubBlockUID = subBlock.ReadSubBlock();
                                // check if a block with this UiD already present
                                if (subSubBlockUID.ID == TokenID.UiD)
                                {
                                    uint UID = subSubBlockUID.ReadUInt();
                                    origObject = Find(x => x.UiD == UID);
                                    if (origObject == null)
                                    {
                                        wrongBlock = true;
                                        Trace.TraceWarning("Skipped world block {0} (0x{0:X}), UID {1} not matching with base file", subBlock.ID, UID);
                                        subSubBlockUID.Skip();
                                        subBlock.Skip();
                                    }
                                    else
                                    {
                                        wrongBlock = !TestMatch(subBlock, origObject);
                                        if (!wrongBlock)
                                        {
                                            subSubBlockUID.Skip();
                                            while (!subBlock.EndOfBlock() && !wrongBlock)
                                            {
                                                using (SBR subSubBlock = subBlock.ReadSubBlock())
                                                {
                                                    origObject.AddOrModifyObj(subSubBlock);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Trace.TraceWarning("Skipped world block {0} (0x{0:X}), UID {1} not matching with base file", subBlock.ID, UID);
                                            subSubBlockUID.Skip();
                                            subBlock.Skip();
                                        }
                                    }
                                }

                            }
                            subBlock.EndOfBlock();
                        }
                        else
                        {
                            subBlock.Skip();
                        }
                    }
                    catch (Exception error) when (error is Exception)
                    {
                        Trace.WriteLine(new FileLoadException(block.FileName, error));
                    }
                }
            }
        }

        private static bool TestMatch(SBR subBlock, WorldObject origObject)
        {
            if (subBlock.ID == TokenID.Static && origObject is StaticObject) return true;
            if (subBlock.ID == TokenID.Gantry && origObject is BasicObject) return true;
            if (subBlock.ID == TokenID.Pickup && origObject is PickupObject) return true;
            if (subBlock.ID == TokenID.Transfer && origObject is TransferObject) return true;
            if (subBlock.ID == TokenID.Forest && origObject is ForestObject) return true;
            if (subBlock.ID == TokenID.Signal && origObject is SignalObject) return true;
            if (subBlock.ID == TokenID.Speedpost && origObject is SpeedPostObject) return true;
            if (subBlock.ID == TokenID.LevelCr && origObject is LevelCrossingObject) return true;
            if (subBlock.ID == TokenID.Hazard && origObject is HazardObject) return true;
            if (subBlock.ID == TokenID.CarSpawner && origObject is CarSpawnerObject) return true;
            return false;
        }
    }

    public class BasicObject : WorldObject
    {
        public BasicObject()
        {
        }

        internal BasicObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;

            ReadBlock(block, tileX, tileZ);
        }
    }

    public class StaticObject : BasicObject
    {
        public StaticObject(SBR block, int detailLevel, int tileX, int tileZ)
            : base(block, detailLevel, tileX, tileZ)
        {
        }
    }

    public class SignalUnits : List<SignalUnit>
    {
        internal SignalUnits(SBR block)
        {
            block.VerifyID(TokenID.SignalUnits);
            uint count = block.ReadUInt();
            for (int i = 0; i < count; i++)
            {
                using (SBR subBlock = block.ReadSubBlock())
                {
                    Add(new SignalUnit(subBlock));
                }
            }
            block.VerifyEndOfBlock();
        }
    }

    public class SignalUnit
    {
        public int SubObject { get; private set; }
        public int TrackItem { get; private set; }

        internal SignalUnit(SBR block)
        {
            block.VerifyID(TokenID.SignalUnit);
            SubObject = block.ReadInt();
            using (SBR subBlock = block.ReadSubBlock())
            {
                subBlock.VerifyID(TokenID.TrItemId);
                subBlock.ReadUInt(); // Unk?
                TrackItem = subBlock.ReadInt();
                subBlock.VerifyEndOfBlock();
            }
            block.VerifyEndOfBlock();
        }
    }

    /// <summary>
    /// Pickup objects supply fuel (diesel, coal) or water.
    /// </summary>
    public class PickupObject : BasicObject
    {
        public PickupType PickupType { get; private set; }
        public AnimationData Options { get; private set; }
        /// <summary>
        /// SpeedRangeItem specifies the acceptable range of speeds (meters/sec) for using a pickup.
        /// Presumably non-zero speeds are intended for water troughs or, perhaps, merry-go-round freight.
        /// </summary>
        public Range SpeedRange { get; private set; }
        public CapacityData Capacity { get; private set; }
        public TrackItems TrackItemIds { get; } = new TrackItems();
        public uint CollideFlags { get; private set; }
        public int MaxStackedContainers { get; private set; }
        public float StackLocationsLength { get; private set; } = 12.19f;
        public StackLocationItems StackLocations { get; private set; }
        public float PickingSurfaceYOffset { get; private set; }
        public Vector3 PickingSurfaceRelativeTopStartPosition { get; private set; }
        public int GrabberArmsParts { get; private set; } = 2;
        public string CraneSound { get; private set; }

        /// <summary>
        /// Creates the object, but currently skips the animation field.
        /// </summary>
        internal PickupObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;
            ReadBlock(block, tileX, tileZ);
        }

        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.SpeedRange: SpeedRange = new Range(subBlock, subBlock.ID); break;
                case TokenID.PickupType:
                    PickupType = (PickupType)subBlock.ReadUInt();
                    subBlock.Skip(); // Discard the 2nd value (0 or 1 but significance is not known)
                    break;
                case TokenID.OrtsMaxStackedContainers:
                    MaxStackedContainers = subBlock.ReadInt();
                    break;
                case TokenID.OrtsStackLocationsLength:
                    StackLocationsLength = subBlock.ReadFloat();
                    break;
                case TokenID.OrtsStackLocations:
                    StackLocations = new StackLocationItems(subBlock, this);
                    break;
                case TokenID.OrtsPickingSurfaceYOffset:
                    PickingSurfaceYOffset = subBlock.ReadFloat();
                    break;
                case TokenID.OrtsPickingSurfaceRelativeTopStartPosition:
                    PickingSurfaceRelativeTopStartPosition = subBlock.ReadVector3();
                    break;
                case TokenID.OrtsGrabberArmsParts:
                    GrabberArmsParts = subBlock.ReadInt();
                    break;
                case TokenID.OrtsCraneSound:
                    CraneSound = subBlock.ReadString();
                    break;
                case TokenID.PickupAnimData: Options = new AnimationData(subBlock); break;
                case TokenID.PickupCapacity: Capacity = new CapacityData(subBlock); break;
                case TokenID.TrItemId: TrackItemIds.Add(subBlock); break;
                case TokenID.CollideFlags: CollideFlags = subBlock.ReadUInt(); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }

        /// <summary>
        /// PickupAnimDataItem specifies 2 values.  The first represents different pickup animation options.
        /// The second represents the animation speed which will be used.
        /// For the moment PickupOptions may not be used.
        /// </summary>
        public class AnimationData
        {
            public float PickupOptions { get; private set; }
            public float AnimationSpeed { get; private set; }

            internal AnimationData(SBR block)
            {
                block.VerifyID(TokenID.PickupAnimData);
                PickupOptions = block.ReadFloat();
                AnimationSpeed = block.ReadFloat();
                if (AnimationSpeed == 0) AnimationSpeed = 1.0f;
                block.VerifyEndOfBlock();
            }
        }

        /// <summary>
        /// Creates the object.
        /// The units of measure have been assumed and, once parsed, the values are not currently used.
        /// </summary>
        public class CapacityData
        {
            public float QuantityAvailableKG { get; private set; }
            public float FeedRateKGpS { get; private set; }

            internal CapacityData(SBR block)
            {
                block.VerifyID(TokenID.PickupCapacity);
                QuantityAvailableKG = (float)Mass.Kilogram.FromLb(block.ReadFloat());
                FeedRateKGpS = (float)Mass.Kilogram.FromLb(block.ReadFloat());
                block.VerifyEndOfBlock();
            }
        }

        public class StackLocationItems
        {
            public IList<StackLocation> Locations { get; private set; }

            public StackLocationItems(SBR block, PickupObject pickupObject)
            {
                Locations = new List<StackLocation>();
                var count = block.ReadUInt();
                for (var i = 0; i < count; i++)
                {
                    using (var subBlock = block.ReadSubBlock())
                    {
                        if (subBlock.ID == TokenID.StackLocation)
                        {
                            Locations.Add(new StackLocation(subBlock, pickupObject));
                        }
                    }
                }
                block.VerifyEndOfBlock();
            }
        }

        public class StackLocation
        {
            public Vector3 Position { get; private set; }
            public int MaxStackedContainers { get; private set; }
            public float Length { get; private set; }
            public bool Flipped { get; private set; }

            public StackLocation(SBR block, PickupObject pickupObject)
            {
                while (!block.EndOfBlock())
                {
                    using (var subBlock = block.ReadSubBlock())
                    {
                        switch (subBlock.ID)
                        {
                            case TokenID.Position:
                                Position = subBlock.ReadVector3();
                                break;
                            case TokenID.MaxStackedContainers:
                                MaxStackedContainers = subBlock.ReadInt();
                                break;
                            case TokenID.Length:
                                Length = subBlock.ReadFloat();
                                break;
                            case TokenID.Flipped:
                                subBlock.ReadInt();
                                Flipped = true;
                                break;
                            default:
                                subBlock.Skip();
                                break;
                        }
                    }
                }
                if (Length == 0)
                    Length = pickupObject.StackLocationsLength;
                if (MaxStackedContainers == 0)
                    MaxStackedContainers = pickupObject.MaxStackedContainers;
            }
        }
    }

    public class TransferObject : WorldObject
    {
        public float Width { get; private set; }
        public float Height { get; private set; }

        internal TransferObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;

            ReadBlock(block, tileX, tileZ);
        }

        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.Width: Width = subBlock.ReadFloat(); break;
                case TokenID.Height: Height = subBlock.ReadFloat(); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }
    }

    public class TrackObject : WorldObject
    {
        private WorldLocation location;
        public int SectionIndex { get; private set; }
        public float Elevation { get; private set; }
        public uint CollideFlags { get; private set; }
        public ref readonly WorldLocation WorldLocation => ref location;

        internal TrackObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;

            ReadBlock(block, tileX, tileZ);
        }

        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.SectionIdx: SectionIndex = subBlock.ReadInt(); break;
                case TokenID.Elevation: Elevation = subBlock.ReadFloat(); break;
                case TokenID.CollideFlags: CollideFlags = subBlock.ReadUInt(); break;
                case TokenID.JNodePosn: ReadLocation(subBlock); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }

        private void ReadLocation(SBR block)
        {
            block.VerifyID(TokenID.JNodePosn);
            location = new WorldLocation(block.ReadInt(), block.ReadInt(), block.ReadFloat(), block.ReadFloat(), block.ReadFloat());
            block.VerifyEndOfBlock();
        }
    }

    public class DynamicTrackObject : WorldObject
    {
        public uint SectionIndex { get; private set; }
        public float Elevation { get; private set; }
        public uint CollideFlags { get; private set; }
#pragma warning disable CA1002 // Do not expose generic lists
        public List<TrackSection> TrackSections { get; private set; }
#pragma warning restore CA1002 // Do not expose generic lists

        internal DynamicTrackObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;

            ReadBlock(block, tileX, tileZ);
        }

        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.SectionIdx: SectionIndex = subBlock.ReadUInt(); break;
                case TokenID.Elevation: Elevation = subBlock.ReadFloat(); break;
                case TokenID.CollideFlags: CollideFlags = subBlock.ReadUInt(); break;
                case TokenID.TrackSections: TrackSections = ReadTrackSections(subBlock); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }

        // DyntrackObj copy constructor with a single TrackSection
        public DynamicTrackObject(DynamicTrackObject source, int trackSetionIndex)
        {
            SectionIndex = source?.SectionIndex ?? throw new ArgumentNullException(nameof(source));
            Elevation = source.Elevation;
            CollideFlags = source.CollideFlags;
            StaticFlags = source.StaticFlags;
            FileName = source.FileName;
            DetailLevel = source.DetailLevel;
            UiD = source.UiD;
            worldPosition = source.WorldPosition;
            TrackSections = new List<TrackSection>() { source.TrackSections[trackSetionIndex] };
        }

        private static List<TrackSection> ReadTrackSections(SBR block)
        {
            List<TrackSection> result = new List<TrackSection>();
            block.VerifyID(TokenID.TrackSections);
            int count = 5;
            while (count-- > 0)
                result.Add(new TrackSection(block.ReadSubBlock()));
            block.VerifyEndOfBlock();
            return result;
        }
    }

    public class ForestObject : WorldObject
    {
        public bool IsYard { get; private set; }
        public string TreeTexture { get; private set; }
        public int Population { get; private set; }
        public Range ScaleRange { get; private set; }
        public Size2D ForestArea { get; private set; }
        public Size2D TreeSize { get; private set; }

        internal ForestObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;

            ReadBlock(block, tileX, tileZ);

            IsYard = TreeTexture == null;
        }


        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.TreeTexture: TreeTexture = subBlock.ReadString(); break;
                case TokenID.ScaleRange: ScaleRange = new Range(subBlock, subBlock.ID); break;
                case TokenID.Area: ForestArea = new Size2D(subBlock, subBlock.ID); break;
                case TokenID.Population: Population = subBlock.ReadInt(); break;
                case TokenID.TreeSize: TreeSize = new Size2D(subBlock, subBlock.ID); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }
    }

    public class SignalObject : WorldObject
    {
        public uint SignalSubObject { get; private set; }
        public SignalUnits SignalUnits { get; private set; }

        internal SignalObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;

            ReadBlock(block, tileX, tileZ);
        }

        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.SignalSubObj: SignalSubObject = subBlock.ReadFlags(); break;
                case TokenID.SignalUnits: SignalUnits = new SignalUnits(subBlock); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }
    }

    public class SpeedPostObject : WorldObject
    {
        public string TextureFile { get; private set; } //ace
        public TextData TextSize { get; private set; }// ( 0.08 0.06 0 )
#pragma warning disable CA1002 // Do not expose generic lists
        public List<Vector4> SignShapes { get; } = new List<Vector4>();
#pragma warning restore CA1002 // Do not expose generic lists
        public TrackItems TrackItemIds { get; } = new TrackItems();

        internal SpeedPostObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;

            ReadBlock(block, tileX, tileZ);

        }

        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.Speed_Digit_Tex: TextureFile = subBlock.ReadString(); break;
                case TokenID.Speed_Sign_Shape: ReadSpeedSignShape(subBlock); break;
                case TokenID.Speed_Text_Size: TextSize = new TextData(subBlock); break;
                case TokenID.TrItemId: TrackItemIds.Add(subBlock); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }

        private void ReadSpeedSignShape(SBR block)
        {
            block.VerifyID(TokenID.Speed_Sign_Shape);
            int numShapes = block.ReadInt();
            for (int i = 0; i < numShapes; i++)
            {
                SignShapes.Add(new Vector4(block.ReadFloat(), block.ReadFloat(), -block.ReadFloat(), block.ReadFloat()));
            }
            block.VerifyEndOfBlock();
        }

        public class TextData
        {
            public float Size { get; private set; }
            public Vector2 Offset { get; private set; }

            internal TextData(SBR block)
            {
                block.VerifyID(TokenID.Speed_Text_Size);
                Size = block.ReadFloat();
                Offset = new Vector2(block.ReadFloat(), block.ReadFloat());
                block.VerifyEndOfBlock();
            }
        }
    }

    public class TrackItems
    {
        internal void Add(SBR block)
        {
            block.VerifyID(TokenID.TrItemId);
            if (block.ReadInt() == 0)
            {
                TrackDbItems.Add(block.ReadInt());
            }
            else
            {
                RoadDbItems.Add(block.ReadInt());
            }
            block.VerifyEndOfBlock();
        }

#pragma warning disable CA1002 // Do not expose generic lists
        public List<int> RoadDbItems { get; } = new List<int>();
        public List<int> TrackDbItems { get; } = new List<int>();
#pragma warning restore CA1002 // Do not expose generic lists
    }

    public class LevelCrossingObject : WorldObject
    {
        public TrackItems TrackItemIds { get; } = new TrackItems();
        public int CrashProbability { get; private set; }
        public bool Visible { get; private set; } = true;
        public bool Silent { get; private set; }
        public string SoundFileName { get; private set; }
        public float InitialTiming { get; private set; }
        public float SeriousTiming { get; private set; }
        public float AnimationTiming { get; private set; }
        public float WarningTime { get; private set; }
        public float MinimumDistance { get; private set; }

        internal LevelCrossingObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;

            ReadBlock(block, tileX, tileZ);
        }

        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.LevelCrParameters: ReadCrossingParameters(subBlock); break;
                case TokenID.CrashProbability: CrashProbability = subBlock.ReadInt(); break;
                case TokenID.LevelCrData: ReadCrossingData(subBlock); break;
                case TokenID.LevelCrTiming: ReadCrossingTiming(subBlock); break;
                case TokenID.TrItemId: TrackItemIds.Add(subBlock); break;
                case TokenID.OrtsSoundFileName: SoundFileName = subBlock.ReadString(); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }

        private void ReadCrossingParameters(SBR block)
        {
            block.VerifyID(TokenID.LevelCrParameters);
            WarningTime = block.ReadFloat();
            MinimumDistance = block.ReadFloat();
            block.VerifyEndOfBlock();
        }

        private void ReadCrossingData(SBR block)
        {
            block.VerifyID(TokenID.LevelCrData);
            int data = block.ReadInt();
            block.ReadInt();    // not used and not known
            block.VerifyEndOfBlock();

            Visible = (data & 0x1) == 0;
            Silent = !Visible || (data & 0x6) == 0x6;
        }

        private void ReadCrossingTiming(SBR block)
        {
            block.VerifyID(TokenID.LevelCrTiming);
            InitialTiming = block.ReadFloat();
            SeriousTiming = block.ReadFloat();
            AnimationTiming = block.ReadFloat();
            block.VerifyEndOfBlock();
        }
    }

    public class HazardObject : WorldObject
    {
        private Vector3 euler;
        public int ItemId { get; private set; }

        internal Quaternion Direction
        {
            set
            {
                value.Conjugate();
                euler = new Vector3((2 * value.Y * value.W + 2 * value.Z * value.X),
                    (2 * value.Z * value.Y - 2 * value.X * value.W),
                    (value.Z * value.Z - value.Y * value.Y - value.X * value.X + value.W * value.W));
            }
        }

        internal HazardObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;

            ReadBlock(block, tileX, tileZ);
        }

        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.TrItemId: ReadTrackItemId(subBlock); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }

        private void ReadTrackItemId(SBR block)
        {
            block.VerifyID(TokenID.TrItemId);
            block.ReadInt();    //don't mind the database id, should be 0 always (TrackDB)
            ItemId = block.ReadInt();
            block.VerifyEndOfBlock();
        }

        public void UpdatePosition(float distance)
        {
            worldPosition = WorldPosition.ChangeTranslation(euler.X * distance, euler.Y * distance, euler.Z * distance);
        }
    }

    public class CarSpawnerObject : WorldObject
    {
        public TrackItems TrackItemIds { get; } = new TrackItems();
        public float CarFrequency { get; private set; }
        public float CarAverageSpeed { get; private set; }
        public string ListName { get; private set; } // name of car list associated to this car spawner
        public int CarSpawnerListIndex { get; set; }

        internal CarSpawnerObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;
            CarFrequency = 5.0f;
            CarAverageSpeed = 20.0f;
            ReadBlock(block, tileX, tileZ);
        }

        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.CarFrequency: CarFrequency = subBlock.ReadFloat(); break;
                case TokenID.CarAvSpeed: CarAverageSpeed = subBlock.ReadFloat(); break;
                case TokenID.OrtsListName: ListName = subBlock.ReadString(); break;
                case TokenID.TrItemId: TrackItemIds.Add(subBlock); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }
    }

    /// <summary>
    /// Super-class for similar track items SidingObj and PlatformObj.
    /// </summary>
    public abstract class StationObject : WorldObject
    {
        public TrackItems TrackItemIds { get; } = new TrackItems();

        // this one called by PlatformObj
        internal StationObject()
        { }

        // this one called by SidingObj
        internal StationObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;

            ReadBlock(block, tileX, tileZ);
        }

        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.TrItemId: TrackItemIds.Add(subBlock); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }
    }

    /// <summary>
    /// Empty sub-class distinguishes siding objects from platform objects.
    /// </summary>
    public class SidingObject : StationObject
    {
        public SidingObject(SBR block, int detailLevel, int tileX, int tileZ) :
            base(block, detailLevel, tileX, tileZ)
        {
        }
    }

    /// <summary>
    /// Empty sub-class distinguishes platform objects from siding objects.
    /// </summary>
    public class PlatformObject : StationObject
    {
        public uint PlatformData { get; private set; }

        internal PlatformObject(SBR block, int detailLevel, int tileX, int tileZ)
        {
            DetailLevel = detailLevel;

            ReadBlock(block, tileX, tileZ);
        }

        private protected override void AddOrModifyObj(SBR subBlock, PositionHolder holder)
        {
            switch (subBlock.ID)
            {
                case TokenID.PlatformData: PlatformData = subBlock.ReadFlags(); break;
                default: base.AddOrModifyObj(subBlock, holder); break;
            }
        }
    }

    public class Range
    {
        public float LowerLimit { get; private set; }
        public float UpperLimit { get; private set; }

        internal Range(SBR block, TokenID expectedToken)
        {
            block.VerifyID(expectedToken);
            LowerLimit = block.ReadFloat();
            UpperLimit = block.ReadFloat();
            block.VerifyEndOfBlock();
        }
    }

    public class Size2D
    {
        public float Width { get; private set; }
        public float Height { get; private set; }

        internal Size2D(SBR block, TokenID expectedToken)
        {
            block.VerifyID(expectedToken);
            Width = block.ReadFloat();
            Height = block.ReadFloat();
            block.VerifyEndOfBlock();
        }
    }
}
