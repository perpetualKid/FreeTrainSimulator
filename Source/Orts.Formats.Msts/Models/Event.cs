using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Orts.Common;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    /// <summary>
    /// Parses Event objects and saves them in EventList.
    /// </summary>
    public class Events: List<Event>
    {
        public Events(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("eventcategorylocation", ()=>{ Add(new EventCategoryLocation(stf)); }),
                new STFReader.TokenProcessor("eventcategoryaction", ()=>{ Add(new EventCategoryAction(stf)); }),
                new STFReader.TokenProcessor("eventcategorytime", ()=>{ Add(new EventCategoryTime(stf)); }),
            });
        }

        public void InsertORSpecificData(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("eventcategorylocation", ()=>{ TryModify(0, stf); }),
                new STFReader.TokenProcessor("eventcategoryaction", ()=>{ TryModify(1, stf); }),
                new STFReader.TokenProcessor("eventcategorytime", ()=>{ TryModify(2, stf); }),
            });
        }

        private void TryModify(int category, STFReader stf)
        {
            Event origEvent;
            bool wrongEventID = false;
            int modifiedID = -1;
            try
            {
                stf.MustMatch("(");
                stf.MustMatch("id");
                stf.MustMatch("(");
                modifiedID = stf.ReadInt(null);
                stf.MustMatch(")");
                origEvent = Find(x => x.ID == modifiedID);
                if (origEvent == null)
                {
                    wrongEventID = true;
                    Trace.TraceWarning("Skipped event {0} not present in base activity file", modifiedID);
                    stf.SkipRestOfBlock();
                }
                else
                {
                    wrongEventID = !TestMatch(category, origEvent);
                    if (!wrongEventID)
                    {
                        origEvent.AddOrModifyEvent(stf, Path.GetDirectoryName(stf.FileName));
                    }
                    else
                    {
                        Trace.TraceWarning("Skipped event {0} of event category not matching with base activity file", modifiedID);
                        stf.SkipRestOfBlock();
                    }
                }
            }
            catch (Exception error)
            {
                Trace.WriteLine(new FileLoadException("Error in additional activity file", error));
            }
        }

        private bool TestMatch(int category, Event origEvent)
        {
            return 
                ((category == 0 && origEvent is EventCategoryLocation) ||
                (category == 1 && origEvent is EventCategoryAction) ||
                (category == 2 && origEvent is EventCategoryTime));
        }
    }

    /// <summary>
    /// The 3 types of event are inherited from the abstract Event class.
    /// </summary>
    public abstract class Event
    {
        public int ID { get; protected set; }
        public string Name { get; protected set; }
        public int ActivationLevel { get; set; }
        public Outcomes Outcomes { get; protected set; }
        public string TextToDisplayOnCompletionIfTriggered { get; protected set; } = "";
        public string TextToDisplayOnCompletionIfNotTriggered { get; protected set; } = "";
        public bool Reversible { get; protected set; }
        public int OrtsContinue { get; protected set; } = -1;
        public string OrtsActivitySoundFile { get; protected set; }
        public OrtsActivitySoundFileType OrtsActivitySoundFileType { get; protected set; }
        public ORTSWeatherChange OrtsWeatherChange { get; protected set; }
        public string TrainService { get; protected set; } = "";
        public int TrainStartingTime { get; protected set; } = -1;

        public virtual void AddOrModifyEvent(STFReader stf, string fileName)
        { }

        protected void OrtsActivitySoundProcessor(STFReader stf)
        {
            stf.MustMatch("(");
            string soundFile = stf.ReadString();
            OrtsActivitySoundFile = Path.Combine(FolderStructure.RouteSoundsFolder, soundFile);
            if (!EnumExtension.GetValue(stf.ReadString(), out OrtsActivitySoundFileType soundFileType))
            {
                stf.StepBackOneItem();
                STFException.TraceInformation(stf, "Skipped unknown activity sound file type " + stf.ReadString());
                OrtsActivitySoundFileType = OrtsActivitySoundFileType.None;
            }
            else
                OrtsActivitySoundFileType = soundFileType;
            stf.MustMatch(")");
        }
    }

    public class EventCategoryLocation : Event
    {
        private WorldLocation location;
        public bool TriggerOnStop { get; private set; } // Value assumed if property not found.
        public ref WorldLocation Location => ref location;
        public float RadiusM { get; private set; }

        public EventCategoryLocation(STFReader stf)
        {
            stf.MustMatch("(");
            AddOrModifyEvent(stf, stf.FileName);
        }

        public override void AddOrModifyEvent(STFReader stf, string fileName)
        {
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("eventtypelocation", ()=>{ stf.MustMatch("("); stf.MustMatch(")"); }),
                new STFReader.TokenProcessor("id", ()=>{ ID = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("ortstriggeringtrain", ()=>{ ParseTrain(stf); }),
                new STFReader.TokenProcessor("activation_level", ()=>{ ActivationLevel = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("outcomes", ()=>
                {
                    if (Outcomes == null)
                        Outcomes = new Outcomes(stf, fileName);
                    else
                        Outcomes.CreateOrModifyOutcomes(stf, fileName); }),
                new STFReader.TokenProcessor("name", ()=>{ Name = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("texttodisplayoncompletioniftriggered", ()=>{ TextToDisplayOnCompletionIfTriggered = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("texttodisplayoncompletionifnottriggered", ()=>{ TextToDisplayOnCompletionIfNotTriggered = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("triggeronstop", ()=>{ TriggerOnStop = stf.ReadBoolBlock(true); }),
                new STFReader.TokenProcessor("location", ()=>{
                    stf.MustMatch("(");
                    location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null),
                        stf.ReadFloat(STFReader.Units.None, null), 0f, stf.ReadFloat(STFReader.Units.None, null));
                    RadiusM = stf.ReadFloat(STFReader.Units.Distance, null);
                    stf.MustMatch(")");
                }),
                new STFReader.TokenProcessor("ortscontinue", ()=>{ OrtsContinue = stf.ReadIntBlock(0); }),
                new STFReader.TokenProcessor("ortsactsoundfile", ()=> OrtsActivitySoundProcessor(stf)),
                new STFReader.TokenProcessor("ortsweatherchange", ()=>{ OrtsWeatherChange = new ORTSWeatherChange(stf);}),
            });
        }

        private void ParseTrain(STFReader stf)
        {
            stf.MustMatch("(");
            TrainService = stf.ReadString();
            TrainStartingTime = stf.ReadInt(-1);
            stf.SkipRestOfBlock();
        }
    }

    /// <summary>
    /// Parses all types of action events.
    /// Save type of action event in Type. MSTS syntax isn't fully hierarchical, so using inheritance here instead of Type would be awkward. 
    /// </summary>
    public class EventCategoryAction : Event
    {
        public EventType Type { get; private set; }
        public WagonList WagonList { get; private set; }
        public uint? SidingId { get; private set; }  // May be specified inside the Wagon_List instead. Nullable as can't use -1 to indicate not set.
        public float SpeedMpS { get; private set; }

        public EventCategoryAction(STFReader stf)
        {
            stf.MustMatch("(");
            AddOrModifyEvent(stf, stf.FileName);
        }

        public override void AddOrModifyEvent(STFReader stf, string fileName)
        {
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("eventtypeallstops", ()=>{ stf.MustMatch("("); stf.MustMatch(")"); Type = EventType.AllStops; }),
                new STFReader.TokenProcessor("eventtypeassembletrain", ()=>{ stf.MustMatch("("); stf.MustMatch(")"); Type = EventType.AssembleTrain; }),
                new STFReader.TokenProcessor("eventtypeassembletrainatlocation", ()=>{ stf.MustMatch("("); stf.MustMatch(")"); Type = EventType.AssembleTrainAtLocation; }),
                new STFReader.TokenProcessor("eventtypedropoffwagonsatlocation", ()=>{ stf.MustMatch("("); stf.MustMatch(")"); Type = EventType.DropOffWagonsAtLocation; }),
                new STFReader.TokenProcessor("eventtypepickuppassengers", ()=>{ stf.MustMatch("("); stf.MustMatch(")"); Type = EventType.PickUpPassengers; }),
                new STFReader.TokenProcessor("eventtypepickupwagons", ()=>{ stf.MustMatch("("); stf.MustMatch(")"); Type = EventType.PickUpWagons; }),
                new STFReader.TokenProcessor("eventtypereachspeed", ()=>{ stf.MustMatch("("); stf.MustMatch(")"); Type = EventType.ReachSpeed; }),
                new STFReader.TokenProcessor("id", ()=>{ ID = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("activation_level", ()=>{ ActivationLevel = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("outcomes", ()=>
                {
                    if (Outcomes == null)
                        Outcomes = new Outcomes(stf, fileName);
                    else
                        Outcomes.CreateOrModifyOutcomes(stf, fileName); }),
                new STFReader.TokenProcessor("texttodisplayoncompletioniftriggered", ()=>{ TextToDisplayOnCompletionIfTriggered = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("texttodisplayoncompletionifnotrriggered", ()=>{ TextToDisplayOnCompletionIfNotTriggered = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("name", ()=>{ Name = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("wagon_list", ()=>{ WagonList = new WagonList(stf, Type); }),
                new STFReader.TokenProcessor("sidingitem", ()=>{ SidingId = (uint)stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("speed", ()=>{ SpeedMpS = stf.ReadFloatBlock(STFReader.Units.Speed, null); }),
                new STFReader.TokenProcessor("reversable_event", ()=>{ stf.MustMatch("("); stf.MustMatch(")"); Reversible = true; }),
                // Also support the correct spelling !
                new STFReader.TokenProcessor("reversible_event", ()=>{ stf.MustMatch("("); stf.MustMatch(")"); Reversible = true; }),
                new STFReader.TokenProcessor("ortscontinue", ()=>{ OrtsContinue = stf.ReadIntBlock(0); }),
                new STFReader.TokenProcessor("ortsactsoundfile", ()=> OrtsActivitySoundProcessor(stf)),
            });
        }
    }

    public class EventCategoryTime : Event
    {  // E.g. Hisatsu route and Short Passenger Run shrtpass.act
        public int Time { get; private set; }

        public EventCategoryTime(STFReader stf)
        {
            stf.MustMatch("(");
            AddOrModifyEvent(stf, stf.FileName);
        }

        public override void AddOrModifyEvent(STFReader stf, string fileName)
        {
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("id", ()=>{ ID = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("activation_level", ()=>{ ActivationLevel = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("outcomes", ()=>
                {
                    if (Outcomes == null)
                        Outcomes = new Outcomes(stf, fileName);
                    else
                        Outcomes.CreateOrModifyOutcomes(stf, fileName); }),
                new STFReader.TokenProcessor("texttodisplayoncompletioniftriggered", ()=>{ TextToDisplayOnCompletionIfTriggered = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("texttodisplayoncompletionifnotrriggered", ()=>{ TextToDisplayOnCompletionIfNotTriggered = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("name", ()=>{ Name = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("time", ()=>{ Time = (int)stf.ReadFloatBlock(STFReader.Units.Time, null); }),
                new STFReader.TokenProcessor("ortscontinue", ()=>{ OrtsContinue = stf.ReadIntBlock(0); }),
                new STFReader.TokenProcessor("ortsactsoundfile", ()=> OrtsActivitySoundProcessor(stf)),
                new STFReader.TokenProcessor("ortsweatherchange", ()=>{ OrtsWeatherChange = new ORTSWeatherChange(stf);}),
            });
        }
    }
}
