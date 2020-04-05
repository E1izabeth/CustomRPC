using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyRpc.Impl;
using MyRpc.Model;

namespace CsTest
{
    public interface IChatService : IRemoteObject
    {
        string Ping();
        IChatSession Login(string username, IMessageHandler handler);
    }

    public interface IChatSession : IRemoteObject
    {
        void SendMessage(string text);
    }

    public interface IMessageHandler : IRemoteObject
    {
        void OnMessage(string username, string text);
    }

    class ClientMessageHandler : IMessageHandler
    {
        void IMessageHandler.OnMessage(string username, string text)
        {
            Console.WriteLine($"[{username}] {text}");
        }
    }

    class Test : IDisposable
    {


        public void Dispose()
        {
            throw new ApplicationException("dispose");
        }


        static void Main()
        {
            //if (!Debugger.IsAttached)
            //    Debugger.Launch();

            //var p = Process.Start(Process.GetCurrentProcess().MainModule.FileName);


            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var thread = System.Threading.Thread.CurrentThread;
            thread.CurrentCulture = culture;
            thread.CurrentUICulture = culture;
            Program.Main2(null);
        }
    }

    class Program
    {
        //static readonly IRpcProtocol<IPEndPoint, byte[], Object> _protocol = Rpc.TcpTransport.MakeProtocol(Rpc.BinarySerializer);
        static readonly IRpcProtocol<IPEndPoint, byte[], Object> _protocol = Rpc.TcpTransport.MakeProtocol(Rpc.JsonSerializer);
        static readonly IRpcServiceHost<object, IChatService> _host = Rpc.GenericHost.Helper().ForService<IChatService>();

        static void DoServer()
        {
            var svc = new ChatServiceImpl();

            var activeSessions = new LinkedList<IRpcChannel<IChatService>>();

            using (var listener = _host.Listen(_protocol, new IPEndPoint(IPAddress.Any, 12345)))
            {
                listener.Start();

                Action<IRpcChannelAcceptContext<IPEndPoint, IChatService>> acceptHandler = null;
                acceptHandler = ctx =>
                {
                    var sessionSvcStub = new ChatServiceStub(svc);
                    var channel = ctx.Confirm(sessionSvcStub);

                    lock (activeSessions)
                    {
                        channel.OnClosed += () =>
                        {
                            lock (activeSessions)
                                activeSessions.Remove(channel);

                            sessionSvcStub.CleanupSession();
                        };

                        activeSessions.AddLast(channel);
                    }

                    channel.Start();

                    listener.AcceptChannelAsync(acceptHandler);
                };

                listener.AcceptChannelAsync(acceptHandler);

                Console.ReadLine();
            }
        }

        static void DoClient()
        {
            using (var cnn = _host.Connect(_protocol, new IPEndPoint(IPAddress.Loopback, 12345)))
            {
                cnn.Start();

                var sw = new Stopwatch();
                for (int i = 0; i < 20; i++)
                {
                    sw.Start();
                    Console.WriteLine(cnn.Service.Ping());
                    sw.Stop();
                    Console.WriteLine(i);
                }
                Console.WriteLine(new TimeSpan(sw.ElapsedTicks / 20));

                var session = cnn.Service.Login(Console.ReadLine(), new ClientMessageHandler());

                for (; ; )
                    session.SendMessage(Console.ReadLine());
            }
        }

        public static void Main2(string[] args)
        {
            Console.WriteLine("Press S for server mode or other key for client mode..");
            if (Console.ReadKey().Key == ConsoleKey.S)
            {
                DoServer();
            }
            else
            {
                DoClient();
            }
        }
    }
}

