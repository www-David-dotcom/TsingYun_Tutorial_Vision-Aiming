using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TsingYun.UnityArena
{
    // Per-episode replay recorder. Writes a JSON line-stream to
    // {recordingDir}/{episode_id}.json. Mirrors replay_recorder.gd. MP4 capture
    // is handled by Unity's Recorder package, not by this class.
    public class ReplayRecorder
    {
        private readonly string _dir;
        private StreamWriter _writer;
        private bool _open;
        private string _episodeId = "";
        private long _seed;

        public ReplayRecorder() : this(GetDefaultDir()) {}

        public ReplayRecorder(string recordingDir)
        {
            _dir = recordingDir;
        }

        private static string GetDefaultDir()
            => Path.Combine(Application.persistentDataPath, "replays");

        public void Start(string episodeId, long seedValue)
        {
            // Idempotent: close any prior writer so calling Start twice without
            // Finish (or hitting the same episode_id again on a reused
            // recorder) doesn't leak the previous file handle.
            Close();
            _episodeId = episodeId;
            _seed = seedValue;
            Directory.CreateDirectory(_dir);
            string path = Path.Combine(_dir, episodeId + ".json");
            _writer = new StreamWriter(path);
            _open = true;
            WriteLine(new Dictionary<string, object>
            {
                { "kind", "header" },
                { "episode_id", episodeId },
                { "seed", seedValue },
                { "version", "1.6.0" },
            });
        }

        public void Record(Dictionary<string, object> evt)
        {
            if (!_open) return;
            WriteLine(evt);
        }

        public void Finish(Dictionary<string, object> stats)
        {
            if (!_open) return;
            WriteLine(new Dictionary<string, object>
            {
                { "kind", "footer" },
                { "stats", stats },
            });
            Close();
        }

        // Releases the file handle without writing a footer. Used when the
        // owning ArenaMain is destroyed mid-episode (scene reload, app quit) —
        // the partial replay keeps its events but is missing the footer, which
        // downstream parsers can detect.
        public void Close()
        {
            if (_writer != null)
            {
                _writer.Close();
                _writer = null;
            }
            _open = false;
        }

        private void WriteLine(Dictionary<string, object> payload)
        {
            // Minimal JSON serializer: handles strings, numbers, bools, long,
            // and nested dicts. Matches the shape replay_recorder.gd emits via
            // JSON.stringify.
            _writer.WriteLine(JsonHelper.SerializeDict(payload));
        }
    }
}
