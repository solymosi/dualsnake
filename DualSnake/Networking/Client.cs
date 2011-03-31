using System;
using System.Net.Sockets;
using System.Text;
using System.Net;

namespace Solymosi.Networking.Sockets
{
    /// <summary>
    /// Implements a greatly simplified wrapper for System.Net.Sockets.Socket.
    /// </summary>
    public class Client
    {

        #region Delegates

        /// <summary>
        /// References a method to be called when a connection is established.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A System.EventArgs object that contains the event data.</param>
        public delegate void ConnectDelegate(object sender, EventArgs e);

        /// <summary>
        /// References a method to be called when data is successfully sent to the remote host.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A Client.TransmitEventArgs object that contains the sent data.</param>
        public delegate void SendDelegate(object sender, TransmitEventArgs e);

        /// <summary>
        /// References a method to be called when data is successfully received from the remote host.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A Client.TransmitEventArgs object that contains the received data.</param>
        public delegate void ReceiveDelegate(object sender, TransmitEventArgs e);

        /// <summary>
        /// References a method to be called when the connection is closed or dropped.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A Client.CloseEventArgs object that contains the reason the connection was closed.</param>
        public delegate void CloseDelegate(object sender, CloseEventArgs e);

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a connection is established.
        /// </summary>
        public event ConnectDelegate Connected = delegate { };

        /// <summary>
        /// Occurs when data is successfully sent to the remote host.
        /// </summary>
        public event SendDelegate Sent = delegate { };

        /// <summary>
        /// Occurs when data is successfully received from the remote host.
        /// </summary>
        public event ReceiveDelegate Received = delegate { };

        /// <summary>
        /// Occurs when the connection is closed or dropped.
        /// </summary>
        public event CloseDelegate Closed = delegate { };

        #endregion

        #region Protected properties

        /// <summary>
        /// The underlying System.Net.Sockets.Socket instance.
        /// </summary>
        protected Socket Sock;

        /// <summary>
        /// Protected variable to keep track of connection status.
        /// </summary>
        protected bool _Connected = false;

        /// <summary>
        /// Buffer for the underlying System.Net.Sockets.Socket instance to write received data to.
        /// </summary>
        protected byte[] ReceiveBuffer = new byte[4096];

        /// <summary>
        /// Buffer to collect received data until the receive operation is completed.
        /// </summary>
        protected byte[] ReceivedData = new byte[0];

        #endregion

        #region Public properties

        /// <summary>
        /// The underlying System.Net.Sockets.Socket instance.
        /// </summary>
        public Socket Socket
        {
            get { return this.Sock; }
            set
            {
                this.Sock = value;
                if (this.Sock.Connected) { this._Connected = true; }
            }
        }

        /// <summary>
        /// Gets whether this Client instance has an active connection to a remote endpoint.
        /// </summary>
        public bool IsConnected { get { return this._Connected; } }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new Solymosi.Networking.Sockets.Client instance with default properties.
        /// </summary>
        public Client() : this(null) { }

        /// <summary>
        /// Initializes a new Solymosi.Networking.Sockets.Client instance with an existing System.Net.Sockets.Socket object.
        /// </summary>
        /// <param name="Socket">The existing System.Net.Sockets.Socket instance to use.</param>
        public Client(Socket Socket)
        {
            this.Socket = (Socket == null ? new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) : Socket);
            this.Closed += new CloseDelegate(ControlSocket_Closed);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Override this method to add code to be exetuted right after this Client instance is created.
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// Throws an InvalidOperationException if this Client is not currently connected to a remote host.
        /// </summary>
        protected void RequireConnection()
        {
            if (!IsConnected) { throw new InvalidOperationException("Client not connected."); }
        }

        #endregion

        #region Connect

        /// <summary>
        /// Begins connecting to the specified remote host and port.
        /// </summary>
        /// <param name="Host">The remote host to connect to.</param>
        /// <param name="Port">The remote port to connect to.</param>
        public void Connect(string Host, int Port)
        {
            if (this.IsConnected) { throw new InvalidOperationException("Client already connected."); }
            try { Sock.BeginConnect(Host, Port, new AsyncCallback(ConnectCallback), new object()); }
            catch (Exception e) { this.Abort(e); }
        }

        /// <summary>
        /// Begins connecting to the specified remote endpoint.
        /// </summary>
        /// <param name="EndPoint">The remote endpoint to connect to.</param>
        public void Connect(IPEndPoint EndPoint)
        {
            if (this.IsConnected) { throw new InvalidOperationException("Client already connected."); }
            try { Sock.BeginConnect(EndPoint, new AsyncCallback(ConnectCallback), new object()); }
            catch (Exception e) { this.Abort(e); }
        }

        /// <summary>
        /// Called when a connect operation is finished.
        /// </summary>
        /// <param name="Result">The result object of the asynchronous operation.</param>
        protected void ConnectCallback(IAsyncResult Result)
        {
            try
            {
                Sock.EndConnect(Result);
                this._Connected = true;
                this.Receive();
                this.Connected(this, new EventArgs());
            }
            catch (Exception e) { this.Abort(e); }
        }

        #endregion

        #region Send

        /// <summary>
        /// Begins sending text to the remote host.
        /// </summary>
        /// <param name="Text">The text to send encoded in UTF-8.</param>
        public void Send(string Text) { Send(GetBytes(Text + "\n")); }

        /// <summary>
        /// Begins sending data the the remote host.
        /// </summary>
        /// <param name="Data">The data to send.</param>
        public void Send(byte[] Data)
        {
            this.RequireConnection();
            try { Sock.BeginSend(Data, 0, Data.Length, SocketFlags.None, new AsyncCallback(SendCallback), Data); }
            catch (Exception e) { this.Abort(e); }
        }

        /// <summary>
        /// Called when a send operation is finished.
        /// </summary>
        /// <param name="Result">The result object of the asynchronous operation.</param>
        protected void SendCallback(IAsyncResult Result)
        {
            try
            {
                if (Sock.EndSend(Result) == 0) { throw new SocketException(); }
                this.Sent(this, new TransmitEventArgs((byte[])Result.AsyncState));
            }
            catch (Exception e) { this.Abort(e); }
        }

        #endregion

        #region Receive

        /// <summary>
        /// Starts listening for data to receive from the remote host. Calling this method is not neccessary unless you manually set the Client.Socket attribute.
        /// </summary>
        public void Receive()
        {
            try { this.Sock.BeginReceive(ReceiveBuffer, 0, ReceiveBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), new object()); }
            catch (Exception e) { this.Abort(e); }
        }

        /// <summary>
        /// Called when data is received from the remote host.
        /// </summary>
        /// <param name="Result">The result object of the asynchronous operation.</param>
        protected void ReceiveCallback(IAsyncResult Result)
        {
            int Received = 0;
            try
            {
                Received = Sock.EndReceive(Result);
                if (Received == 0) { throw new SocketException(); }
            }
            catch (Exception e)
            {
                this.Abort(e);
                return;
            }
            this.AddReceivedData(this.ReceiveBuffer, Received);
            this.Receive();
        }

        /// <summary>
        /// Parses the received data and raises Client.Received if a complete message has been received.
        /// </summary>
        /// <param name="Bytes">Buffer of received bytes to parse.</param>
        /// <param name="Count">Number of bytes to parse from the beginning of the buffer.</param>
        protected void AddReceivedData(byte[] Bytes, int Count)
        {
            byte[] NewData = new byte[this.ReceivedData.Length + Count];
            this.ReceivedData.CopyTo(NewData, 0);
            Buffer.BlockCopy(Bytes, 0, NewData, this.ReceivedData.Length, Count);
            this.ReceivedData = NewData;
            bool MessageFound = false;
            do
            {
                MessageFound = false;
                for (int i = 0; i < this.ReceivedData.Length; i++)
                {
                    if ((char)this.ReceivedData[i] == '\n')
                    {
                        byte[] Message = new byte[i];
                        Buffer.BlockCopy(this.ReceivedData, 0, Message, 0, Message.Length);

                        byte[] Remainder = new byte[this.ReceivedData.Length - i - 1];
                        Buffer.BlockCopy(this.ReceivedData, i + 1, Remainder, 0, this.ReceivedData.Length - i - 1);
                        this.ReceivedData = Remainder;

                        MessageFound = true;
                        this.Received(this, new TransmitEventArgs(Message));
                        break;
                    }
                }
            } while (MessageFound);
        }

        #endregion

        #region Disconnect

        /// <summary>
        /// Begins to gracefully disconnect from the remote host.
        /// </summary>
        public void Disconnect()
        {
            this.RequireConnection();
            try { Sock.BeginDisconnect(false, new AsyncCallback(DisconnectCallback), new object()); }
            catch (Exception e) { this.Abort(e); }
        }

        /// <summary>
        /// Called when a disconnect operation is finished.
        /// </summary>
        /// <param name="Result">The result object of the asynchronous operation.</param>
        protected void DisconnectCallback(IAsyncResult Result)
        {
            try
            {
                Sock.EndDisconnect(Result);
                Sock.Close();
                this.Closed(this, new CloseEventArgs(CloseType.Graceful));
            }
            catch (Exception e) { this.Abort(e); }
        }

        #endregion

        #region Close

        /// <summary>
        /// Terminates the connection with the remote host. Use only in emergency situations when a graceful disconnect is not possible.
        /// </summary>
        public void Abort() { this.Abort(null); }

        /// <summary>
        /// Terminates the connection with the remote host.
        /// </summary>
        /// <param name="Exception">The exception that requires the connection to be terminated.</param>
        protected void Abort(Exception Exception)
        {
            Sock.Close();
            this.Closed(this, new CloseEventArgs(CloseType.Dropped, Exception));
        }

        /// <summary>
        /// Called when the connection is closed or dropped. Updates the connection status.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A Client.CloseEventArgs object that contains the reason the connection was closed.</param>
        protected void ControlSocket_Closed(object sender, Client.CloseEventArgs e)
        {
            this._Connected = false;
        }

        #endregion

        #region Static methods

        /// <summary>
        /// Converts a byte array to a string encoded in UTF-8.
        /// </summary>
        /// <param name="Bytes">The byte array to convert.</param>
        /// <returns>The converted data as a string encoded in UTF-8.</returns>
        public static string GetText(byte[] Bytes)
        {
            return Encoding.UTF8.GetString(Bytes).Replace("\r", "");
        }

        /// <summary>
        /// Converts a string encoded in UTF-8 to a byte array.
        /// </summary>
        /// <param name="Text">The string to convert.</param>
        /// <returns>The converted data as a byte array.</returns>
        public static byte[] GetBytes(string Text)
        {
            return Encoding.UTF8.GetBytes(Text);
        }

        #endregion

        /// <summary>
        /// Contains data for transmission-related events such as Send or Receive.
        /// </summary>
        public class TransmitEventArgs : EventArgs
        {
            #region Properties

            /// <summary>
            /// The transmitted data.
            /// </summary>
            public byte[] Data;

            /// <summary>
            /// The transmitted data converted to a string encoded in UTF-8.
            /// </summary>
            public string Text { get { return Client.GetText(Data); } }

            #endregion

            #region Constructor

            /// <summary>
            /// Initializes this instance of Client.TransmitEventArgs.
            /// </summary>
            /// <param name="Bytes">The transmitted data.</param>
            public TransmitEventArgs(byte[] Bytes) { this.Data = Bytes; }

            #endregion
        }

        /// <summary>
        /// Contains data for the Client.Close event.
        /// </summary>
        public class CloseEventArgs : EventArgs
        {
            #region Properties

            /// <summary>
            /// The reason the connection was closed.
            /// </summary>
            public CloseType Type;

            /// <summary>
            /// The exception that required the connection to be closed.
            /// </summary>
            public Exception Exception;

            #endregion

            #region Constructor

            /// <summary>
            /// Initializes this instance of Client.CloseEventArgs.
            /// </summary>
            /// <param name="Type">The reason the connection was closed.</param>
            public CloseEventArgs(CloseType Type) { this.Type = Type; }

            /// <summary>
            /// Initializes this instance of Client.CloseEventArgs.
            /// </summary>
            /// <param name="Type">The reason the connection was closed.</param>
            /// <param name="Exception">The exception that required the connection to be closed.</param>
            public CloseEventArgs(CloseType Type, Exception Exception) : this(Type) { this.Exception = Exception; }

            #endregion
        }

        /// <summary>
        /// Contains reasons why the connection of a Client instance might be closed.
        /// </summary>
        public enum CloseType
        {
            /// <summary>
            /// The connection was closed in an ordinary way.
            /// </summary>
            Graceful,
            /// <summary>
            /// The connection was forcefully terminated.
            /// </summary>
            Dropped
        }
    }
}
