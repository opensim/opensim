/*
 * Created by SharpDevelop.
 * User: caseyj
 * Date: 25/02/2008
 * Time: 21:30
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules
{
    /// <summary>
    /// Sends a 'texture not found' packet back to the client
    /// </summary>
    public class TextureNotFoundSender : ITextureSender
    {
        //private static readonly log4net.ILog m_log 
        //    = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        private LLUUID m_textureId;
        private IClientAPI m_client;
        
        // See ITextureSender
        public bool Sending 
        { 
            get { return false; }
            set { m_sending = value; }
        }
                    
        private bool m_sending = false;           

        // See ITextureSender
        public bool Cancel 
        { 
            get { return false; }
            set { m_cancel = value; }
        }
                    
        private bool m_cancel = false;      
        
        public TextureNotFoundSender(IClientAPI client, LLUUID textureID)
        {
            m_client = client;
            m_textureId = textureID;
        }
        
        // See ITextureSender
        public void UpdateRequest(int discardLevel, uint packetNumber)
        {
            // Not need to implement since priority changes don't affect this operation
        }
        
        // See ITextureSender
        public bool SendTexturePacket()
        {
            //m_log.InfoFormat(
            //    "[TEXTURE NOT FOUND SENDER]: Informing the client that texture {0} cannot be found", 
            //    m_textureId);
            
            ImageNotInDatabasePacket notFound = new ImageNotInDatabasePacket();
            notFound.ImageID.ID = m_textureId;
            m_client.OutPacket(notFound, ThrottleOutPacketType.Unknown);
            
            return true;
        }
    }
}
