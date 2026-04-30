using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace TsingYun.UnityArena
{
    // Length-prefixed JSON over TCP control server. Wire layout in each
    // direction: [u32 BE length][JSON UTF-8 bytes]. Mirrors tcp_proto_server.gd
    // byte-for-byte. Listens on port 7654 by default.
    //
    // Dispatcher signature: (method, request) -> response. Set via
    // SetDispatcher. ArenaMain wires this up at Awake.
    public class TcpProtoServer : MonoBehaviour
    {
        public int Port = 7654;

        private TcpListener _listener;
        private Thread _acceptThread;
        private bool _running;
        private Func<string, Dictionary<string, object>, object> _dispatch;

        public void SetDispatcher(Func<string, Dictionary<string, object>, object> dispatcher)
        {
            _dispatch = dispatcher;
        }

        private void Awake()
        {
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "TcpProtoServer-accept" };
            _acceptThread.Start();
            Debug.Log($"[TcpProtoServer] listening on tcp://0.0.0.0:{Port}");
        }

        private void OnDestroy()
        {
            _running = false;
            _listener?.Stop();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    client.NoDelay = true;
                    var clientThread = new Thread(() => ClientLoop(client))
                    {
                        IsBackground = true,
                        Name = "TcpProtoServer-client",
                    };
                    clientThread.Start();
                }
                catch (SocketException) { /* listener closed */ }
                catch (ObjectDisposedException) { /* listener closed */ }
            }
        }

        private void ClientLoop(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                while (_running && client.Connected)
                {
                    byte[] header = ReadExact(stream, 4);
                    if (header == null) return;
                    int len = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
                    byte[] body = ReadExact(stream, len);
                    if (body == null) return;
                    string requestJson = Encoding.UTF8.GetString(body);
                    object response = Dispatch(requestJson);
                    SendResponse(stream, response);
                }
            }
        }

        private object Dispatch(string requestJson)
        {
            // Minimal JSON parser: only need top-level object with "method" and "request".
            // Use Unity's JsonUtility-incompatible path: hand-parse to a Dictionary.
            var parsed = JsonMiniParser.ParseDict(requestJson);
            if (parsed == null)
                return new Dictionary<string, object> { { "ok", false }, { "error", "request was not a JSON object" } };

            string method = parsed.TryGetValue("method", out var m) ? m as string ?? "" : "";
            var request = parsed.TryGetValue("request", out var r) ? r as Dictionary<string, object> ?? new Dictionary<string, object>() : new Dictionary<string, object>();

            if (_dispatch == null)
                return new Dictionary<string, object> { { "ok", false }, { "error", "no dispatcher set" } };

            object response;
            try
            {
                response = _dispatch(method, request);
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "ok", false }, { "error", ex.Message } };
            }

            // Mirror arena_main.gd's _error sentinel: response dict with "_error" key indicates failure.
            if (response is Dictionary<string, object> respDict && respDict.ContainsKey("_error"))
                return new Dictionary<string, object> { { "ok", false }, { "error", respDict["_error"] } };

            return new Dictionary<string, object> { { "ok", true }, { "response", response } };
        }

        private void SendResponse(NetworkStream stream, object response)
        {
            string json = response is Dictionary<string, object> d
                ? JsonHelper.SerializeDict(d)
                : "{}";
            byte[] body = Encoding.UTF8.GetBytes(json);
            int n = body.Length;
            byte[] header = new byte[]
            {
                (byte)((n >> 24) & 0xFF),
                (byte)((n >> 16) & 0xFF),
                (byte)((n >> 8) & 0xFF),
                (byte)(n & 0xFF),
            };
            stream.Write(header, 0, 4);
            stream.Write(body, 0, n);
            stream.Flush();
        }

        private static byte[] ReadExact(NetworkStream stream, int n)
        {
            byte[] buf = new byte[n];
            int read = 0;
            while (read < n)
            {
                int got = stream.Read(buf, read, n - read);
                if (got <= 0) return null;
                read += got;
            }
            return buf;
        }
    }
}
