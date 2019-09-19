using System.IO;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Entities
{
    public class WaterLevelOffset
    {
        public float SW { get; private set; }
        public float SE { get; private set; }
        public float NE { get; private set; }
        public float NW { get; private set; }

        public WaterLevelOffset(SBR block)
        {
            block.VerifyID(TokenID.terrain_water_height_offset);
            if (!block.EndOfBlock())
                SW = block.ReadFloat();
            if (!block.EndOfBlock())
                SE = block.ReadFloat();
            if (!block.EndOfBlock())
                NE = block.ReadFloat();
            if (!block.EndOfBlock())
                NW = block.ReadFloat();
        }
    }

    public class Terrain
    {
        public float ErrorThresholdScale { get; private set; } = 1;
        public WaterLevelOffset WaterLevelOffset { get; private set; }
        public float AlwaysSelectMaxDistance { get; private set; }
        public Samples Samples { get; private set; }
        public Shader[] Shaders { get; private set; }
        public PatchSet[] Patchsets { get; private set; }

        public Terrain(SBR block)
        {
            block.VerifyID(TokenID.terrain);
            while (!block.EndOfBlock())
            {
                using (var subBlock = block.ReadSubBlock())
                {
                    switch (subBlock.ID)
                    {
                        case TokenID.terrain_errthreshold_scale:
                            ErrorThresholdScale = subBlock.ReadFloat();
                            break;
                        case TokenID.terrain_water_height_offset:
                            WaterLevelOffset = new WaterLevelOffset(subBlock);
                            break;
                        case TokenID.terrain_alwaysselect_maxdist:
                            AlwaysSelectMaxDistance = subBlock.ReadFloat();
                            break;
                        case TokenID.terrain_samples:
                            Samples = new Samples(subBlock);
                            break;
                        case TokenID.terrain_shaders:
                            Shaders = new Shader[subBlock.ReadInt()];
                            for (var i = 0; i < Shaders.Length; ++i)
                                using (var terrain_shadersBlock = subBlock.ReadSubBlock())
                                    Shaders[i] = new Shader(terrain_shadersBlock);
                            if (!subBlock.EndOfBlock())
                                subBlock.Skip();
                            break;
                        case TokenID.terrain_patches:
                            using (var patch_sets_Block = subBlock.ReadSubBlock())
                            {
                                Patchsets = new PatchSet[patch_sets_Block.ReadInt()];
                                for (var i = 0; i < Patchsets.Length; ++i)
                                    using (var terrain_patchsetBlock = patch_sets_Block.ReadSubBlock())
                                        Patchsets[i] = new PatchSet(terrain_patchsetBlock);
                                if (!subBlock.EndOfBlock())
                                    subBlock.Skip();
                            }
                            break;
                    }
                }
            }
        }
    }

    // TODO fails on = "c:\\program files\\microsoft games\\train simulator\\ROUTES\\EUROPE1\\Tiles\\-11cc0604.t") Line 330 + 0x18 bytes	C#

    public class Samples
    {
        public int SampleCount { get; private set; }
        public float SampleRotation { get; private set; }
        public float SampleFloor { get; private set; }
        public float SampleScale { get; private set; }
        public float SampleSize { get; private set; }
        public string SampleBufferY { get; private set; }
        public string SampleBufferE { get; private set; }
        public string SampleBufferN { get; private set; }

        public Samples(SBR block)
        {
            block.VerifyID(TokenID.terrain_samples);
            while (!block.EndOfBlock())
            {
                using (var subBlock = block.ReadSubBlock())
                {
                    switch (subBlock.ID)
                    {
                        case TokenID.terrain_nsamples:
                            SampleCount = subBlock.ReadInt();
                            break;
                        case TokenID.terrain_sample_rotation:
                            SampleRotation = subBlock.ReadFloat();
                            break;
                        case TokenID.terrain_sample_floor:
                            SampleFloor = subBlock.ReadFloat();
                            break;
                        case TokenID.terrain_sample_scale:
                            SampleScale = subBlock.ReadFloat();
                            break;
                        case TokenID.terrain_sample_size:
                            SampleSize = subBlock.ReadFloat();
                            break;
                        case TokenID.terrain_sample_ybuffer:
                            SampleBufferY = subBlock.ReadString();
                            break;
                        case TokenID.terrain_sample_ebuffer:
                            SampleBufferE = subBlock.ReadString();
                            break;
                        case TokenID.terrain_sample_nbuffer:
                            SampleBufferN = subBlock.ReadString();
                            break;
                        case TokenID.terrain_sample_asbuffer:
                            subBlock.Skip(); // TODO parse this
                            break;
                        case TokenID.terrain_sample_fbuffer:
                            subBlock.Skip(); // TODO parse this
                            break;
                        case (TokenID)282:  // TODO figure out what this is and handle it
                            subBlock.Skip();
                            break;
                        default:
                            throw new InvalidDataException("Unknown token " + subBlock.ID.ToString());
                    }
                }
            }
        }
    }

    public class TextureSlot
    {
        public string Filename { get; private set; }
        public int A { get; private set; }
        public int B { get; private set; }

        public TextureSlot(SBR block)
        {
            block.VerifyID(TokenID.terrain_texslot);
            Filename = block.ReadString();
            A = block.ReadInt();
            B = block.ReadInt();
            block.Skip();
        }
    }

    public class UVCalc
    {
        public int A { get; private set; }
        public int B { get; private set; }
        public int C { get; private set; }
        public int D { get; private set; }

        public UVCalc(SBR block)
        {
            block.VerifyID(TokenID.terrain_uvcalc);
            A = block.ReadInt();
            B = block.ReadInt();
            C = block.ReadInt();
            D = (int)block.ReadFloat();
        }
    }

    public class Patch
    {
        public uint Flags { get; private set; }  // 1 = don't draw, C0 = draw water
        public float CenterX { get; private set; }
        public float CenterZ { get; private set; }
        public float AverageY { get; private set; }
        public float RangeY { get; private set; }
        public float FactorY { get; private set; }  // don't really know the purpose of these
        public int ShaderIndex { get; private set; }
        public float ErrorBias { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public float W { get; private set; }
        public float H { get; private set; }
        public float B { get; private set; }
        public float C { get; private set; }  // texture coordinates
        public float RadiusM { get; private set; }

        public bool WaterEnabled { get { return (Flags & 0xC0) != 0; } }
        public bool DrawingEnabled { get { return (Flags & 1) == 0; } }

        public Patch(SBR block)
        {
            block.VerifyID(TokenID.terrain_patchset_patch);
            Flags = block.ReadUInt();
            CenterX = block.ReadFloat();    // 64
            AverageY = block.ReadFloat();   // 299.9991
            CenterZ = block.ReadFloat();    // -64
            FactorY = block.ReadFloat();    // 99.48125
            RangeY = block.ReadFloat();     // 0
            RadiusM = block.ReadFloat();    // 64
            ShaderIndex = block.ReadInt();  // 0 , 14, 6 etc  TODO, I think there is something wrong here
            X = block.ReadFloat();   // 0.001953 or 0.998 or 0.001  (1/512, 511/512, 1/1024) typically, but not always
            Y = block.ReadFloat();   // 0.001953 or 0.998 or 0.001
            W = block.ReadFloat();	 // 0.06225586 0 -0.06225586  (255/256)/16
            B = block.ReadFloat();	 // 0.06225586 0 -0.06225586  
            C = block.ReadFloat();   // 0.06225586 0 -0.06225586  
            H = block.ReadFloat();   // 0.06225586 0 -0.06225586  
            ErrorBias = block.ReadFloat();  // 0 - 1
        }
    }

    public class PatchSet
    {
        public int Distance { get; private set; }
        public int PatchSize { get; private set; }
        public Patch[] Patches { get; private set; }

        public PatchSet(SBR block)
        {
            block.VerifyID(TokenID.terrain_patchset);
            while (!block.EndOfBlock())
            {
                using (var subBlock = block.ReadSubBlock())
                {
                    switch (subBlock.ID)
                    {
                        case TokenID.terrain_patchset_distance:
                            Distance = subBlock.ReadInt();
                            break;
                        case TokenID.terrain_patchset_npatches:
                            PatchSize = subBlock.ReadInt();
                            break;
                        case TokenID.terrain_patchset_patches:
                            Patches = new Patch[PatchSize * PatchSize];
                            for (var i = 0; i < Patches.Length; ++i)
                                Patches[i] = new Patch(subBlock.ReadSubBlock());
                            break;
                    }
                }
            }
        }

        public Patch GetPatch(int x, int z)
        {
            return Patches[z * PatchSize + x];
        }
    }

    public class Shader
    {
        public string Name { get; private set; }
        public TextureSlot[] Textureslots { get; private set; }
        public UVCalc[] UVCalcs { get; private set; }

        public Shader(SBR block)
        {
            block.VerifyID(TokenID.terrain_shader);
            Name = block.ReadString();
            while (!block.EndOfBlock())
            {
                using (var subBlock = block.ReadSubBlock())
                {
                    switch (subBlock.ID)
                    {
                        case TokenID.terrain_texslots:
                            Textureslots = new TextureSlot[subBlock.ReadUInt()];
                            for (var i = 0; i < Textureslots.Length; ++i)
                                Textureslots[i] = new TextureSlot(subBlock.ReadSubBlock());
                            break;
                        case TokenID.terrain_uvcalcs:
                            UVCalcs = new UVCalc[subBlock.ReadUInt()];
                            for (var i = 0; i < UVCalcs.Length; ++i)
                                UVCalcs[i] = new UVCalc(subBlock.ReadSubBlock());
                            break;
                        default:
                            break;
                    }
                }
            }
        }
    }
}
