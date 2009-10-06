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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using OpenMetaverse;
using OpenSim.Framework;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A database interface class to a user profile storage system
    /// </summary>
    public class MySqlFramework
    {
        private static readonly log4net.ILog m_log =
                log4net.LogManager.GetLogger(
                System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected MySqlConnection m_Connection;

        protected MySqlFramework(string connectionString)
        {
            m_Connection = new MySqlConnection(connectionString);
            m_Connection.Open();
        }

        //////////////////////////////////////////////////////////////
        //
        // All non queries are funneled through one connection
        // to increase performance a little
        //
        protected int ExecuteNonQuery(MySqlCommand cmd)
        {
            lock (m_Connection)
            {
                cmd.Connection = m_Connection;

                bool errorSeen = false;

                while (true)
                {
                    try
                    {
                        return cmd.ExecuteNonQuery();
                    }
                    catch (MySqlException e)
                    {
                        m_log.Error(e.Message, e);
                        if (errorSeen)
                            throw;

                        // This is "Server has gone away" and "Server lost"
                        if (e.Number == 2006 || e.Number == 2013)
                        {
                            errorSeen = true;

                            m_Connection.Close();
                            MySqlConnection newConnection =
                                    (MySqlConnection)((ICloneable)m_Connection).Clone();
                            m_Connection.Dispose();
                            m_Connection = newConnection;
                            m_Connection.Open();

                            cmd.Connection = m_Connection;
                        }
                        else
                            throw;
                    }
                    catch (Exception e)
                    {
                        m_log.Error(e.Message, e);
                        return 0;
                    }
                }
            }
        }
        
        protected IDataReader ExecuteReader(MySqlCommand cmd)
        {
            MySqlConnection newConnection =
                    (MySqlConnection)((ICloneable)m_Connection).Clone();
            newConnection.Open();

            cmd.Connection = newConnection;
            return cmd.ExecuteReader();
        }

        protected void CloseDBConnection(IDataReader reader, MySqlCommand cmd)
        {
            reader.Close();
            cmd.Connection.Close();
            cmd.Connection.Dispose();
        }
    }
}
