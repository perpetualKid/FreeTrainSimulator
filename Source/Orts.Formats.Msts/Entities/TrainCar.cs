using System.Collections.Generic;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Entities
{
    public class WagonList
    {
        public List<WorkOrderWagon> WorkOrderWagonList { get; private set; } = new List<WorkOrderWagon>();
        private uint uid;

        public WagonList(STFReader stf, EventType eventType)
        {
            stf.MustMatch("(");
            // "Drop Off" Wagon_List sometimes lacks a Description attribute, so we create the wagon _before_ description
            // is parsed. Bad practice, but not very dangerous as each Description usually repeats the same data.
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("uid", ()=>
                {
                    uid = stf.ReadUIntBlock(null);
                }),
                new STFReader.TokenProcessor("sidingitem", ()=>
                {
                    WorkOrderWagonList.Add(new WorkOrderWagon(uid, stf.ReadUIntBlock(null)));
                }),
                new STFReader.TokenProcessor("description", ()=>
                {
                    WorkOrderWagonList[WorkOrderWagonList.Count-1].Description = stf.ReadStringBlock("");
                }),
            });
        }
    }

    /// <summary>
    /// Parses a wagon from the WagonList.
    /// Do not confuse with older class Wagon below, which parses TrainCfg from the *.con file.
    /// </summary>
    public class WorkOrderWagon
    {
        public uint UId { get; private set; }        
        public uint SidingId { get; private set; }   
        public string Description { get; internal set; } = "";   // Value assumed if property not found.

        public WorkOrderWagon(uint uid, uint sidingId)
        {
            UId = uid;
            SidingId = sidingId;
        }
    }
}
