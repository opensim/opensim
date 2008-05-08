/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Scenes
{
    public class SceneExternalChecks
    {
        private Scene m_scene;

        public SceneExternalChecks(Scene scene)
        {
            m_scene = scene;
        }

        #region Object Permission Checks

            #region REZ OBJECT
            public delegate bool CanRezObject(int objectCount, LLUUID owner, LLVector3 objectPosition, Scene scene);
            private List<CanRezObject> CanRezObjectCheckFunctions = new List<CanRezObject>();

            public void addCheckRezObject(CanRezObject delegateFunc)
            {
                if(!CanRezObjectCheckFunctions.Contains(delegateFunc))
                    CanRezObjectCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckRezObject(CanRezObject delegateFunc)
            {
                if (CanRezObjectCheckFunctions.Contains(delegateFunc))
                    CanRezObjectCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanRezObject(int objectCount, LLUUID owner, LLVector3 objectPosition)
            {
                foreach (CanRezObject check in CanRezObjectCheckFunctions)
                {
                    if (check(objectCount, owner,objectPosition, m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region DEREZ OBJECT
            public delegate bool CanDeRezObject(LLUUID objectID, LLUUID deleter, Scene scene);
            private List<CanDeRezObject> CanDeRezObjectCheckFunctions = new List<CanDeRezObject>();

            public void addCheckDeRezObject(CanDeRezObject delegateFunc)
            {
                if (!CanDeRezObjectCheckFunctions.Contains(delegateFunc))
                    CanDeRezObjectCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckDeRezObject(CanDeRezObject delegateFunc)
            {
                if (CanDeRezObjectCheckFunctions.Contains(delegateFunc))
                    CanDeRezObjectCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanDeRezObject(LLUUID objectID, LLUUID deleter)
            {
                foreach (CanDeRezObject check in CanDeRezObjectCheckFunctions)
                {
                    if (check(objectID,deleter,m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region TAKE OBJECT
            public delegate bool CanTakeObject(LLUUID objectID, LLUUID stealer, Scene scene);
            private List<CanTakeObject> CanTakeObjectCheckFunctions = new List<CanTakeObject>();

            public void addCheckTakeObject(CanTakeObject delegateFunc)
            {
                if (!CanTakeObjectCheckFunctions.Contains(delegateFunc))
                    CanTakeObjectCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckTakeObject(CanTakeObject delegateFunc)
            {
                if (CanTakeObjectCheckFunctions.Contains(delegateFunc))
                    CanTakeObjectCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanTakeObject(LLUUID objectID, LLUUID stealer)
            {
                foreach (CanTakeObject check in CanTakeObjectCheckFunctions)
                {
                    if (check(objectID, stealer, m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region COPY OBJECT
            public delegate bool CanCopyObject(int objectCount, LLUUID objectID, LLUUID owner, Scene scene, LLVector3 objectPosition);
            private List<CanCopyObject> CanCopyObjectCheckFunctions = new List<CanCopyObject>();

            public void addCheckCopyObject(CanCopyObject delegateFunc)
            {
                if (!CanCopyObjectCheckFunctions.Contains(delegateFunc))
                    CanCopyObjectCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckCopyObject(CanCopyObject delegateFunc)
            {
                if (CanCopyObjectCheckFunctions.Contains(delegateFunc))
                    CanCopyObjectCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanCopyObject(int objectCount, LLUUID objectID, LLUUID owner, LLVector3 objectPosition)
            {
                foreach (CanCopyObject check in CanCopyObjectCheckFunctions)
                {
                    if (check(objectCount, objectID, owner, m_scene, objectPosition) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region EDIT OBJECT
            public delegate bool CanEditObject(LLUUID objectID, LLUUID editorID, Scene scene);
            private List<CanEditObject> CanEditObjectCheckFunctions = new List<CanEditObject>();

            public void addCheckEditObject(CanEditObject delegateFunc)
            {
                if (!CanEditObjectCheckFunctions.Contains(delegateFunc))
                    CanEditObjectCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckEditObject(CanEditObject delegateFunc)
            {
                if (CanEditObjectCheckFunctions.Contains(delegateFunc))
                    CanEditObjectCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanEditObject(LLUUID objectID, LLUUID editorID)
            {
                foreach (CanEditObject check in CanEditObjectCheckFunctions)
                {
                    if (check(objectID, editorID, m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region MOVE OBJECT
            public delegate bool CanMoveObject(LLUUID objectID, LLUUID moverID, Scene scene);
            private List<CanMoveObject> CanMoveObjectCheckFunctions = new List<CanMoveObject>();

            public void addCheckMoveObject(CanMoveObject delegateFunc)
            {
                if (!CanMoveObjectCheckFunctions.Contains(delegateFunc))
                    CanMoveObjectCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckMoveObject(CanMoveObject delegateFunc)
            {
                if (CanMoveObjectCheckFunctions.Contains(delegateFunc))
                    CanMoveObjectCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanMoveObject(LLUUID objectID, LLUUID moverID)
            {
                foreach (CanMoveObject check in CanMoveObjectCheckFunctions)
                {
                    if (check(objectID,moverID,m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region RETURN OBJECT
            public delegate bool CanReturnObject(LLUUID objectID, LLUUID returnerID, Scene scene);
            private List<CanReturnObject> CanReturnObjectCheckFunctions = new List<CanReturnObject>();

            public void addCheckReturnObject(CanReturnObject delegateFunc)
            {
                if (!CanReturnObjectCheckFunctions.Contains(delegateFunc))
                    CanReturnObjectCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckReturnObject(CanReturnObject delegateFunc)
            {
                if (CanReturnObjectCheckFunctions.Contains(delegateFunc))
                    CanReturnObjectCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanReturnObject(LLUUID objectID, LLUUID returnerID)
            {
                foreach (CanReturnObject check in CanReturnObjectCheckFunctions)
                {
                    if (check(objectID,returnerID,m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

        #endregion

        #region Misc Permission Checks

            #region INSTANT MESSAGE
            public delegate bool CanInstantMessage(LLUUID user, LLUUID target, Scene startScene);
            private List<CanInstantMessage> CanInstantMessageCheckFunctions = new List<CanInstantMessage>();

            public void addCheckInstantMessage(CanInstantMessage delegateFunc)
            {
                if (!CanInstantMessageCheckFunctions.Contains(delegateFunc))
                    CanInstantMessageCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckInstantMessage(CanInstantMessage delegateFunc)
            {
                if (CanInstantMessageCheckFunctions.Contains(delegateFunc))
                    CanInstantMessageCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanInstantMessage(LLUUID user, LLUUID target)
            {
                foreach (CanInstantMessage check in CanInstantMessageCheckFunctions)
                {
                    if (check(user,target,m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region INVENTORY TRANSFER
            public delegate bool CanInventoryTransfer(LLUUID user, LLUUID target, Scene startScene);
            private List<CanInventoryTransfer> CanInventoryTransferCheckFunctions = new List<CanInventoryTransfer>();

            public void addCheckInventoryTransfer(CanInventoryTransfer delegateFunc)
            {
                if (!CanInventoryTransferCheckFunctions.Contains(delegateFunc))
                    CanInventoryTransferCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckInventoryTransfer(CanInventoryTransfer delegateFunc)
            {
                if (CanInventoryTransferCheckFunctions.Contains(delegateFunc))
                    CanInventoryTransferCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanInventoryTransfer(LLUUID user, LLUUID target)
            {
                foreach (CanInventoryTransfer check in CanInventoryTransferCheckFunctions)
                {
                    if (check(user, target, m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region EDIT SCRIPT
            public delegate bool CanEditScript(LLUUID script, LLUUID user, Scene scene);
            private List<CanEditScript> CanEditScriptCheckFunctions = new List<CanEditScript>();

            public void addCheckEditScript(CanEditScript delegateFunc)
            {
                if (!CanEditScriptCheckFunctions.Contains(delegateFunc))
                    CanEditScriptCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckEditScript(CanEditScript delegateFunc)
            {
                if (CanEditScriptCheckFunctions.Contains(delegateFunc))
                    CanEditScriptCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanEditScript(LLUUID script, LLUUID user)
            {
                foreach (CanEditScript check in CanEditScriptCheckFunctions)
                {
                    if (check(script, user, m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region RUN SCRIPT
            public delegate bool CanRunScript(LLUUID script, LLUUID user, Scene scene);
            private List<CanRunScript> CanRunScriptCheckFunctions = new List<CanRunScript>();

            public void addCheckRunScript(CanRunScript delegateFunc)
            {
                if (!CanRunScriptCheckFunctions.Contains(delegateFunc))
                    CanRunScriptCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckRunScript(CanRunScript delegateFunc)
            {
                if (CanRunScriptCheckFunctions.Contains(delegateFunc))
                    CanRunScriptCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanRunScript(LLUUID script, LLUUID user)
            {
                foreach (CanRunScript check in CanRunScriptCheckFunctions)
                {
                    if (check(script, user, m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region TERRAFORM LAND
            public delegate bool CanTerraformLandCommand(LLUUID user, LLVector3 position, Scene requestFromScene);
            private List<CanTerraformLandCommand> CanTerraformLandCommandCheckFunctions = new List<CanTerraformLandCommand>();

            public void addCheckTerraformLandCommand(CanTerraformLandCommand delegateFunc)
            {
                if (!CanTerraformLandCommandCheckFunctions.Contains(delegateFunc))
                    CanTerraformLandCommandCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckTerraformLandCommand(CanTerraformLandCommand delegateFunc)
            {
                if (CanTerraformLandCommandCheckFunctions.Contains(delegateFunc))
                    CanTerraformLandCommandCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanTerraformLand(LLUUID user, LLVector3 pos)
            {
                foreach (CanTerraformLandCommand check in CanTerraformLandCommandCheckFunctions)
                {
                    if (check(user, pos, m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region RUN CONSOLE COMMAND
            public delegate bool CanRunConsoleCommand(LLUUID user, Scene requestFromScene);
            private List<CanRunConsoleCommand> CanRunConsoleCommandCheckFunctions = new List<CanRunConsoleCommand>();

            public void addCheckRunConsoleCommand(CanRunConsoleCommand delegateFunc)
            {
                if (!CanRunConsoleCommandCheckFunctions.Contains(delegateFunc))
                    CanRunConsoleCommandCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckRunConsoleCommand(CanRunConsoleCommand delegateFunc)
            {
                if (CanRunConsoleCommandCheckFunctions.Contains(delegateFunc))
                    CanRunConsoleCommandCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanRunConsoleCommand(LLUUID user)
            {
                foreach (CanRunConsoleCommand check in CanRunConsoleCommandCheckFunctions)
                {
                    if (check(user, m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

            #region CAN BE GODLIKE
            public delegate bool CanBeGodLike(LLUUID user, Scene requestFromScene);
            private List<CanBeGodLike> CanBeGodLikeCheckFunctions = new List<CanBeGodLike>();

            public void addCheckBeGodLike(CanBeGodLike delegateFunc)
            {
                if (!CanBeGodLikeCheckFunctions.Contains(delegateFunc))
                    CanBeGodLikeCheckFunctions.Add(delegateFunc);
            }
            public void removeCheckBeGodLike(CanBeGodLike delegateFunc)
            {
                if (CanBeGodLikeCheckFunctions.Contains(delegateFunc))
                    CanBeGodLikeCheckFunctions.Remove(delegateFunc);
            }

            public bool ExternalChecksCanBeGodLike(LLUUID user)
            {
                foreach (CanBeGodLike check in CanBeGodLikeCheckFunctions)
                {
                    if (check(user, m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

            #endregion

        #endregion

        #region Parcel and Estate Permission Checks
                #region EDIT ESTATE TERRAIN
                public delegate bool CanEditEstateTerrain(LLUUID user, Scene scene);
                    private List<CanEditEstateTerrain> CanEditEstateTerrainCheckFunctions = new List<CanEditEstateTerrain>();

                    public void addCheckEditEstateTerrain(CanEditEstateTerrain delegateFunc)
                    {
                        if (!CanEditEstateTerrainCheckFunctions.Contains(delegateFunc))
                            CanEditEstateTerrainCheckFunctions.Add(delegateFunc);
                    }
                    public void removeCheckEditEstateTerrain(CanEditEstateTerrain delegateFunc)
                    {
                        if (CanEditEstateTerrainCheckFunctions.Contains(delegateFunc))
                            CanEditEstateTerrainCheckFunctions.Remove(delegateFunc);
                    }

                    public bool ExternalChecksCanEditEstateTerrain(LLUUID user)
                    {
                        foreach (CanEditEstateTerrain check in CanEditEstateTerrainCheckFunctions)
                        {
                            if (check(user, m_scene) == false)
                            {
                                return false;
                            }
                        }
                        return true;
                    }

                #endregion

                #region RESTART SIM
                public delegate bool CanRestartSim(LLUUID user, Scene scene);
                private List<CanRestartSim> CanRestartSimCheckFunctions = new List<CanRestartSim>();

                public void addCheckRestartSim(CanRestartSim delegateFunc)
                {
                    if (!CanRestartSimCheckFunctions.Contains(delegateFunc))
                        CanRestartSimCheckFunctions.Add(delegateFunc);
                }
                public void removeCheckRestartSim(CanRestartSim delegateFunc)
                {
                    if (CanRestartSimCheckFunctions.Contains(delegateFunc))
                        CanRestartSimCheckFunctions.Remove(delegateFunc);
                }

                public bool ExternalChecksCanRestartSim(LLUUID user)
                {
                    foreach (CanRestartSim check in CanRestartSimCheckFunctions)
                    {
                        if (check(user, m_scene) == false)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            #endregion

                #region EDIT PARCEL
                    public delegate bool CanEditParcel(LLUUID user, ILandObject parcel, Scene scene);
                    private List<CanEditParcel> CanEditParcelCheckFunctions = new List<CanEditParcel>();

                    public void addCheckEditParcel(CanEditParcel delegateFunc)
                    {
                        if (!CanEditParcelCheckFunctions.Contains(delegateFunc))
                            CanEditParcelCheckFunctions.Add(delegateFunc);
                    }
                    public void removeCheckEditParcel(CanEditParcel delegateFunc)
                    {
                        if (CanEditParcelCheckFunctions.Contains(delegateFunc))
                            CanEditParcelCheckFunctions.Remove(delegateFunc);
                    }

                    public bool ExternalChecksCanEditParcel(LLUUID user, ILandObject parcel)
                    {
                        foreach (CanEditParcel check in CanEditParcelCheckFunctions)
                        {
                            if (check(user, parcel, m_scene) == false)
                            {
                                return false;
                            }
                        }
                        return true;
                    }
                #endregion

                #region SELL PARCEL
                    public delegate bool CanSellParcel(LLUUID user, ILandObject parcel, Scene scene);
                    private List<CanSellParcel> CanSellParcelCheckFunctions = new List<CanSellParcel>();

                    public void addCheckSellParcel(CanSellParcel delegateFunc)
                    {
                        if (!CanSellParcelCheckFunctions.Contains(delegateFunc))
                            CanSellParcelCheckFunctions.Add(delegateFunc);
                    }
                    public void removeCheckSellParcel(CanSellParcel delegateFunc)
                    {
                        if (CanSellParcelCheckFunctions.Contains(delegateFunc))
                            CanSellParcelCheckFunctions.Remove(delegateFunc);
                    }

                    public bool ExternalChecksCanSellParcel(LLUUID user, ILandObject parcel)
                    {
                        foreach (CanSellParcel check in CanSellParcelCheckFunctions)
                        {
                            if (check(user, parcel, m_scene) == false)
                            {
                                return false;
                            }
                        }
                        return true;
                    }
                #endregion

                #region ABANDON PARCEL
                    public delegate bool CanAbandonParcel(LLUUID user, ILandObject parcel, Scene scene);
                    private List<CanAbandonParcel> CanAbandonParcelCheckFunctions = new List<CanAbandonParcel>();

                    public void addCheckAbandonParcel(CanAbandonParcel delegateFunc)
                    {
                        if (!CanAbandonParcelCheckFunctions.Contains(delegateFunc))
                            CanAbandonParcelCheckFunctions.Add(delegateFunc);
                    }
                    public void removeCheckAbandonParcel(CanAbandonParcel delegateFunc)
                    {
                        if (CanAbandonParcelCheckFunctions.Contains(delegateFunc))
                            CanAbandonParcelCheckFunctions.Remove(delegateFunc);
                    }

                    public bool ExternalChecksCanAbandonParcel(LLUUID user, ILandObject parcel)
                    {
                        foreach (CanAbandonParcel check in CanAbandonParcelCheckFunctions)
                        {
                            if (check(user, parcel, m_scene) == false)
                            {
                                return false;
                            }
                        }
                        return true;
                    }
                #endregion
            #endregion


    }
}
