// COPYRIGHT 2014, 2015 by the Open Rails project.
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


namespace Orts.Formats.OR
{
    //================================================================================================//
    /// <summary>
    /// AuxActionRef
    /// The main class to define Auxiliary Action through the editor and used by ActivityRunner
    /// </summary>

    public class AuxActionRef
    {
        public bool IsGeneric { get; set; }
        public AUX_ACTION ActionType;

       
        public enum AUX_ACTION
        {
            WAITING_POINT,
            SOUND_HORN,
            CONTROL_START,
            SIGNAL_DELEGATE,
            CONTROL_STOPPED,
            NONE
        }

        //================================================================================================//
        /// <summary>
        /// AIAuxActionsRef: Generic Constructor
        /// The specific datas are used to fired the Action.
        /// </summary>

        public AuxActionRef(AUX_ACTION actionType, bool isGeneric)  
        {
            IsGeneric = isGeneric;
            ActionType = actionType;
        }

        public AuxActionRef(AUX_ACTION actionType = AuxActionRef.AUX_ACTION.NONE)
        {
            IsGeneric = true;
            ActionType = actionType;
        }
    }

    //  AuxActionHorn is always a Generic Action, no need to specify a location
    public class AuxActionHorn : AuxActionRef
    {
        public int Delay;
        public float RequiredDistance;

        public AuxActionHorn(bool isGeneric, int delay = 2, float requiredDistance = 0) :    
            base(AUX_ACTION.SOUND_HORN ,isGeneric)                                          
        {
            Delay = delay;
            RequiredDistance = requiredDistance;
        }
    }    
}
