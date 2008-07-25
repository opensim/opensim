using System;
using libsecondlife;

namespace OpenSim.Framework
{
    public interface IEventArgs
    {
        IScene Scene { get; set; }
        IClientAPI Sender { get; set; }
    }

    /// <summary>
    /// ChatFromViewer Arguments
    /// </summary>
    public class OSChatMessage : EventArgs, IEventArgs
    {
        protected int m_channel;
        protected string m_from;
        protected string m_message;
        protected LLVector3 m_position;

        protected IScene m_scene;
        protected IClientAPI m_sender;
        protected object m_senderObject;
        protected ChatTypeEnum m_type;
        protected LLUUID m_fromID;

        public OSChatMessage()
        {
            m_position = new LLVector3();
        }

        /// <summary>
        /// The message sent by the user
        /// </summary>
        public string Message
        {
            get { return m_message; }
            set { m_message = value; }
        }

        /// <summary>
        /// The type of message, eg say, shout, broadcast.
        /// </summary>
        public ChatTypeEnum Type
        {
            get { return m_type; }
            set { m_type = value; }
        }

        /// <summary>
        /// Which channel was this message sent on? Different channels may have different listeners. Public chat is on channel zero.
        /// </summary>
        public int Channel
        {
            get { return m_channel; }
            set { m_channel = value; }
        }

        /// <summary>
        /// The position of the sender at the time of the message broadcast.
        /// </summary>
        public LLVector3 Position
        {
            get { return m_position; }
            set { m_position = value; }
        }

        /// <summary>
        /// The name of the sender (needed for scripts)
        /// </summary>
        public string From
        {
            get { return m_from; }
            set { m_from = value; }
        }

        #region IEventArgs Members

        /// TODO: Sender and SenderObject should just be Sender and of
        /// type IChatSender

        /// <summary>
        /// The client responsible for sending the message, or null.
        /// </summary>
        public IClientAPI Sender
        {
            get { return m_sender; }
            set { m_sender = value; }
        }

        /// <summary>
        /// The object responsible for sending the message, or null.
        /// </summary>
        public object SenderObject
        {
            get { return m_senderObject; }
            set { m_senderObject = value; }
        }

        public LLUUID SenderUUID
        {
            get { return m_fromID; }
            set { m_fromID = value; }
        }

        /// <summary>
        ///
        /// </summary>
        public IScene Scene
        {
            get { return m_scene; }
            set { m_scene = value; }
        }

        #endregion
    }
}