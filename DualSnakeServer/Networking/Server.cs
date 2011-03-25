using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Net;

namespace Solymosi.Networking.Sockets
{
    /// <summary>
    /// Implements a simplified wrapper for System.Net.Sockets.TcpListener with client list
    /// </summary>
    /// <typeparam name="T">The client class (must inherit from Solymosi.Network.Sockets.Client)</typeparam>
    public class Server<T> where T : Client, new()
    {

        #region Delegates
        /// <summary>
        /// References a method to be called when a new client has connected.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A Server.ConnectionEventArgs object that contains the new Client instance.</param>
        public delegate void ConnectedDelegate(object sender, ConnectionEventArgs e);

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a new client has connected.
        /// </summary>
        public event ConnectedDelegate Connected = delegate { };

        #endregion

        #region Protected properties

        /// <summary>
        /// The underlying System.Net.Sockets.TcpListener instance.
        /// </summary>
        protected TcpListener ListenSocket;

        /// <summary>
        /// Notifies the listener that it should continue listening for incoming connections.
        /// </summary>
        protected ManualResetEvent ListenEvent = new ManualResetEvent(false);

        /// <summary>
        /// Protected variable to store the IP address this instance is listening on.
        /// </summary>
        protected IPAddress _IP;

        /// <summary>
        /// Protected variable to store port this instance is listening on.
        /// </summary>
        protected int _Port;

        #endregion

        #region Public properties

        /// <summary>
        /// The list of clients currently connected to this Server instance.
        /// </summary>
        public List<T> ClientList;

        /// <summary>
        /// Gets the port this Server instance is listening on.
        /// </summary>
        public int Port { get { return this._Port; } }

        /// <summary>
        /// Gets the IP address this Server instance is bound to.
        /// </summary>
        public IPAddress IP { get { return this._IP; } }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new Solymosi.Networking.Sockets.Server instance to listen on the specified port of all adapters.
        /// </summary>
        /// <param name="Port">The port to listen on.</param>
        public Server(int Port) : this(new IPAddress(new byte[] { 0, 0, 0, 0 }), Port) { }

        /// <summary>
        /// Initializes a new Solymosi.Networking.Sockets.Server instance to listen on the specified port of the specified IP address.
        /// </summary>
        /// <param name="IP">The IP address of the adapter to listen on.</param>
        /// <param name="Port">The port to listen on.</param>
        public Server(IPAddress IP, int Port)
        {
            this._Port = Port;
            this.ClientList = new List<T>();
            this.ListenSocket = new TcpListener(new IPEndPoint(IP, this.Port));
        }

        #endregion

        #region Listen

        /// <summary>
        /// Starts listening for incoming connection requests.
        /// </summary>
        virtual public void Listen()
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object State)
            {
                ListenSocket.Start(100);
                while (true)
                {
                    ListenEvent.Reset();
                    ListenSocket.BeginAcceptSocket(new AsyncCallback(EndAccept), new object());
                    ListenEvent.WaitOne();
                }
            }));
        }

        /// <summary>
        /// Called when an incoming connection request is made. Accepts the connection, creates an instance of Solymosi.Network.Sockets.Client and adds it to the client list.
        /// </summary>
        /// <param name="Result">The result object of the asynchronous operation.</param>
        virtual protected void EndAccept(IAsyncResult Result)
        {
            T Client = new T();
            Client.Socket = ListenSocket.EndAcceptSocket(Result);
            Client.Initialize();
            ListenEvent.Set();
            ClientList.Add(Client);
            Client.Closed += new Client.CloseDelegate(Client_Closed);
            Client.Receive();
            Connected(null, new ConnectionEventArgs(Client));
        }

        #endregion

        #region Close

        /// <summary>
        /// Called when the connection to a Client instance in Server.ClientList is closed or dropped. Removes the Client instance from the client list. 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        virtual protected void Client_Closed(object sender, Client.CloseEventArgs e)
        {
            T Client = (T)sender;
            ClientList.Remove(Client);
        }

        #endregion

        /// <summary>
        /// Contains data for the Server.Connected event.
        /// </summary>
        public class ConnectionEventArgs : EventArgs
        {
            #region Properties

            /// <summary>
            /// The client that has connected.
            /// </summary>
            public T Client;

            #endregion

            #region Constructor

            /// <summary>
            /// Initializes this instance of Server.ConnectionEventArgs.
            /// </summary>
            /// <param name="Client">The client that has connected.</param>
            public ConnectionEventArgs(T Client) { this.Client = Client; }

            #endregion
        }
    }
}
