using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace TsingYun.UnityArena
{
    // Frame publisher. Captures the gimbal camera's RenderTexture into an
    // RGB888 byte stream and publishes it over plain TCP at TargetFps. Wire
    // layout: 16-byte LE header (frame_id u64, stamp_ns u64) + width*height*3
    // raw bytes. Mirrors tcp_frame_pub.gd. AsyncGPUReadback is used to avoid
    // stalling the main thread on the GPU.
    public class TcpFramePub : MonoBehaviour
    {
        public int Port = 7655;
        public int TargetFps = 60;
        public int FrameWidth = 1280;
        public int FrameHeight = 720;
        public Camera SourceCamera;

        private TcpListener _listener;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _clientsLock = new object();
        private Thread _acceptThread;
        private bool _running;
        private float _accumulator;
        private ulong _frameId;
        private RenderTexture _captureRt;
        private bool _readbackInFlight;

        private void Awake()
        {
            _captureRt = new RenderTexture(FrameWidth, FrameHeight, 0, RenderTextureFormat.ARGB32);
            _captureRt.Create();

            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "TcpFramePub-accept" };
            _acceptThread.Start();
            Debug.Log($"[TcpFramePub] listening on tcp://0.0.0.0:{Port}");
        }

        private void OnDestroy()
        {
            _running = false;
            _listener?.Stop();
            lock (_clientsLock)
            {
                foreach (var c in _clients) c.Close();
                _clients.Clear();
            }
            if (_captureRt != null) _captureRt.Release();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    client.NoDelay = true;
                    lock (_clientsLock) _clients.Add(client);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }
        }

        private void Update()
        {
            _accumulator += Time.unscaledDeltaTime;
            float period = 1f / TargetFps;
            if (_accumulator < period) return;
            _accumulator = 0f;

            int clientCount;
            lock (_clientsLock) clientCount = _clients.Count;
            if (clientCount == 0) return;
            if (_readbackInFlight) return;
            if (SourceCamera == null) return;

            // Render camera to capture RT, then async readback.
            SourceCamera.targetTexture = _captureRt;
            SourceCamera.Render();

            _readbackInFlight = true;
            AsyncGPUReadback.Request(_captureRt, 0, TextureFormat.RGB24, OnReadback);
        }

        private void OnReadback(AsyncGPUReadbackRequest req)
        {
            _readbackInFlight = false;
            if (req.hasError) return;

            byte[] rgb = req.GetData<byte>().ToArray();
            _frameId++;
            ulong stampNs = (ulong)(System.Diagnostics.Stopwatch.GetTimestamp() * 1_000_000_000L /
                                    System.Diagnostics.Stopwatch.Frequency);

            byte[] header = new byte[16];
            BitConverter.GetBytes(_frameId).CopyTo(header, 0);
            BitConverter.GetBytes(stampNs).CopyTo(header, 8);

            BroadcastFrame(header, rgb);
        }

        private void BroadcastFrame(byte[] header, byte[] body)
        {
            lock (_clientsLock)
            {
                for (int i = _clients.Count - 1; i >= 0; i--)
                {
                    var client = _clients[i];
                    try
                    {
                        if (!client.Connected) { _clients.RemoveAt(i); continue; }
                        var stream = client.GetStream();
                        stream.Write(header, 0, header.Length);
                        stream.Write(body, 0, body.Length);
                    }
                    catch
                    {
                        client.Close();
                        _clients.RemoveAt(i);
                    }
                }
            }
        }
    }
}
