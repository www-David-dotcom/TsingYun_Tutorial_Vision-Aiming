using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.PlayMode
{
    public class TcpFramePubTests
    {
        private GameObject _hostObject;
        private GameObject _camObject;
        private TcpFramePub _pub;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _camObject = new GameObject("TestCamera");
            var cam = _camObject.AddComponent<Camera>();
            cam.targetTexture = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);

            _hostObject = new GameObject("TcpFramePubHost");
            _pub = _hostObject.AddComponent<TcpFramePub>();
            _pub.Port = 17655;
            _pub.SourceCamera = cam;
            _pub.FrameWidth = 64;
            _pub.FrameHeight = 36;
            yield return null;  // wait for Awake
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            UnityEngine.Object.Destroy(_hostObject);
            UnityEngine.Object.Destroy(_camObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstFrame_Has16ByteHeader_PlusExpectedPayloadSize()
        {
            using var client = new TcpClient("127.0.0.1", _pub.Port);
            using var stream = client.GetStream();

            // Wait for at least one frame to be published.
            yield return new WaitForSeconds(0.2f);

            byte[] header = new byte[16];
            int read = 0;
            while (read < 16)
            {
                int got = stream.Read(header, read, 16 - read);
                if (got <= 0) Assert.Fail("connection closed before header");
                read += got;
            }

            ulong frameId = BitConverter.ToUInt64(header, 0);
            ulong stampNs = BitConverter.ToUInt64(header, 8);
            Assert.GreaterOrEqual(frameId, 1UL);
            Assert.Greater(stampNs, 0UL);

            // Payload size = width * height * 3 (RGB888)
            int expected = 64 * 36 * 3;
            byte[] body = new byte[expected];
            read = 0;
            while (read < expected)
            {
                int got = stream.Read(body, read, expected - read);
                if (got <= 0) Assert.Fail("connection closed before body");
                read += got;
            }
        }
    }
}
