using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public class WaterLayer
    {
        public float Height { get; private set; }
        public string TextureName { get; private set; }

        public WaterLayer(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("world_water_layer_height", ()=>{ Height = stf.ReadFloatBlock(STFReader.Units.Distance, null); }),
                new STFReader.TokenProcessor("world_anim_shader", ()=>{ stf.MustMatch("("); stf.ParseBlock(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("world_shader", ()=>{ stf.MustMatch("("); stf.ReadString()/*TextureMode*/; stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("terrain_texslots", ()=>{ stf.MustMatch("("); stf.ReadInt(null)/*Count*/; stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("terrain_texslot", ()=>{ stf.MustMatch("("); TextureName = stf.ReadString(); stf.SkipRestOfBlock(); }),
                            });}),
                        });}),
                    });}),
                });
        }
    }

    public class SkyLayer
    {
        public string Fadein_Begin_Time { get; private set; }
        public string Fadein_End_Time { get; private set; }
        public string TextureName { get; private set; }
        public string TextureMode { get; private set; }
        public float TileX { get; private set; }
        public float TileY { get; private set; }


        public SkyLayer(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("world_sky_layer_fadein", ()=>{ stf.MustMatch("("); Fadein_Begin_Time = stf.ReadString(); Fadein_End_Time = stf.ReadString(); stf.SkipRestOfBlock();}),
                        new STFReader.TokenProcessor("world_anim_shader", ()=>{ stf.MustMatch("("); stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("world_anim_shader_frames", ()=>{ stf.MustMatch("("); stf.ParseBlock(new STFReader.TokenProcessor[] {
                                new STFReader.TokenProcessor("world_anim_shader_frame", ()=>{ stf.MustMatch("("); stf.ParseBlock(new STFReader.TokenProcessor[] {
                                    new STFReader.TokenProcessor("world_anim_shader_frame_uvtiles", ()=>{ stf.MustMatch("("); TileX = stf.ReadFloat(STFReader.Units.Any, 1.0f); TileY = stf.ReadFloat(STFReader.Units.Any, 1.0f); stf.ParseBlock(new STFReader.TokenProcessor[] {
                                    });}),
                                });}),
                            });}),
                            new STFReader.TokenProcessor("world_shader", ()=>{ stf.MustMatch("("); TextureMode = stf.ReadString(); stf.ParseBlock(new STFReader.TokenProcessor[] {
                                new STFReader.TokenProcessor("terrain_texslots", ()=>{ stf.MustMatch("("); stf.ReadInt(null)/*Count*/; stf.ParseBlock(new STFReader.TokenProcessor[] {
                                    new STFReader.TokenProcessor("terrain_texslot", ()=>{ stf.MustMatch("("); TextureName = stf.ReadString(); stf.SkipRestOfBlock(); }),
                                    });}),
                                });}),
                            });}),
            });

        }
    }

    public class SkySatellite
    {

        public string TextureName { get; private set; }
        public string TextureMode { get; private set; }

        public SkySatellite(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("world_anim_shader", ()=>{ stf.MustMatch("("); stf.ParseBlock(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("world_shader", ()=>{ stf.MustMatch("("); TextureMode = stf.ReadString(); stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("terrain_texslots", ()=>{ stf.MustMatch("("); stf.ReadInt(null)/*Count*/; stf.ParseBlock(new STFReader.TokenProcessor[] {
                            new STFReader.TokenProcessor("terrain_texslot", ()=>{ stf.MustMatch("("); TextureName = stf.ReadString(); stf.SkipRestOfBlock(); }),
                            });}),
                        });}),
                    });}),
                });
        }
    }
}
