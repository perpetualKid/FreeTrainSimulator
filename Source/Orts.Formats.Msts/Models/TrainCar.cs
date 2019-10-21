using System.Collections.Generic;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public class WorkOrderWagons: List<WorkOrderWagon>
    {
        public uint UiD { get; private set; }

        public WorkOrderWagons(STFReader stf, EventType eventType)
        {
            stf.MustMatchBlockStart();
            // "Drop Off" Wagon_List sometimes lacks a Description attribute, so we create the wagon _before_ description
            // is parsed. 
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("uid", ()=> { UiD = stf.ReadUIntBlock(null); }),
                new STFReader.TokenProcessor("sidingitem", ()=> { Add(new WorkOrderWagon(UiD, stf.ReadUIntBlock(null))); }),
                new STFReader.TokenProcessor("description", ()=> { this[Count-1].Description = stf.ReadStringBlock(""); }),
            });
        }
    }

    /// <summary>
    /// Parses a wagon from the WagonList.
    /// Do not confuse with older class Wagon below, which parses TrainCfg from the *.con file.
    /// </summary>
    public class WorkOrderWagon
    {
        public uint UiD { get; private set; }
        public uint SidingId { get; private set; }
        public string Description { get; internal set; } = "";   // Value assumed if property not found.

        public WorkOrderWagon(uint uid, uint sidingId)
        {
            UiD = uid;
            SidingId = sidingId;
        }
    }

    public class TrainSet
    {
        public string Name { get; private set; } = "Loose consist.";
        public int Serial { get; private set; } = 1;
        public MaxVelocity MaxVelocity { get; private set; }
        public float Durability { get; private set; } = 1.0f;   // Value assumed if attribute not found.
        private int nextWagonUiD;

        public List<Wagon> Wagons { get; } = new List<Wagon>();


        public TrainSet(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("traincfg", ()=>{ ParseTrainConfig(stf); }),
            });
        }

        private void ParseTrainConfig(STFReader stf)
        {
            stf.MustMatchBlockStart();
            Name = stf.ReadString();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("name", ()=>{ Name = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("serial", ()=>{ Serial = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("maxvelocity", ()=>{ MaxVelocity = new MaxVelocity(stf); }),
                new STFReader.TokenProcessor("nextwagonuid", ()=>{ nextWagonUiD = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("durability", ()=>{ Durability = stf.ReadFloatBlock(STFReader.Units.None, null); }),
                new STFReader.TokenProcessor("wagon", ()=>{ Wagons.Add(new Wagon(stf)); }),
                new STFReader.TokenProcessor("engine", ()=>{ Wagons.Add(new Wagon(stf)); }),
            });
        }
    }

    public class Wagon
    {
        public string Folder { get; private set; }
        public string Name { get; private set; }
        public int UiD { get; private set; }
        public bool IsEngine { get; private set; }
        public bool Flip { get; private set; }

        public Wagon(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("uid", ()=>{ UiD = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("flip", ()=>{ stf.MustMatchBlockStart(); stf.MustMatch(")"); Flip = true; }),
                new STFReader.TokenProcessor("enginedata", ()=>{ stf.MustMatchBlockStart(); Name = stf.ReadString(); Folder = stf.ReadString(); stf.MustMatch(")"); IsEngine = true; }),
                new STFReader.TokenProcessor("wagondata", ()=>{ stf.MustMatchBlockStart(); Name = stf.ReadString(); Folder = stf.ReadString(); stf.MustMatch(")"); }),
            });
        }

        public string GetName(uint uId, List<Wagon> wagonList)
        {
            return wagonList.Find((w) => w.UiD == uId)?.Name ?? "<unknown name>";
        }
    }

}
