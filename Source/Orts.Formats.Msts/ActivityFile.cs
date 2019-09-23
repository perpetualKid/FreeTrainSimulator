// COPYRIGHT 2009, 2010, 2011, 2013, 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

//EBNF
//Usage follows http://en.wikipedia.org/wiki/Extended_Backus%E2%80%93Naur_Form

//Usage 			Notation
//================ ========
//definition 		 =
//concatenation 	 ,
//termination 	 ;
//alternation 	 |
//option 			 [ ... ]
//repetition 		 { ... }
//grouping 		 ( ... )
//terminal string  " ... "
//terminal string  ' ... '
//comment 		 (* ... *)
//special sequence ? ... ?
//exception 		 -

//(* MSTS Activity syntax in EBNF *)
//(* Note inconsistent use of "_" in names *)
//(* Note very similar names for different elements: ID v UiD, WagonsList v Wagon_List v Wagon, Train_Config v TrainCfg *)
//(* Note some percentages as integers 0-100 and some as float, e.g. 0.75 *)
//(* Note some times as 3*Integer for hr, min, sec and some as Integer seconds since some reference. *)
//(* As with many things Microsoft, text containing spaces must be enclosed in "" and text with no spaces needs no delimiter. *)

//Tr_Activity =
//    "(", Serial, Tr_Activity_Header, Tr_Activity_File, ")" ;

//    Serial = "Serial", "(", Integer, ")" ;

//    Tr_Activity_Header = "Tr_Activity_Header",
//        "(", *[ RouteID | Name | Description | Briefing | CompleteActivity
//        | Type | Mode | StartTime | Season | Weather | PathID | StartingSpeed | Duration | Difficulty
//        | Animals | Workers | FuelWater | FuelCoal | FuelDiesel ] ")" ; 
//        (* 1 or more options. Sequence is probably not significant. 
//           No information about which options are required or checking for duplicates. 
//         *)

//        RouteID = "RouteID", "(", Text, ")" ;  (* file name *)

//        Name = "Name", "(", Text, ")" ;

//        Description = "Description", "(", Text, ")" ;

//        Briefing = "Briefing", "(", ParagraphText, ")" ;

//            ParagraphText = Text, *( "+", Text ) ;

//        CompleteActivity = "CompleteActivity", "(", Integer, ")" ;	(* 1 for true (to be checked) *)

//        Type = "Type", "(", Integer, ")" ;	(* 0 (default) for ??? (to be checked) *)

//        Mode = "Mode", "(", Integer, ")" ;	(* 2 (default) for ??? (to be checked) *)

//        StartTime = "StartTime", "(", 3*Integer, ")" ;  (* Hour, Minute, Second (default is 10am) *)

//        Season = "Season", "(", Integer, ")" ;	(* Spring=0, Summer (default), Autumn, Winter *)

//        Weather = "Weather", "(", Integer, ")" ;	(* Clear=0 (default), Snow, Rain *)

//        PathID = "PathID", "(", Text , ")" ; 

//        StartingSpeed = "StartingSpeed", "(", Integer, ")" ;	(* 0 (default) for meters/second *) (* Why integer? *)

//        Duration = "Duration", "(", 2*Integer, ")" ;  (* Hour , Minute (default is 1 hour) *)

//        Difficulty = "Difficulty", "(", Integer, ")" ;	(* Easy=0 (default), Medium, Hard *)

//        Animals = "Animals", "(", Integer, ")" ;	(* 0-100 for % (default is 100) *)

//        Workers = "Workers", "(", Integer, ")" ;	(* 0-100 for % (default is 0) *)

//        FuelWater = "FuelWater", "(", Integer, ")";	(* 0-100 for % (default is 100) *)

//        FuelCoal = "FuelCoal", "(", Integer, ")";	(* 0-100 for % (default is 100) *)

//        FuelDiesel = "FuelDiesel", "(", Integer, ")";	(* 0-100 for % (default is 100) *)

//    Tr_Activity_File = "Tr_Activity_File", 
//        "(", *[ Player_Service_Definition | NextServiceUID | NextActivityObjectUID
//        | Traffic_Definition | Events | ActivityObjects | ActivityFailedSignals | PlatformNumPassengersWaiting | ActivityRestrictedSpeedZones ] ")" ;

//        Player_Service_Definition = "Player_Service_Definition",	(* Text is linked to PathID somehow. *)
//            "(", Text, [ Player_Traffic_Definition | UiD | *Player_Service_Item ], ")" ;    (* Code suggests just one Player_Traffic_Definition *)

//                Player_Traffic_Definition = "Player_Traffic_Definition", 
//                    "(", Integer, *( Player_Traffic_Item ), ")" ;

//                    Player_Traffic_Item =	(* Note lack of separator between Player_Traffic_Items. 
//                                               For simplicity, parser creates a new object whenever PlatformStartID is parsed. *)
//                        *[ "ArrivalTime", "(", Integer, ")"
//                         | "DepartTime", "(", Integer, ")"
//                         | "SkipCount", "(", Integer, ")"
//                         | "DistanceDownPath", "(", Float, ")" ],
//                        "PlatformStartID", "(", Integer, ")" ;

//                UiD = "UiD", "(", Integer, ")" ;

//                Player_Service_Item =	(* Note lack of separator between Player_Service_Items *)
//                                           For simplicity, parser creates a new object whenever PlatformStartID is parsed. *)
//                    *[ "Efficiency", "(", Float, ")"   (* e.g. 0.75 for 75% efficient? *)
//                     | "SkipCount", "(", Integer, ")"
//                     | "DistanceDownPath", "(", Float, ")" ],
//                    "PlatformStartID", "(", Integer, ")" ;

//        NextServiceUID = "NextServiceUID", "(", Integer, ")" ;

//        NextActivityObjectUID = "NextActivityObjectUID", "(", Integer, ")" ;

//        Traffic_Definition = "Traffic_Definition", "(", Text, *Service_Definition, ")" ;

//            Service_Definition = "Service_Definition",
//                "(", Text, Integer, UiD, *Player_Service_Item, ")" ;  (* Integer is time in seconds *)

//        Events = "Events", 
//            "(", *[ EventCategoryLocation | EventCategoryAction | EventCategoryTime ], ")" ;  (* CategoryTime *)

//            EventCategoryLocation = "EventCategoryLocation", 
//                "(", *[ EventTypeLocation | ID | Activation_Level | Outcomes
//                | Name | Location | TriggerOnStop ], ")" ;  (* ID and Name defined above *)	

//                EventTypeLocation = "EventTypeLocation", "(", ")" ;

//                ID = "ID", "(", Integer, ")" ;

//                Activation_Level = "Activation_Level", "(", Integer, ")" ;

//                Outcomes = "Outcomes",
//                    "(", *[ ActivitySuccess | ActivityFail | ActivateEvent | RestoreActLevel | DecActLevel | IncActLevel | DisplayMessage ], ")" ;

//                    ActivitySuccess = "ActivitySuccess", "(", ")" ;   (* No text parameter *)

//                    ActivityFail = "ActivityFail", "(", Text, ")" ;

//                    ActivateEvent = "ActivateEvent", "(", Integer, ")" ;

//                    RestoreActLevel = "RestoreActLevel", "(", Integer, ")" ;

//                    DecActLevel = "DecActLevel", "(", Integer, ")" ;

//                    IncActLevel = "IncActLevel", "(", Integer, ")" ;  (* Some MSTS samples have more than a single IncActLevel *)

//                    DisplayMessage = "DisplayMessage", "(", Text, ")" ;

//                Location = "Location", "(", 5*Integer, ")" ;

//                TriggerOnStop = "TriggerOnStop", "(", Integer, ")" ;  (* 0 for ?? *)

//                TextToDisplayOnCompletionIfTriggered = "TextToDisplayOnCompletionIfTriggered", "(", ParagraphText, ")" ;

//                TextToDisplayOnCompletionIfNotTriggered = "TextToDisplayOnCompletionIfNotTriggered", "(", ParagraphText, ")" ;

//            EventCategoryAction = "EventCategoryAction", 
//                "(", *[ EventType | ID | Activation_Level
//                | Outcomes | Reversable_Event | Name | Wagon_List | SidingItem | StationStop | Speed ] ;  (* ID, Activation_Level, Outcomes and Name defined above *)					

//                EventType =
//                    [ EventTypeAllStops | EventTypeAssembleTrain
//                    | EventTypeAssembleTrainAtLocation | EventTypeDropOffWagonsAtLocation 
//                    | EventTypePickUpPassengers | EventTypePickUpWagons 
//                    | EventTypeReachSpeed ] ;

//                    EventTypeAllStops = "EventTypeAllStops", "(", ")" ;

//                    EventTypeAssembleTrain = "EventTypeAssembleTrain", "(", ")" ;

//                    EventTypeAssembleTrainAtLocation = "EventTypeAssembleTrainAtLocation", "(", ")" ;

//                    EventTypeDropOffWagonsAtLocation = "EventTypeDropOffWagonsAtLocation", "(", ")" ;

//                    EventTypePickUpPassengers = "EventTypePickUpPassengers", "(", ")" ;

//                    EventTypePickUpWagons = "EventTypePickUpWagons", "(", ")" ;

//                    EventTypeReachSpeed = "EventTypeReachSpeed", "(", ")" ;

//                Reversable_Event = [ "Reversable_Event" | "Reversible_Event" ],  (* Reversable is not listed at www.learnersdictionary.com *) 
//                    "(", ")" ;

//                SidingItem =  "(", Integer, ")" ;

//                Wagon_List = "Wagon_List", "(", *WagonListItem, ")" ;

//                    WagonListItem = (* Description omitted from PickUpWagons and sometimes from DropOffWagonsAtLocation *)
//                        UiD, SidingItem, [ "Description", "(", Text, ")" ] ;  (" MSTS uses SidingItem inside the Wagon_List and also at the same level *)

//                StationStop = 

//                Speed = "(", Integer, ")" ;

//            EventCategoryTime = "EventCategoryTime", "(",  (* single instance of each alternative *)
//                [ EventTypeTime | ID | Activation_Level | Outcomes | TextToDisplayOnCompletionIfTriggered 
//                | TextToDisplayOnCompletionIfNotTriggered | Name | Time ], ")" ;  (* Outcomes may have empty parameters *)

//                EventTypeTime = "EventTypeTime", "(", ")" ;

//                Time = "Time", "(", Integer, ")" ;

//        ActivityObjects	= "ActivityObjects", "(", *ActivityObject, ")" ;

//            ActivityObject = "ActivityObject", 
//                "(", *[ ObjectType | Train_Config | Direction | ID | Tile ], ")" ;  (* ID defined above *)

//                ObjectType = "ObjectType", 
//                    "(", [ "WagonsList" | ?? ], ")" ;

//                Train_Config = "Train_Config", "(", TrainCfg, ")" ;

//                    TrainCfg = "TrainCfg", 
//                        "(", [ Name | Serial | MaxVelocity | NextWagonUID | Durability | Wagon | Engine ], ")" ;

//                        Serial = "Serial", "(", Integer, ")" ;

//                        MaxVelocity = "MaxVelocity", "(", 2*Float, ")" ;

//                        NextWagonUID = "NextWagonUID", "(", Integer, ")" ;

//                        Durability = "Durability", "(", Float, ")" ;

//                        Wagon = "Wagon", 
//                            "(", *[ WagonData | UiD ], ")" ;  (* UiD defined above *)

//                            WagonData = "WagonData", "(", 2*Text, ")" ;

//                        Engine = "Engine", "(", *[ UiD | EngineData ], ")" ;  (* UiD defined above *)

//                            EngineData = "EngineData", 
//                                "(", 2*Text, ")" ;

//                Direction = "Direction", "(", Integer, ")" ;  (* 0 for ??, 1 for ?? *)

//                Tile = "Tile", "(", 2*Integer, 2*Float, ")" ;

//        ActivityFailedSignals = "ActivityFailedSignals", "(", *ActivityFailedSignal, ")" ;

//            ActivityFailedSignal = "ActivityFailedSignal", "(", Integer, ")" ;

//        PlatformNumPassengersWaiting = "PlatformNumPassengersWaiting", "(", *PlatformData, ")" ;

//            PlatformData = "PlatformData", "(", 2*Integer, ")" ;

//        ActivityRestrictedSpeedZones = "ActivityRestrictedSpeedZones", "(", *ActivityRestrictedSpeedZone, ")" ;

//            ActivityRestrictedSpeedZone = "ActivityRestrictedSpeedZone",
//                "(", StartPosition, EndPosition, ")" ;

//                StartPosition = "StartPosition, "(", 4*Integer, ")" ;

//                EndPosition = "EndPosition", "(", 4*Integer, ")" ;


using System.Diagnostics;
using Microsoft.Xna.Framework;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts
{

    /// <summary>
    /// Parse and *.act file.
    /// Naming for classes matches the terms in the *.act file.
    /// </summary>
    public class ActivityFile
    {
        public Tr_Activity Tr_Activity { get; private set; }

        public ActivityFile(string fileName)
        {
            using (STFReader stf = new STFReader(fileName, false)) {
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("tr_activity", ()=>{ Tr_Activity = new Tr_Activity(stf); }),
                });
                if (Tr_Activity == null)
                    STFException.TraceWarning(stf, "Missing Tr_Activity statement");
            }
        }

        public void InsertORSpecificData(string filenamewithpath)
        {
            using (STFReader stf = new STFReader(filenamewithpath, false))
            {
                var tr_activityTokenPresent = false;
                stf.ParseFile(() => false && (Tr_Activity.Tr_Activity_Header != null), new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("tr_activity", ()=>{ tr_activityTokenPresent = true;  Tr_Activity.InsertORSpecificData (stf); }),
                    });
                if (!tr_activityTokenPresent)
                    STFException.TraceWarning(stf, "Missing Tr_Activity statement");
            }
        }

        // Used for explore in activity mode
        public ActivityFile(int startTime, string name)
        {
            Tr_Activity = new Tr_Activity(startTime, name);
        }
    }

    public class Tr_Activity {
        public int Serial = 1;
        public Tr_Activity_Header Tr_Activity_Header;
        public Tr_Activity_File Tr_Activity_File;

        public Tr_Activity(STFReader stf) {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tr_activity_file", ()=>{ Tr_Activity_File = new Tr_Activity_File(stf); }),
                new STFReader.TokenProcessor("serial", ()=>{ Serial = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("tr_activity_header", ()=>{ Tr_Activity_Header = new Tr_Activity_Header(stf); }),
            });
        }

        public void InsertORSpecificData(STFReader stf)
        {
            stf.MustMatch("(");
            var tr_activity_fileTokenPresent = false;
            stf.ParseBlock(() => false && (Tr_Activity_Header != null), new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tr_activity_file", ()=>{ tr_activity_fileTokenPresent = true;  Tr_Activity_File.InsertORSpecificData (stf); }),
            });
            if (!tr_activity_fileTokenPresent)
                STFException.TraceWarning(stf, "Missing Tr_Activity_File statement");
        }

        // Used for explore in activity mode
        public Tr_Activity(int startTime, string name)
        {
            Serial = -1;
            Tr_Activity_Header = new Tr_Activity_Header();
            Tr_Activity_File = new Tr_Activity_File(startTime, name);
        }
    }

    public class Tr_Activity_Header {
        public string RouteID;
        public string Name;					// AE Display Name
        public string Description = " ";
        public string Briefing = " ";
        public int CompleteActivity = 1;    // <CJComment> Should be boolean </CJComment>
        public int Type;
        public ActivityMode Mode = ActivityMode.Player;
        public StartTime StartTime = new StartTime(10, 0, 0);
        public SeasonType Season = SeasonType.Summer;
        public WeatherType Weather = WeatherType.Clear;
        public string PathID;
        public int StartingSpeed;       // <CJComment> Should be float </CJComment>
        public Duration Duration = new Duration(1, 0);
        public Difficulty Difficulty = Difficulty.Easy;
        public int Animals = 100;		// percent
        public int Workers; 			// percent
        public int FuelWater = 100;		// percent
        public int FuelCoal = 100;		// percent
        public int FuelDiesel = 100;	// percent

        public Tr_Activity_Header(STFReader stf) {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("routeid", ()=>{ RouteID = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("name", ()=>{ Name = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("description", ()=>{ Description = stf.ReadStringBlock(Description); }),
                new STFReader.TokenProcessor("briefing", ()=>{ Briefing = stf.ReadStringBlock(Briefing); }),
                new STFReader.TokenProcessor("completeactivity", ()=>{ CompleteActivity = stf.ReadIntBlock(CompleteActivity); }),
                new STFReader.TokenProcessor("type", ()=>{ Type = stf.ReadIntBlock(Type); }),
                new STFReader.TokenProcessor("mode", ()=>{ Mode = (ActivityMode)stf.ReadIntBlock((int)Mode); }),
                new STFReader.TokenProcessor("starttime", ()=>{ StartTime = new StartTime(stf); }),
                new STFReader.TokenProcessor("season", ()=>{ Season = (SeasonType)stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("weather", ()=>{ Weather = (WeatherType)stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("pathid", ()=>{ PathID = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("startingspeed", ()=>{ StartingSpeed = (int)stf.ReadFloatBlock(STFReader.Units.Speed, (float)StartingSpeed); }),                
                new STFReader.TokenProcessor("duration", ()=>{ Duration = new Duration(stf); }),
                new STFReader.TokenProcessor("difficulty", ()=>{ Difficulty = (Difficulty)stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("animals", ()=>{ Animals = stf.ReadIntBlock(Animals); }),
                new STFReader.TokenProcessor("workers", ()=>{ Workers = stf.ReadIntBlock(Workers); }),
                new STFReader.TokenProcessor("fuelwater", ()=>{ FuelWater = stf.ReadIntBlock(FuelWater); }),
                new STFReader.TokenProcessor("fuelcoal", ()=>{ FuelCoal = stf.ReadIntBlock(FuelCoal); }),
                new STFReader.TokenProcessor("fueldiesel", ()=>{ FuelDiesel = stf.ReadIntBlock(FuelDiesel); }),
            });
        }

        // Used for explore in activity mode
        public Tr_Activity_Header()
        {
        }
    }

    public class Tr_Activity_File {
        public PlayerServices PlayerServices;
        public int NextServiceUID = 1;
        public int NextActivityObjectUID = 32786;
        public ActivityObjects ActivityObjects;
        public FailedSignals ActivityFailedSignals;
        public Events Events;
        public Traffic Traffic_Definition;
        public PlatformPassengersWaiting PlatformWaitingPassengers;
        public RestrictedSpeedZones ActivityRestrictedSpeedZones;
        public int ORTSAIHornAtCrossings = -1;

        // Override values for activity creators
        bool IsActivityOverride = false;

        // General TAB
        public int ORTSOptionsGraduatedBrakeRelease = -1;
        public int ORTSOptionsViewDispatcherWindow = -1;
        public int ORTSOptionsRetainersOnAllCars = -1;
        public int ORTSOptionsSoundSpeedControl = -1;

        // Video TAB
        public int ORTSOptionsFastFullScreenAltTab = -1;

        // Simulation TAB
        public int ORTSOptionsForcedRedAtStationStops = -1;
        public int ORTSOptionsAutopilot = -1;
        public int ORTSOptionsExtendedAITrainShunting = -1;
        public int ORTSOptionsUseAdvancedAdhesion = -1;
        public int ORTSOptionsBreakCouplers = -1;
        public int ORTSOptionsCurveResistanceDependent = -1;
        public int ORTSOptionsCurveSpeedDependent = -1;
        public int ORTSOptionsTunnelResistanceDependent = -1;
        public int ORTSOptionsWindResistanceDependent = -1;
        public int ORTSOptionsHotStart = -1;

        // Experimental TAB
        public int ORTSOptionsUseLocationPassingPaths = -1;
        public int ORTSOptionsAdhesionFactor = -1;
        public int ORTSOptionsAdhesionFactorChange = -1;
        public int ORTSOptionsAdhesionProportionalToWeather = -1;
        public int ORTSOptionsActivityRandomization = -1;
        public int ORTSOptionsActivityWeatherRandomization = -1;
        public int ORTSOptionsSuperElevationLevel = -1;
        public int ORTSOptionsSuperElevationMinimumLength = -1;
        public int ORTSOptionsSuperElevationGauge = -1;


        public Tr_Activity_File(STFReader stf) {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("player_service_definition",()=>{ PlayerServices = new PlayerServices(stf); }),
                new STFReader.TokenProcessor("nextserviceuid",()=>{ NextServiceUID = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("nextactivityobjectuid",()=>{ NextActivityObjectUID = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("ortsaihornatcrossings", ()=>{ ORTSAIHornAtCrossings = stf.ReadIntBlock(ORTSAIHornAtCrossings); }),
                new STFReader.TokenProcessor("events",()=>{ Events = new Events(stf); }),
                new STFReader.TokenProcessor("traffic_definition",()=>{ Traffic_Definition = new Traffic(stf); }),
                new STFReader.TokenProcessor("activityobjects",()=>{ ActivityObjects = new ActivityObjects(stf); }),
                new STFReader.TokenProcessor("platformnumpassengerswaiting",()=>{ PlatformWaitingPassengers = new PlatformPassengersWaiting(stf); }),  // 35 files. To test, use EUROPE1\ACTIVITIES\aftstorm.act
                new STFReader.TokenProcessor("activityfailedsignals",()=>{ ActivityFailedSignals = new FailedSignals(stf); }),
                new STFReader.TokenProcessor("activityrestrictedspeedzones",()=>{ ActivityRestrictedSpeedZones = new RestrictedSpeedZones(stf); }),   // 27 files. To test, use EUROPE1\ACTIVITIES\lclsrvce.act
            });
        }

        // Used for explore in activity mode
        public Tr_Activity_File(int startTime, string name)
        {
            PlayerServices = new PlayerServices(startTime, name);
        }

        public void InsertORSpecificData(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("ortsaihornatcrossings", ()=>{ ORTSAIHornAtCrossings = stf.ReadIntBlock(ORTSAIHornAtCrossings); }),

                // General TAB
                new STFReader.TokenProcessor("ortsgraduatedbrakerelease", ()=>{ ORTSOptionsGraduatedBrakeRelease = stf.ReadIntBlock(ORTSOptionsGraduatedBrakeRelease); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortsviewdispatchwindow", ()=>{ ORTSOptionsViewDispatcherWindow = stf.ReadIntBlock(ORTSOptionsViewDispatcherWindow); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortsretainersonallcars", ()=>{ ORTSOptionsRetainersOnAllCars = stf.ReadIntBlock(ORTSOptionsRetainersOnAllCars); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortssoundspeedcontrol", ()=>{ ORTSOptionsSoundSpeedControl = stf.ReadIntBlock(ORTSOptionsSoundSpeedControl); IsActivityOverride = true; }),

                // Video TAB
                new STFReader.TokenProcessor("ortsfastfullscreenalttab", ()=>{ ORTSOptionsFastFullScreenAltTab = stf.ReadIntBlock(ORTSOptionsFastFullScreenAltTab); IsActivityOverride = true; }),

                // Simulation TAB
                new STFReader.TokenProcessor("ortsforcedredatstationstops", ()=>{ ORTSOptionsForcedRedAtStationStops = stf.ReadIntBlock(ORTSOptionsForcedRedAtStationStops); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortsautopilot", ()=>{ ORTSOptionsAutopilot = stf.ReadIntBlock(ORTSOptionsAutopilot); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortsextendedaitrainshunting", ()=>{ ORTSOptionsExtendedAITrainShunting = stf.ReadIntBlock(ORTSOptionsExtendedAITrainShunting); IsActivityOverride = true; }),

                new STFReader.TokenProcessor("ortsuseadvancedadhesion", ()=>{ ORTSOptionsUseAdvancedAdhesion = stf.ReadIntBlock(ORTSOptionsUseAdvancedAdhesion); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortsbreakcouplers", ()=>{ ORTSOptionsBreakCouplers = stf.ReadIntBlock(ORTSOptionsBreakCouplers); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortscurveresistancedependent", ()=>{ ORTSOptionsCurveResistanceDependent = stf.ReadIntBlock(ORTSOptionsCurveResistanceDependent); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortscurvespeeddependent", ()=>{ ORTSOptionsCurveSpeedDependent = stf.ReadIntBlock(ORTSOptionsCurveSpeedDependent); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortstunnelresistancedependent", ()=>{ ORTSOptionsTunnelResistanceDependent = stf.ReadIntBlock(ORTSOptionsTunnelResistanceDependent); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortswindresistancedependent", ()=>{ ORTSOptionsWindResistanceDependent = stf.ReadIntBlock(ORTSOptionsWindResistanceDependent); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortshotstart", ()=>{ ORTSOptionsHotStart = stf.ReadIntBlock(ORTSOptionsHotStart); IsActivityOverride = true; }),

                // Experimental TAB
                new STFReader.TokenProcessor("ortslocationlinkedpassingpaths", ()=>{ ORTSOptionsUseLocationPassingPaths = stf.ReadIntBlock(ORTSOptionsUseLocationPassingPaths); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortsadhesionfactorcorrection", ()=>{ ORTSOptionsAdhesionFactor = stf.ReadIntBlock(ORTSOptionsAdhesionFactor); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortsadhesionfactorchange", ()=>{ ORTSOptionsAdhesionFactorChange = stf.ReadIntBlock(ORTSOptionsAdhesionFactorChange); IsActivityOverride = true; }),

                new STFReader.TokenProcessor("ortsadhesionproportionaltoweather", ()=>{ ORTSOptionsAdhesionProportionalToWeather = stf.ReadIntBlock(ORTSOptionsAdhesionProportionalToWeather); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortsactivityrandomization", ()=>{ ORTSOptionsActivityRandomization = stf.ReadIntBlock(ORTSOptionsActivityRandomization); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortsactivityweatherrandomization", ()=>{ ORTSOptionsActivityWeatherRandomization = stf.ReadIntBlock(ORTSOptionsActivityWeatherRandomization); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortssuperelevationlevel", ()=>{ ORTSOptionsSuperElevationLevel = stf.ReadIntBlock(ORTSOptionsSuperElevationLevel); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortssuperelevationminimumlength", ()=>{ ORTSOptionsSuperElevationMinimumLength = stf.ReadIntBlock(ORTSOptionsSuperElevationMinimumLength); IsActivityOverride = true; }),
                new STFReader.TokenProcessor("ortssuperelevationgauge", ()=>{ ORTSOptionsSuperElevationGauge = stf.ReadIntBlock(ORTSOptionsSuperElevationGauge); IsActivityOverride = true; }),

                new STFReader.TokenProcessor("events",()=>
                {
                    if ( Events == null)
                        Events = new Events(stf);
                    else
                        Events.InsertORSpecificData (stf);
                }
                ),
            });
        }

        // Override User settings with activity creator settings if present in INCLUDE file
        public void OverrideUserSettings(Orts.Settings.UserSettings setting)
        {
            if (IsActivityOverride)
            {
                Trace.Write("\n------------------------------------------------------------------------------------------------");
                Trace.Write("\nThe following Option settings have been temporarily set by this activity (no permanent changes have been made to your settings):");

                // General TAB 

                if (ORTSOptionsRetainersOnAllCars == 1)
                {
                    setting.RetainersOnAllCars = true;
                    Trace.Write("\nRetainers on all cars            =   True");
                }
                else if (ORTSOptionsRetainersOnAllCars == 0)
                {
                    setting.RetainersOnAllCars = false;
                    Trace.Write("\nRetainers on all cars            =   True");
                }

                if (ORTSOptionsGraduatedBrakeRelease == 1)
                {
                    setting.GraduatedRelease = true;
                    Trace.Write("\nGraduated Brake Release          =   True");
                }
                else if (ORTSOptionsGraduatedBrakeRelease == 0)
                {
                    setting.GraduatedRelease = false;
                    Trace.Write("\nGraduated Brake Release          =   False");
                }
                               
                if (ORTSOptionsViewDispatcherWindow == 1)
                {
                    setting.ViewDispatcher = true;
                    Trace.Write("\nView Dispatch Window             =   True");
                }
                else if (ORTSOptionsViewDispatcherWindow == 0)
                {
                    setting.ViewDispatcher = false;
                    Trace.Write("\nView Dispatch Window             =   False");
                }

                if (ORTSOptionsSoundSpeedControl == 1)
                {
                    setting.SpeedControl = true;
                    Trace.Write("\nSound speed control              =   True");
                }
                else if (ORTSOptionsSoundSpeedControl == 0)
                {
                    setting.SpeedControl = false;
                    Trace.Write("\nSound speed control              =   True");
                }

                // Video TAB
                if (ORTSOptionsFastFullScreenAltTab == 1)
                {
                    setting.FastFullScreenAltTab = true;
                    Trace.Write("\nFast Full Screen Alt TAB         =   True");
                }
                else if (ORTSOptionsFastFullScreenAltTab == 0)
                {
                    setting.FastFullScreenAltTab = false;
                    Trace.Write("\nFast Full Screen Alt TAB         =   False");
                }


                // Simulation TAB
                if (ORTSOptionsAutopilot == 1)
                {
                    setting.Autopilot = true;
                    Trace.Write("\nAutopilot                        =   True");
                }
                else if (ORTSOptionsAutopilot == 0)
                {
                    setting.Autopilot = false;
                    Trace.Write("\nAutopilot                        =   False");
                }

                if (ORTSOptionsForcedRedAtStationStops == 1)
                {
                    setting.NoForcedRedAtStationStops = false; // Note this parameter is reversed in its logic to others.
                    Trace.Write("\nForced Red at Station Stops      =   True");
                }
                else if (ORTSOptionsForcedRedAtStationStops == 0)
                {
                    setting.NoForcedRedAtStationStops = true; // Note this parameter is reversed in its logic to others.
                    Trace.Write("\nForced Red at Station Stops      =   False");
                }


                if (ORTSOptionsExtendedAITrainShunting == 1)
                {
                    setting.ExtendedAIShunting = true;
                    Trace.Write("\nExtended AI Train Shunting       =   True");
                }
                else if (ORTSOptionsExtendedAITrainShunting == 0)
                {
                    setting.ExtendedAIShunting = false;
                    Trace.Write("\nExtended AI Train Shunting       =   False");
                }

                if (ORTSOptionsUseAdvancedAdhesion == 1)
                {
                    setting.UseAdvancedAdhesion = true;
                    Trace.Write("\nUse Advanced Adhesion            =   True");
                }
                else if (ORTSOptionsUseAdvancedAdhesion == 0)
                {
                    setting.UseAdvancedAdhesion = false;
                    Trace.Write("\nUse Advanced Adhesion            =   False");
                }

                if (ORTSOptionsBreakCouplers == 1)
                {
                    setting.BreakCouplers = true;
                    Trace.Write("\nBreak Couplers                   =   True");
                }
                else if (ORTSOptionsBreakCouplers == 0)
                {
                    setting.BreakCouplers = false;
                    Trace.Write("\nBreak Couplers                   =   False");
                }

                if (ORTSOptionsCurveResistanceDependent == 1)
                {
                    setting.CurveResistanceDependent = true;
                    Trace.Write("\nCurve Resistance Dependent       =   True");
                }
                else if (ORTSOptionsCurveResistanceDependent == 0)
                {
                    setting.CurveResistanceDependent = false;
                    Trace.Write("\nCurve Resistance Dependent       =   False");
                }

                if (ORTSOptionsCurveSpeedDependent == 1)
                {
                    setting.CurveSpeedDependent = true;
                    Trace.Write("\nCurve Speed Dependent            =   True");
                }
                else if (ORTSOptionsCurveSpeedDependent == 1)
                {
                    setting.CurveSpeedDependent = false;
                    Trace.Write("\nCurve Speed Dependent            =   False");
                }

                if (ORTSOptionsTunnelResistanceDependent == 1)
                {
                    setting.TunnelResistanceDependent = true;
                    Trace.Write("\nTunnel Resistance Dependent      =   True");
                }
                else if (ORTSOptionsTunnelResistanceDependent == 0)
                {
                    setting.TunnelResistanceDependent = false;
                    Trace.Write("\nTunnel Resistance Dependent      =   False");
                }

                if (ORTSOptionsWindResistanceDependent == 1)
                {
                    setting.WindResistanceDependent = true;
                    Trace.Write("\nWind Resistance Dependent        =   True");
                }
                else if (ORTSOptionsWindResistanceDependent == 0)
                {
                    setting.WindResistanceDependent = false;
                    Trace.Write("\nWind Resistance Dependent        =   False");
                }

                if (ORTSOptionsHotStart == 1)
                {
                    setting.HotStart = true;
                    Trace.Write("\nHot Start                        =   True");
                }
                else if (ORTSOptionsHotStart == 0)
                {
                    setting.HotStart = false;
                    Trace.Write("\nHot Start                        =   False");
                }

                // Experimental TAB
                if (ORTSOptionsUseLocationPassingPaths == 1)
                {
                    setting.UseLocationPassingPaths = true;
                    Trace.Write("\nLocation Linked Passing Paths    =   True");
                }
                else if (ORTSOptionsUseLocationPassingPaths == 0)
                {
                    setting.UseLocationPassingPaths = false;
                    Trace.Write("\nLocation Linked Passing Paths    =   False");
                }

                if (ORTSOptionsAdhesionFactor > 0)
                {
                    setting.AdhesionFactor = ORTSOptionsAdhesionFactor;
                    setting.AdhesionFactor = MathHelper.Clamp(setting.AdhesionFactor, 10, 200);
                    Trace.Write("\nAdhesion Factor Correction       =   " + setting.AdhesionFactor.ToString());
                }

                if (ORTSOptionsAdhesionFactorChange > 0)
                {
                    setting.AdhesionFactorChange = ORTSOptionsAdhesionFactorChange;
                    setting.AdhesionFactorChange = MathHelper.Clamp(setting.AdhesionFactorChange, 0, 100);
                    Trace.Write("\nAdhesion Factor Change           =   " + setting.AdhesionFactorChange.ToString());
                }

                if (ORTSOptionsAdhesionProportionalToWeather == 1)
                {
                    setting.AdhesionProportionalToWeather = true;
                    Trace.Write("\nAdhesion Proportional to Weather =   True");
                }
                else if (ORTSOptionsAdhesionProportionalToWeather ==0)
                {
                    setting.AdhesionProportionalToWeather = true;
                    Trace.Write("\nAdhesion Proportional to Weather =   False");
                }

                if (ORTSOptionsActivityRandomization > 0)
                {
                    setting.ActRandomizationLevel = ORTSOptionsActivityRandomization;
                    setting.ActRandomizationLevel = MathHelper.Clamp(setting.ActRandomizationLevel, 0, 3);
                    Trace.Write("\nActivity Randomization           =   " + setting.ActRandomizationLevel.ToString() );
                }

                if (ORTSOptionsActivityWeatherRandomization > 0)
                {
                    setting.ActWeatherRandomizationLevel = ORTSOptionsActivityWeatherRandomization;
                    setting.ActWeatherRandomizationLevel = MathHelper.Clamp(setting.ActWeatherRandomizationLevel, 0, 3);
                    Trace.Write("\nActivity Weather Randomization   =   " + setting.ActWeatherRandomizationLevel.ToString());
                }

                if (ORTSOptionsSuperElevationLevel > 0)
                {
                    setting.UseSuperElevation = ORTSOptionsSuperElevationLevel;
                    setting.UseSuperElevation = MathHelper.Clamp(setting.UseSuperElevation, 0, 10);
                    Trace.Write("\nSuper elevation - level          =   " + setting.UseSuperElevation.ToString());
                }

                if (ORTSOptionsSuperElevationMinimumLength > 0)
                {
                    setting.SuperElevationMinLen = ORTSOptionsSuperElevationMinimumLength;
                    setting.SuperElevationMinLen = MathHelper.Clamp(setting.SuperElevationMinLen, 50, 1000000);
                    Trace.Write("\nSuper elevation - minimum length =   " + setting.SuperElevationMinLen.ToString());
                }

                if (ORTSOptionsSuperElevationGauge > 0)
                {
                    setting.SuperElevationGauge = ORTSOptionsSuperElevationGauge;
                    setting.SuperElevationGauge = MathHelper.Clamp(setting.SuperElevationGauge, 300, 2500);
                    Trace.Write("\nSuper elevation - gauge          =   " + setting.SuperElevationGauge.ToString());
                }


                Trace.Write("\n------------------------------------------------------------------------------------------------");

            }
        }
    }
}
