using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsTest
{
    class ChatServiceStub : IChatService
    {
        private readonly ChatServiceImpl _svc;

        ChatSession _session;

        public ChatServiceStub(ChatServiceImpl svc)
        {
            _svc = svc;
        }

        IChatSession IChatService.Login(string username, IMessageHandler handler)
        {
            _session = _svc.Login(username, handler);
            return _session;
        }

        public void CleanupSession()
        {
            _svc.Unregister(_session);
        }

        string IChatService.Ping()
        {
            Console.WriteLine("pongpong");
            return "pong";
        }
    }

    class ChatSession : IChatSession
    {
        public ChatServiceImpl Service { get; private set; }
        public string Username { get; private set; }
        public IMessageHandler Handler { get; private set; }

        public ChatSession(ChatServiceImpl service, string username, IMessageHandler handler)
        {
            this.Service = service;
            this.Username = username;
            this.Handler = handler;
        }

        void IChatSession.SendMessage(string text)
        {
            this.Service.PostMessage(this.Username, text);
        }
    }

    class ChatServiceImpl
    {
        readonly LinkedList<ChatSession> _sessions = new LinkedList<ChatSession>();

        public ChatServiceImpl()
        {
        }

        public ChatSession Login(string username, IMessageHandler handler)
        {
            var session = new ChatSession(this, username, handler);

            lock (session)
            {
                _sessions.AddLast(session);
            }

            this.PostMessage("SERVER", $"{username} entered the chat");

            return session;
        }

        public void PostMessage(string authorName, string text)
        {
            List<ChatSession> targets;

            lock (_sessions)
                targets = _sessions.ToList();

            targets.ForEach(s => s.Handler.OnMessage(authorName, text));
        }

        public void Unregister(ChatSession session)
        {
            lock (_sessions)
                _sessions.Remove(session);
        }
    }
}
