using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace HealSphere.Tripo
{
    public sealed partial class TripoModelLoader
    {
        public Task<byte[]> SendReflectionRequestAsync(string route, string json, CancellationToken token)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(route, @"^/api/(reflection/(plans(?:/[a-f0-9-]{36}(?:/cancel)?)?|demo/(fox|cat|rabbit|dog|elephant|gorilla)/(reflection|support|hope)/model)|speech/(narration|audio/[a-f0-9]{64})|library/[a-f0-9-]{36}/model)$"))
                throw new InvalidOperationException("Invalid reflection route.");
            return Request(route, json, token);
        }
        public Task<byte[]> TranscribeSpeechAsync(byte[] wav, CancellationToken token)
        {
            if (wav == null || wav.Length < 44 || wav.Length > 2 * 1024 * 1024) throw new InvalidOperationException("Recording exceeds its audio budget.");
            return RequestBytes("/api/speech/transcribe", wav, "audio/wav", token);
        }
        public async Task<byte[]> ReadReflectionDemoAsync(string animal, string role, CancellationToken token)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(animal ?? "", "^(fox|cat|rabbit|dog|elephant|gorilla|deer|owl)$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(role ?? "", "^(reflection|support|hope)$")) throw new InvalidOperationException("Offline animal illustration unavailable.");
            var path = System.IO.Path.Combine(Application.streamingAssetsPath, "ReflectionDemo", animal + "-" + role + ".glb");
            using var request = UnityWebRequest.Get(path.Contains("://") ? path : new Uri(path).AbsoluteUri); request.timeout = 30;
            var pending = request.SendWebRequest();
            try {
                while (!pending.isDone) { token.ThrowIfCancellationRequested(); if (request.downloadedBytes > MaxModelBytes) throw new InvalidOperationException("Model too large."); await Task.Yield(); }
                token.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success) throw new InvalidOperationException("Offline animal illustration could not load.");
                return request.downloadHandler.data;
            } finally { if (!pending.isDone) request.Abort(); }
        }
    }
}
