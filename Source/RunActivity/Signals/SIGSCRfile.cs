﻿// COPYRIGHT 2013 by the Open Rails project.
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

// Author : Rob Roeterdink
//
//
// This file processes the MSTS SIGSCR.dat file, which contains the signal logic.
// The information is stored in a series of classes.
// This file also contains the functions to process the information when running, and as such is linked with signals.cs
//
// Debug flags :
// #define DEBUG_ALLOWCRASH
// removes catch and allows program to crash on error statement
//
// #define DEBUG_PRINT_PROCESS
// prints processing details
// set TBD_debug_ref to TDB index of required signals
//
// #define DEBUG_PRINT_ENABLED
// prints processing details of all enabled signals
//

using Orts.Formats.Msts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ORTS
{

    //================================================================================================//
    //
    // class scrfile
    //
    //================================================================================================//

    public class SIGSCRfile
    {

#if DEBUG_PRINT_PROCESS
        public static int[] TDB_debug_ref;                 /* signal TDB idents         */
        public static string dpr_fileLoc = @"C:\temp\";     /* file path for debug files */
#endif

#if DEBUG_PRINT_ENABLED
        public static string dpe_fileLoc = @"C:\temp\";     /* file path for debug files */
#endif

        SignalScripts SignalScripts;

        //================================================================================================//
        //
        // Constructor
        //
        //================================================================================================//

        public SIGSCRfile(SignalScripts scripts)
        {
            SignalScripts = scripts;
        }

        //================================================================================================//
        //
        // processing routines
        //
        //================================================================================================//
        //
        // main update routine
        //
        //================================================================================================//

        public static void SH_update(SignalHead thisHead, SIGSCRfile sigscr)
        {

            SignalScripts.SCRScripts signalScript;
            if (thisHead.signalType == null)
                return;
            if (sigscr.SignalScripts.Scripts.TryGetValue(thisHead.signalType, out signalScript))
            {
                sigscr.SH_process_script(thisHead, signalScript, sigscr);
            }
            else
            {
                sigscr.SH_update_basic(thisHead);
            }
        }

        //================================================================================================//
        //
        // update_basic : update signal without script
        //
        //================================================================================================//

        public void SH_update_basic(SignalHead thisHead)
        {
            if (thisHead.mainSignal.block_state() == MstsBlockState.CLEAR)
            {
                thisHead.SetLeastRestrictiveAspect();
            }
            else
            {
                thisHead.SetMostRestrictiveAspect();
            }
        }

        //================================================================================================//
        //
        // process script
        //
        //================================================================================================//

        public void SH_process_script(SignalHead thisHead, SignalScripts.SCRScripts signalScript, SIGSCRfile sigscr)
        {

            int[] localFloats = new int[signalScript.totalLocalFloats];

            // process script

#if DEBUG_PRINT_ENABLED
            if (thisHead.mainSignal.enabledTrain != null)
            {
                File.AppendAllText(dpe_fileLoc + @"printproc.txt", "\n\nSIGNAL : " + thisHead.TDBIndex.ToString() + "\n");
                File.AppendAllText(dpe_fileLoc + @"printproc.txt", "OBJECT : " + thisHead.mainSignal.thisRef.ToString() + "\n");
                File.AppendAllText(dpe_fileLoc + @"printproc.txt", "type   : " + signalScript.scriptname + "\n");
            }
#endif
#if DEBUG_PRINT_PROCESS
            if (TDB_debug_ref.Contains(thisHead.TDBIndex))
            {
                File.AppendAllText(dpr_fileLoc + @"printproc.txt", "\n\nSIGNAL : " + thisHead.TDBIndex.ToString() + "\n");
                File.AppendAllText(dpr_fileLoc + @"printproc.txt", "OBJECT : " + thisHead.mainSignal.thisRef.ToString() + "\n");
                File.AppendAllText(dpr_fileLoc + @"printproc.txt", "type   : " + signalScript.scriptname + "\n");
            }
#endif

            if (!SH_process_StatementBlock(thisHead, signalScript.Statements, localFloats, sigscr))
                return;


#if DEBUG_PRINT_ENABLED
            if (thisHead.mainSignal.enabledTrain != null)
            {
                File.AppendAllText(dpe_fileLoc + @"printproc.txt", "\n ------- \n");
            }
#endif
#if DEBUG_PRINT_PROCESS
            if (TDB_debug_ref.Contains(thisHead.TDBIndex))
            {
                File.AppendAllText(dpr_fileLoc + @"printproc.txt", "\n ------- \n");
            }
#endif

        }

        //================================================================================================//
        //
        // process statement block
        // called for full script as well as for IF and ELSE blocks
        // if returns false : abort further processing
        //
        //================================================================================================//

        public bool SH_process_StatementBlock(SignalHead thisHead, ArrayList Statements,
                    int[] localFloats, SIGSCRfile sigscr)
        {

            // loop through all lines

            foreach (object scriptstat in Statements)
            {

                // process statement lines

                if (scriptstat is SignalScripts.SCRScripts.SCRStatement)
                {
                    SignalScripts.SCRScripts.SCRStatement ThisStat = (SignalScripts.SCRScripts.SCRStatement)scriptstat;

                    if (ThisStat.StatementTerms[0].Function == SignalScripts.SCRExternalFunctions.RETURN)
                    {
                        return false;
                    }

                    SH_processAssignStatement(thisHead, ThisStat, localFloats, sigscr);

#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt", "Statement : \n");
                        foreach (string statstring in ThisStat.StatementParts)
                        {
                            File.AppendAllText(dpe_fileLoc + @"printproc.txt", "   " + statstring + "\n");
                        }
                        foreach (int lfloat in localFloats)
                        {
                            File.AppendAllText(dpe_fileLoc + @"printproc.txt", " local : " + lfloat.ToString() + "\n");
                        }
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt", "Externals : \n");
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt", " state      : " + thisHead.state.ToString() + "\n");
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt", " draw_state : " + thisHead.draw_state.ToString() + "\n");
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt", " enabled    : " + thisHead.mainSignal.enabled.ToString() + "\n");
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt", " blockstate : " + thisHead.mainSignal.blockState.ToString() + "\n");
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt", "\n");
                    }
#endif

#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        File.AppendAllText(dpr_fileLoc + @"printproc.txt", "Statement : \n");
                        foreach (string statstring in ThisStat.StatementParts)
                        {
                            File.AppendAllText(dpr_fileLoc + @"printproc.txt", "   " + statstring + "\n");
                        }
                        foreach (int lfloat in localFloats)
                        {
                            File.AppendAllText(dpr_fileLoc + @"printproc.txt", " local : " + lfloat.ToString() + "\n");
                        }
                        File.AppendAllText(dpr_fileLoc + @"printproc.txt", "Externals : \n");
                        File.AppendAllText(dpr_fileLoc + @"printproc.txt", " state      : " + thisHead.state.ToString() + "\n");
                        File.AppendAllText(dpr_fileLoc + @"printproc.txt", " draw_state : " + thisHead.draw_state.ToString() + "\n");
                        File.AppendAllText(dpr_fileLoc + @"printproc.txt", " enabled    : " + thisHead.mainSignal.enabled.ToString() + "\n");
                        File.AppendAllText(dpr_fileLoc + @"printproc.txt", " blockstate : " + thisHead.mainSignal.blockState.ToString() + "\n");
                        File.AppendAllText(dpr_fileLoc + @"printproc.txt", "\n");
                    }
#endif

                }

                if (scriptstat is SignalScripts.SCRScripts.SCRConditionBlock)
                {
                    SignalScripts.SCRScripts.SCRConditionBlock thisCond = (SignalScripts.SCRScripts.SCRConditionBlock)scriptstat;
                    if (!SH_processIfCondition(thisHead, thisCond, localFloats, sigscr))
                        return false;
                }
            }

            return true;
        }

        //================================================================================================//
        //
        // process assign statement
        //
        //================================================================================================//

        public void SH_processAssignStatement(SignalHead thisHead, SignalScripts.SCRScripts.SCRStatement thisStat,
                    int[] localFloats, SIGSCRfile sigscr)
        {

            // get term value

            int tempvalue = 0;

            tempvalue = SH_processSubTerm(thisHead, thisStat.StatementTerms, 0, localFloats, sigscr);

            // assign value

            switch (thisStat.AssignType)
            {

                // assign value to external float
                // Possible floats :
                //                        STATE
                //                        DRAW_STATE
                //                        ENABLED     (not allowed for write)
                //                        BLOCK_STATE (not allowed for write)

                case (SignalScripts.SCRTermType.ExternalFloat):
                    SignalScripts.SCRExternalFloats FloatType = (SignalScripts.SCRExternalFloats)thisStat.AssignParameter;

                    switch (FloatType)
                    {
                        case SignalScripts.SCRExternalFloats.STATE:
                            thisHead.state = (MstsSignalAspect)tempvalue;
                            break;

                        case SignalScripts.SCRExternalFloats.DRAW_STATE:
                            thisHead.draw_state = tempvalue;
                            break;

                        default:
                            break;
                    }
                    break;

                // Local float

                case (SignalScripts.SCRTermType.LocalFloat):
                    localFloats[thisStat.AssignParameter] = tempvalue;
                    break;

                default:
                    break;
            }
        }

        //================================================================================================//
        //
        // get value of single term
        //
        //================================================================================================//

        public int SH_processAssignTerm(SignalHead thisHead, List<SignalScripts.SCRScripts.SCRStatTerm> StatementTerms,
                           SignalScripts.SCRScripts.SCRStatTerm thisTerm, int sublevel,
                           int[] localFloats, SIGSCRfile sigscr)
        {

            int termvalue = 0;

            if (thisTerm.Function != SignalScripts.SCRExternalFunctions.NONE)
            {
                termvalue = SH_function_value(thisHead, thisTerm, localFloats, sigscr);
            }
            else if (thisTerm.PartParameter != null)
            {

                // for non-function terms only first entry is valid

                SignalScripts.SCRScripts.SCRParameterType thisParameter = thisTerm.PartParameter[0];
                termvalue = SH_termvalue(thisHead, thisParameter, localFloats, sigscr);
            }
            else if (thisTerm.sublevel > 0)
            {
                termvalue = SH_processSubTerm(thisHead, StatementTerms, thisTerm.sublevel, localFloats, sigscr);
            }

            return termvalue;
        }


        //================================================================================================//
        //
        // process subterm
        //
        //================================================================================================//

        public int SH_processSubTerm(SignalHead thisHead, List<SignalScripts.SCRScripts.SCRStatTerm> StatementTerms,
                           int sublevel, int[] localFloats, SIGSCRfile sigscr)
        {
            int tempvalue = 0;
            int termvalue = 0;

            foreach (SignalScripts.SCRScripts.SCRStatTerm thisTerm in StatementTerms)
            {
                if (thisTerm.Function == SignalScripts.SCRExternalFunctions.RETURN)
                {
                    break;
                }

                SignalScripts.SCRTermOperator thisOperator = thisTerm.TermOperator;
                if (thisTerm.issublevel == sublevel)
                {
                    termvalue =
                            SH_processAssignTerm(thisHead, StatementTerms, thisTerm, sublevel, localFloats, sigscr);
                    if (thisTerm.negate)
                    {
                        termvalue = termvalue == 0 ? 1 : 0;
                    }

                    switch (thisOperator)
                    {
                        case (SignalScripts.SCRTermOperator.MULTIPLY):
                            tempvalue *= termvalue;
                            break;

                        case (SignalScripts.SCRTermOperator.PLUS):
                            tempvalue += termvalue;
                            break;

                        case (SignalScripts.SCRTermOperator.MINUS):
                            tempvalue -= termvalue;
                            break;

                        case (SignalScripts.SCRTermOperator.DIVIDE):
                            if (termvalue == 0)
                            {
                                tempvalue = 0;
                            }
                            else
                            {
                                tempvalue /= termvalue;
                            }
                            break;

                        case (SignalScripts.SCRTermOperator.MODULO):
                            tempvalue %= termvalue;
                            break;

                        default:
                            tempvalue = termvalue;
                            break;
                    }
                }
            }

            return tempvalue;
        }

        //================================================================================================//
        //
        // get parameter term value
        //
        //================================================================================================//

        public static int SH_termvalue(SignalHead thisHead, SignalScripts.SCRScripts.SCRParameterType thisParameter,
                    int[] localFloats, SIGSCRfile sigscr)
        {

            int return_value = 0;

            // for non-function terms only first entry is valid

            switch (thisParameter.PartType)
            {

                // assign value to external float
                // Possible floats :
                //                        STATE
                //                        DRAW_STATE
                //                        ENABLED     
                //                        BLOCK_STATE

                case (SignalScripts.SCRTermType.ExternalFloat):
                    SignalScripts.SCRExternalFloats FloatType = (SignalScripts.SCRExternalFloats)thisParameter.PartParameter;

                    switch (FloatType)
                    {
                        case SignalScripts.SCRExternalFloats.STATE:
                            return_value = (int)thisHead.state;
                            break;

                        case SignalScripts.SCRExternalFloats.DRAW_STATE:
                            return_value = thisHead.draw_state;
                            break;

                        case SignalScripts.SCRExternalFloats.ENABLED:
                            return_value = Convert.ToInt32(thisHead.mainSignal.enabled);
                            break;

                        case SignalScripts.SCRExternalFloats.BLOCK_STATE:
                            return_value = (int)thisHead.mainSignal.block_state();
                            break;

                        case SignalScripts.SCRExternalFloats.APPROACH_CONTROL_REQ_POSITION:
                            return_value = thisHead.ApproachControlLimitPositionM.HasValue ? Convert.ToInt32(thisHead.ApproachControlLimitPositionM.Value) : -1;
                            break;

                        case SignalScripts.SCRExternalFloats.APPROACH_CONTROL_REQ_SPEED:
                            return_value = thisHead.ApproachControlLimitSpeedMpS.HasValue ? Convert.ToInt32(thisHead.ApproachControlLimitSpeedMpS.Value) : -1;
                            break;

                        default:
                            break;
                    }
                    break;

                // Local float

                case (SignalScripts.SCRTermType.LocalFloat):
                    return_value = localFloats[thisParameter.PartParameter];
                    break;

                // all others : constants

                default:
                    return_value = thisParameter.PartParameter;
                    break;

            }

            return return_value;
        }

        //================================================================================================//
        //
        // return function value
        // Possible functions :
        //                        BLOCK_STATE
        //                        ROUTE_SET
        //                        NEXT_SIG_LR
        //                        NEXT_SIG_MR
        //                        THIS_SIG_LR
        //                        THIS_SIG_MR
        //                        OPP_SIG_LR
        //                        OPP_SIG_MR
        //                        DIST_MULTI_SIG_MR
        //                        SIG_FEATURE
        //                        DEF_DRAW_STATE
        //                        HASHEAD
        //                        DEBUG_HEADER   (does not return a value)
        //                        DEBUG_OUT      (does not return a value)
        //
        //================================================================================================//

        public int SH_function_value(SignalHead thisHead, SignalScripts.SCRScripts.SCRStatTerm thisTerm,
                    int[] localFloats, SIGSCRfile sigscr)
        {

            int return_value = 0;
            int parameter1_value = 0;
            int parameter2_value = 0;

            // extract parameters (max. 2)

            if (thisTerm.PartParameter != null)
            {
                if (thisTerm.PartParameter.Length >= 1)
                {
                    SignalScripts.SCRScripts.SCRParameterType thisParameter = thisTerm.PartParameter[0];
                    parameter1_value = SH_termvalue(thisHead, thisParameter,
                        localFloats, sigscr);
                }

                if (thisTerm.PartParameter.Length >= 2)
                {
                    SignalScripts.SCRScripts.SCRParameterType thisParameter = thisTerm.PartParameter[1];
                    parameter2_value = SH_termvalue(thisHead, thisParameter,
                        localFloats, sigscr);
                }
            }

            // switch on function

            SignalScripts.SCRExternalFunctions thisFunction = thisTerm.Function;
            String dumpfile = String.Empty;

            switch (thisFunction)
            {

                // BlockState

                case (SignalScripts.SCRExternalFunctions.BLOCK_STATE):
                    return_value = (int)thisHead.mainSignal.block_state();
                    break;

                // Route set

                case (SignalScripts.SCRExternalFunctions.ROUTE_SET):
                    return_value = (int)thisHead.route_set();
                    break;

                // next_sig_lr

                case (SignalScripts.SCRExternalFunctions.NEXT_SIG_LR):
                    return_value = (int)thisHead.next_sig_lr((MstsSignalFunction)parameter1_value);
#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt",
                                " NEXT_SIG_LR : Located signal : " +
                                               thisHead.mainSignal.sigfound[parameter1_value].ToString() + "\n");
                    }
#endif
#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        var sob = new StringBuilder();
                        sob.AppendFormat(" NEXT_SIG_LR : Located signal : {0}", thisHead.mainSignal.sigfound[parameter1_value].ToString());

                        if (thisHead.mainSignal.sigfound[parameter1_value] > 0)
                        {
                            SignalObject otherSignal = thisHead.mainSignal.signalRef.SignalObjects[thisHead.mainSignal.sigfound[parameter1_value]];
                            sob.AppendFormat(" (");

                            foreach (SignalHead otherHead in otherSignal.SignalHeads)
                            {
                                sob.AppendFormat(" {0} ", otherHead.TDBIndex);
                            }

                            sob.AppendFormat(") ");
                        }
                        sob.AppendFormat("\n");

                        File.AppendAllText(dpr_fileLoc + @"printproc.txt", sob.ToString());
                    }
#endif

                    break;

                // next_sig_mr

                case (SignalScripts.SCRExternalFunctions.NEXT_SIG_MR):
                    return_value = (int)thisHead.next_sig_mr((MstsSignalFunction)parameter1_value);
#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt",
                                " NEXT_SIG_LR : Located signal : " +
                                               thisHead.mainSignal.sigfound[parameter1_value].ToString() + "\n");
                    }
#endif
#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        File.AppendAllText(dpr_fileLoc + @"printproc.txt",
                                        " NEXT_SIG_LR : Located signal : " +
                                               thisHead.mainSignal.sigfound[parameter1_value].ToString() + "\n");
                    }
#endif
                    break;

                // this_sig_lr

                case (SignalScripts.SCRExternalFunctions.THIS_SIG_LR):
                    bool sigfound_lr = false;
                    MstsSignalAspect returnState_lr = thisHead.this_sig_lr((MstsSignalFunction)parameter1_value, ref sigfound_lr);
                    return_value = sigfound_lr ? (int)returnState_lr : -1;
                    break;

                // this_sig_mr

                case (SignalScripts.SCRExternalFunctions.THIS_SIG_MR):
                    bool sigfound_mr = false;
                    MstsSignalAspect returnState_mr = thisHead.this_sig_mr((MstsSignalFunction)parameter1_value, ref sigfound_mr);
                    return_value = sigfound_mr ? (int)returnState_mr : -1;
                    break;

                // opp_sig_lr

                case (SignalScripts.SCRExternalFunctions.OPP_SIG_LR):
                    return_value = (int)thisHead.opp_sig_lr((MstsSignalFunction)parameter1_value);
#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        SignalObject foundSignal = null;
                        int dummy = (int)thisHead.opp_sig_lr((MstsSignalFunction)parameter1_value, ref foundSignal);
                        int foundRef = foundSignal != null ? foundSignal.thisRef : -1;
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt",
                                " OPP_SIG_LR : Located signal : " + foundRef.ToString() + "\n");
                    }
#endif
#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        SignalObject foundSignal = null;
                        int dummy = (int)thisHead.opp_sig_lr((MstsSignalFunction)parameter1_value, ref foundSignal);
                        int foundRef = foundSignal != null ? foundSignal.thisRef : -1;
                        File.AppendAllText(dpr_fileLoc + @"printproc.txt",
                                " OPP_SIG_LR : Located signal : " + foundRef.ToString() + "\n");
                    }
#endif
                    break;

                // opp_sig_mr

                case (SignalScripts.SCRExternalFunctions.OPP_SIG_MR):
                    return_value = (int)thisHead.opp_sig_mr((MstsSignalFunction)parameter1_value);
                    break;

                // next_nsig_lr

                case (SignalScripts.SCRExternalFunctions.NEXT_NSIG_LR):
                    dumpfile = String.Empty;

#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        dumpfile = String.Concat(dpe_fileLoc, "printproc.txt");
                    }
#endif

#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        dumpfile = String.Concat(dpr_fileLoc,"printproc.txt");
                    }
#endif
                    return_value = (int)thisHead.next_nsig_lr((MstsSignalFunction)parameter1_value, parameter2_value, dumpfile);
                    break;

                // dist_multi_sig_mr

                case (SignalScripts.SCRExternalFunctions.DIST_MULTI_SIG_MR):

                    dumpfile = String.Empty;

#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        dumpfile = String.Concat(dpe_fileLoc, "printproc.txt");
                    }
#endif

#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        dumpfile = String.Concat(dpr_fileLoc,"printproc.txt");
                    }
#endif

                    return_value = (int)thisHead.dist_multi_sig_mr(
                            (MstsSignalFunction)parameter1_value,
                            (MstsSignalFunction)parameter2_value,
                            dumpfile);

                    break;

                // sig_feature

                case (SignalScripts.SCRExternalFunctions.SIG_FEATURE):
                    bool temp_value;
                    temp_value = thisHead.sig_feature(parameter1_value);
                    return_value = Convert.ToInt32(temp_value);
                    break;

                // approach control position

                case (SignalScripts.SCRExternalFunctions.APPROACH_CONTROL_POSITION):
                    dumpfile = String.Empty;

#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        dumpfile = String.Concat(dpe_fileLoc, "printproc.txt");
                    }
#endif

#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        dumpfile = String.Concat(dpr_fileLoc, "printproc.txt");
                    }
#endif
                    temp_value = thisHead.mainSignal.ApproachControlPosition(parameter1_value, dumpfile);
                    return_value = Convert.ToInt32(temp_value);
                    break;

                // approach control speed

                case (SignalScripts.SCRExternalFunctions.APPROACH_CONTROL_SPEED):
                    dumpfile = String.Empty;

#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        dumpfile = String.Concat(dpe_fileLoc, "printproc.txt");
                    }
#endif

#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        dumpfile = String.Concat(dpr_fileLoc, "printproc.txt");
                    }
#endif
                    temp_value = thisHead.mainSignal.ApproachControlSpeed(parameter1_value, parameter2_value, dumpfile);
                    return_value = Convert.ToInt32(temp_value);
                    break;

                // Check for CallOn

                case (SignalScripts.SCRExternalFunctions.TRAINHASCALLON):
                    dumpfile = String.Empty;

#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        dumpfile = String.Concat(dpe_fileLoc, "printproc.txt");
                    }
#endif

#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        dumpfile = String.Concat(dpr_fileLoc, "printproc.txt");
                    }
#endif
                    temp_value = thisHead.mainSignal.TrainHasCallOn(true, dumpfile);
                    return_value = Convert.ToInt32(temp_value);
                    break;

                // Check for CallOn

                case (SignalScripts.SCRExternalFunctions.TRAINHASCALLON_RESTRICTED):
                    dumpfile = String.Empty;

#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        dumpfile = String.Concat(dpe_fileLoc, "printproc.txt");
                    }
#endif

#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        dumpfile = String.Concat(dpr_fileLoc, "printproc.txt");
                    }
#endif
                    temp_value = thisHead.mainSignal.TrainHasCallOn(false, dumpfile);
                    return_value = Convert.ToInt32(temp_value);
                    break;

                case (SignalScripts.SCRExternalFunctions.HASHEAD):
                    return_value = thisHead.mainSignal.HasHead(parameter1_value);
#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt",
                                " HASHEAD : required head : " + parameter1_value + " ; state :  " + return_value  + "\n");
                    }
#endif
#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt",
                                " HASHEAD : required head : " + parameter1_value + " ; state :  " + return_value  + "\n");
                    }
#endif
                    break;

                // def_draw_state

                case (SignalScripts.SCRExternalFunctions.DEF_DRAW_STATE):
                    return_value = thisHead.def_draw_state((MstsSignalAspect)parameter1_value);
                    break;

                // DEBUG routine : to be implemented later

                default:
                    break;
            }

#if DEBUG_PRINT_ENABLED
            if (thisHead.mainSignal.enabledTrain != null)
            {
                File.AppendAllText(dpe_fileLoc + @"printproc.txt",
                        "Function Result : " + thisFunction.ToString() + "(" + parameter1_value.ToString() + ") = " +
                        return_value.ToString() + "\n");
            }
#endif
#if DEBUG_PRINT_PROCESS
            if (TDB_debug_ref.Contains(thisHead.TDBIndex))
            {
                File.AppendAllText(dpr_fileLoc + @"printproc.txt",
                                "Function Result : " + thisFunction.ToString() + "(" + parameter1_value.ToString() + ") = " +
                                return_value.ToString() + "\n");
            }
#endif

            // check sign

            if (thisTerm.TermOperator == SignalScripts.SCRTermOperator.MINUS)
            {
                return_value = -(return_value);
            }

            return return_value;
        }

        //================================================================================================//
        //
        // check IF conditions
        //
        //================================================================================================//

        public bool SH_processIfCondition(SignalHead thisHead, SignalScripts.SCRScripts.SCRConditionBlock thisCond,
                    int[] localFloats, SIGSCRfile sigscr)
        {

            //                                SCRScripts.SCRConditionBlock thisCond = (SCRScripts.SCRConditionBlock) scriptstat;
            //                                SH_processIfCondition(thisHead, thisCond, localFloats, sigscr);
            //                                public ArrayList       Conditions;
            //                                public SCRBlock        IfBlock;
            //                                public List <SCRBlock> ElseIfBlock;
            //                                public SCRBlock        ElseBlock;

            bool condition = true;
            bool performed = false;

            // check condition

            condition = SH_processConditionStatement(thisHead, thisCond.Conditions, localFloats, sigscr);

            // if set : execute IF block

            if (condition)
            {
                if (!SH_process_StatementBlock(thisHead, thisCond.IfBlock.Statements, localFloats, sigscr))
                    return false;
                performed = true;
            }

            // not set : check through ELSEIF

            if (!performed)
            {
                int totalElseIf;
                if (thisCond.ElseIfBlock == null)
                {
                    totalElseIf = 0;
                }
                else
                {
                    totalElseIf = thisCond.ElseIfBlock.Count;
                }

                for (int ielseif = 0; ielseif < totalElseIf && !performed; ielseif++)
                {

                    // first (and only ) entry in ELSEIF block must be IF condition - extract condition

                    object elseifStat = thisCond.ElseIfBlock[ielseif].Statements[0];
                    if (elseifStat is SignalScripts.SCRScripts.SCRConditionBlock)
                    {
                        SignalScripts.SCRScripts.SCRConditionBlock elseifCond =
                                (SignalScripts.SCRScripts.SCRConditionBlock)elseifStat;

                        condition = SH_processConditionStatement(thisHead, elseifCond.Conditions,
                                localFloats, sigscr);

                        if (condition)
                        {
                            if (!SH_process_StatementBlock(thisHead, elseifCond.IfBlock.Statements,
                                    localFloats, sigscr))
                                return false;
                            performed = true;
                        }
                    }
                }
            }

            // ELSE block

            if (!performed && thisCond.ElseBlock != null)
            {
                if (!SH_process_StatementBlock(thisHead, thisCond.ElseBlock.Statements, localFloats, sigscr))
                    return false;
            }

            return true;
        }

        //================================================================================================//
        //
        // process condition statement
        //
        //================================================================================================//

        public bool SH_processConditionStatement(SignalHead thisHead, ArrayList thisCStatList,
                    int[] localFloats, SIGSCRfile sigscr)
        {

            // loop through all conditions

            bool condition = true;
            bool newcondition = true;
            bool termnegate = false;
            SignalScripts.SCRAndOr condstring = SignalScripts.SCRAndOr.NONE;

            foreach (object thisCond in thisCStatList)
            {

                // single condition : process

                if (thisCond is SignalScripts.SCRNegate)
                {
                    termnegate = true;
                }

                else if (thisCond is SignalScripts.SCRScripts.SCRConditions)
                {
                    SignalScripts.SCRScripts.SCRConditions thisSingleCond = (SignalScripts.SCRScripts.SCRConditions)thisCond;
                    newcondition = SH_processSingleCondition(thisHead, thisSingleCond, localFloats, sigscr);

                    if (termnegate)
                    {
                        termnegate = false;
                        newcondition = newcondition ? false : true;
                    }

                    switch (condstring)
                    {
                        case (SignalScripts.SCRAndOr.AND):
                            condition &= newcondition;
                            break;

                        case (SignalScripts.SCRAndOr.OR):
                            condition |= newcondition;
                            break;

                        default:
                            condition = newcondition;
                            break;
                    }
                }

  // AND or OR indication (to link previous and next part)

                else if (thisCond is SignalScripts.SCRAndOr)
                {
                    condstring = (SignalScripts.SCRAndOr)thisCond;
                }

  // subcondition

                else
                {
                    ArrayList subCond = (ArrayList)thisCond;
                    newcondition = SH_processConditionStatement(thisHead, subCond, localFloats, sigscr);

                    if (termnegate)
                    {
                        termnegate = false;
                        newcondition = newcondition ? false : true;
                    }

                    switch (condstring)
                    {
                        case (SignalScripts.SCRAndOr.AND):
                            condition &= newcondition;
                            break;

                        case (SignalScripts.SCRAndOr.OR):
                            condition |= newcondition;
                            break;

                        default:
                            condition = newcondition;
                            break;
                    }
                }

            }

            return condition;
        }

        //================================================================================================//
        //
        // process single condition
        //
        //================================================================================================//

        public bool SH_processSingleCondition(SignalHead thisHead, SignalScripts.SCRScripts.SCRConditions thisCond,
                    int[] localFloats, SIGSCRfile sigscr)
        {

            int term1value = 0;
            int term2value = 0;
            bool condition = true;

            // get value of first term


#if DEBUG_PRINT_ENABLED
            if (thisHead.mainSignal.enabledTrain != null)
            {
                File.AppendAllText(dpe_fileLoc + @"printproc.txt", "IF Condition statement (1) : \n");
            }
#endif
#if DEBUG_PRINT_PROCESS
            if (TDB_debug_ref.Contains(thisHead.TDBIndex))
            {
                File.AppendAllText(dpr_fileLoc + @"printproc.txt", "IF Condition statement (1) : \n");
            }
#endif

            if (thisCond.Term1.Function != SignalScripts.SCRExternalFunctions.NONE)
            {
                term1value = SH_function_value(thisHead, thisCond.Term1, localFloats, sigscr);
            }
            else if (thisCond.Term1.PartParameter != null)
            {
                SignalScripts.SCRScripts.SCRParameterType thisParameter = thisCond.Term1.PartParameter[0];

#if DEBUG_PRINT_ENABLED
                if (thisHead.mainSignal.enabledTrain != null)
                {
                    File.AppendAllText(dpe_fileLoc + @"printproc.txt", "Parameter : " + thisParameter.PartType.ToString() + " : " +
                            thisParameter.PartParameter.ToString() + "\n");
                }
#endif
#if DEBUG_PRINT_PROCESS
                if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                {
                    File.AppendAllText(dpr_fileLoc + @"printproc.txt", "Parameter : " + thisParameter.PartType.ToString() + " : " +
                                    thisParameter.PartParameter.ToString() + "\n");
                }
#endif

                SignalScripts.SCRTermOperator thisOperator = thisCond.Term1.TermOperator;
                term1value = SH_termvalue(thisHead, thisParameter,
                        localFloats, sigscr);
                if (thisOperator == SignalScripts.SCRTermOperator.MINUS)
                {
                    term1value = -term1value;
                }
            }

            // get value of second term

            if (thisCond.Term2 == null)

            // if only one value : check for NOT
            {
                if (thisCond.negate1)
                {
                    condition = !(Convert.ToBoolean(term1value));
                }
                else
                {
                    condition = Convert.ToBoolean(term1value);
                }

#if DEBUG_PRINT_ENABLED
                if (thisHead.mainSignal.enabledTrain != null)
                {
                    File.AppendAllText(dpe_fileLoc + @"printproc.txt", "Result of single condition : " +
                        " : " + condition.ToString() + " (NOT : " + thisCond.negate1.ToString() + ")\n\n");
                }
#endif
#if DEBUG_PRINT_PROCESS
                if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                {
                    File.AppendAllText(dpr_fileLoc + @"printproc.txt", "Result of single condition : " +
                            " : " + condition.ToString() + " (NOT : " + thisCond.negate1.ToString() + ")\n\n");
                }
#endif
            }

  // process second term

            else
            {

#if DEBUG_PRINT_ENABLED
                if (thisHead.mainSignal.enabledTrain != null)
                {
                    File.AppendAllText(dpe_fileLoc + @"printproc.txt", "IF Condition statement (2) : \n");
                }
#endif
#if DEBUG_PRINT_PROCESS
                if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                {
                    File.AppendAllText(dpr_fileLoc + @"printproc.txt", "IF Condition statement (2) : \n");
                }
#endif

                if (thisCond.Term2.Function != SignalScripts.SCRExternalFunctions.NONE)
                {
                    term2value = SH_function_value(thisHead, thisCond.Term2, localFloats, sigscr);
                }
                else if (thisCond.Term2.PartParameter != null)
                {
                    SignalScripts.SCRScripts.SCRParameterType thisParameter = thisCond.Term2.PartParameter[0];

#if DEBUG_PRINT_ENABLED
                    if (thisHead.mainSignal.enabledTrain != null)
                    {
                        File.AppendAllText(dpe_fileLoc + @"printproc.txt",
                            "Parameter : " + thisParameter.PartType.ToString() + " : " +
                            thisParameter.PartParameter.ToString() + "\n");
                    }
#endif
#if DEBUG_PRINT_PROCESS
                    if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                    {
                        File.AppendAllText(dpr_fileLoc + @"printproc.txt",
                                "Parameter : " + thisParameter.PartType.ToString() + " : " +
                                thisParameter.PartParameter.ToString() + "\n");
                    }
#endif

                    SignalScripts.SCRTermOperator thisOperator = thisCond.Term2.TermOperator;
                    term2value = SH_termvalue(thisHead, thisParameter,
                        localFloats, sigscr);
                    if (thisOperator == SignalScripts.SCRTermOperator.MINUS)
                    {
                        term2value = -term2value;
                    }
                }

                // check on required condition

                switch (thisCond.Condition)
                {

                    // GT

                    case (SignalScripts.SCRTermCondition.GT):
                        condition = (term1value > term2value);
                        break;

                    // GE

                    case (SignalScripts.SCRTermCondition.GE):
                        condition = (term1value >= term2value);
                        break;

                    // LT

                    case (SignalScripts.SCRTermCondition.LT):
                        condition = (term1value < term2value);
                        break;

                    // LE

                    case (SignalScripts.SCRTermCondition.LE):
                        condition = (term1value <= term2value);
                        break;

                    // EQ

                    case (SignalScripts.SCRTermCondition.EQ):
                        condition = (term1value == term2value);
                        break;

                    // NE

                    case (SignalScripts.SCRTermCondition.NE):
                        condition = (term1value != term2value);
                        break;
                }

#if DEBUG_PRINT_ENABLED
                if (thisHead.mainSignal.enabledTrain != null)
                {
                    File.AppendAllText(dpe_fileLoc + @"printproc.txt", "Result of operation : " +
                        thisCond.Condition.ToString() + " : " + condition.ToString() + "\n\n");
                }
#endif
#if DEBUG_PRINT_PROCESS
                if (TDB_debug_ref.Contains(thisHead.TDBIndex))
                {
                    File.AppendAllText(dpr_fileLoc + @"printproc.txt", "Result of operation : " +
                            thisCond.Condition.ToString() + " : " + condition.ToString() + "\n\n");
                }
#endif
            }


            return condition;
        }

        //================================================================================================//

    }
}

