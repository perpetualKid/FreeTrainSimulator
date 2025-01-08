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


using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Files
{

    /// <summary>
    /// Parse and *.act file.
    /// Naming for classes matches the terms in the *.act file.
    /// </summary>
    public class ActivityFile
    {
        public Activity Activity { get; private set; }

        public ActivityFile(string fileName)
        {
            using (STFReader stf = new STFReader(fileName, false)) {
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("tr_activity", ()=>{ Activity = new Activity(stf); }),
                });
                if (Activity == null)
                    STFException.TraceWarning(stf, "Missing Tr_Activity statement");
            }
        }

        // Used for explore in activity mode
        public ActivityFile(int startTime, string name)
        {
            Activity = new Activity(startTime, name);
        }
    }
}
