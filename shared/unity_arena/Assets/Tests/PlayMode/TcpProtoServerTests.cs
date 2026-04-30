using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.PlayMode
{
    public class TcpProtoServerTests
    {
        private GameObject _hostObject;
        private TcpProtoServer _server;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _hostObject = new GameObject("TcpProtoServerHost");
            _server = _hostObject.AddComponent<TcpProtoServer>();
            _server.Port = 17654;  // unique test port
            // Stub dispatch: echo back the request with method= label.
            _server.SetDispatcher((method, request) => new Dictionary<string, object>
            {
                { "echoed_method", method },
                { "echoed_request", request },
            });
            yield return null;  // wait one frame for Awake
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            UnityEngine.Object.Destroy(_hostObject);
            yield return null;
        }

        [Test]
        public void RoundTrip_LengthPrefixedJson()
        {
            using var client = new TcpClient("127.0.0.1", _server.Port);
            using var stream = client.GetStream();

            // Send: { "method": "env_reset", "request": { "seed": 42 } }
            string requestJson = "{\"method\":\"env_reset\",\"request\":{\"seed\":42}}";
            byte[] body = Encoding.UTF8.GetBytes(requestJson);
            byte[] header = new byte[]
            {
                (byte)((body.Length >> 24) & 0xFF),
                (byte)((body.Length >> 16) & 0xFF),
                (byte)((body.Length >> 8) & 0xFF),
                (byte)(body.Length & 0xFF),
            };
            stream.Write(header, 0, 4);
            stream.Write(body, 0, body.Length);

            // Read 4-byte BE length
            byte[] respHeader = ReadExact(stream, 4);
            int respLen = (respHeader[0] << 24) | (respHeader[1] << 16) | (respHeader[2] << 8) | respHeader[3];
            byte[] respBody = ReadExact(stream, respLen);
            string respJson = Encoding.UTF8.GetString(respBody);

            StringAssert.Contains("\"ok\":true", respJson);
            StringAssert.Contains("\"echoed_method\":\"env_reset\"", respJson);
        }

        private byte[] ReadExact(NetworkStream stream, int n)
        {
            byte[] buf = new byte[n];
            int read = 0;
            while (read < n)
            {
                int got = stream.Read(buf, read, n - read);
                if (got <= 0) throw new Exception("connection closed");
                read += got;
            }
            return buf;
        }
    }
}
