namespace OpenSim.Region.ScriptEngine.Common.TRPC
{
    public class TCPCommon
    {
        public delegate void ClientConnectedDelegate(int ID, System.Net.EndPoint Remote);
        public delegate void DataReceivedDelegate(int ID, byte[] data, int offset, int length);
        public delegate void DataSentDelegate(int ID, int length);
        public delegate void CloseDelegate(int ID);
        public delegate void ConnectErrorDelegate(string Reason);


        public interface ServerAndClientInterface
        {
            void Send(int clientID, byte[] data, int offset, int len);
            event ClientConnectedDelegate ClientConnected;
            event DataReceivedDelegate DataReceived;
            event DataSentDelegate DataSent;
            event CloseDelegate Close;
        }
        public interface ClientInterface : ServerAndClientInterface
        {
            event TCPCommon.ConnectErrorDelegate ConnectError;
            void Connect(string RemoteHost, int RemotePort);
            void Disconnect(int ID);
        }
        public interface ServerInterface : ServerAndClientInterface
        {
            void StartListen();
            void StopListen();
        }

    }
}