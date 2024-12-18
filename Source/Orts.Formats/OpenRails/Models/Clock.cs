using System;
using System.Collections.Generic;
using System.IO;

using FreeTrainSimulator.Common;

using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.OpenRails.Models
{
    public class ClockList : List<Clock>
    {
        public ClockList(STFReader stf, string shapePath)
        {
            ArgumentNullException.ThrowIfNull(stf, nameof(stf));
            var count = stf.ReadInt(null);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("clockitem", ()=>{
                        if (--count < 0)
                            STFException.TraceWarning(stf, "Skipped extra ClockItem");
                        else
                        {
                            var dataItem = new Clock(stf, shapePath);
                            if (File.Exists(dataItem.Name))
                                Add(dataItem);
                            else
                                STFException.TraceWarning(stf, $"Non-existent shape file {dataItem.Name} referenced");
                        }
                    }),
                });
            if (count > 0)
                STFException.TraceWarning(stf, count + " missing ClockItem(s)");
        }
    }

    public class Clock
    {
        public string Name { get; private set; }                                    //sFile of OR-Clock
        public ClockType ClockType { get; private set; }

        //Type of OR-Clock -> analog, digital
        public Clock(STFReader stf, string shapePath)
        {
            ArgumentNullException.ThrowIfNull(stf, nameof(stf));
            stf.MustMatch("(");
            Name = Path.Combine(shapePath, stf.ReadString());
            if (EnumExtension.GetValue(stf.ReadString(), out ClockType type))
                ClockType = type;
            stf.SkipRestOfBlock();
        }
    }


}
