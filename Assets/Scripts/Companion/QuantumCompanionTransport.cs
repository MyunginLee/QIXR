using System;
using System.Collections;
using System.Security.Cryptography;
using UnityEngine.Networking;

namespace Qixr.Companion
{
    public sealed class CompanionHttpResult
    {
        public long Status;
        public byte[] Data;
        public bool Success => Status >= 200 && Status < 300;
    }

    public interface IQuantumCompanionTransport
    {
        IEnumerator Send(string url, string method, byte[] body, string contentType, string token,
            string certificatePin, Action<CompanionHttpResult> completed);
        void AbortAll();
    }

    public sealed class QuantumCompanionTransport : IQuantumCompanionTransport
    {
        private readonly System.Collections.Generic.HashSet<UnityWebRequest> active = new System.Collections.Generic.HashSet<UnityWebRequest>();
        public IEnumerator Send(string url, string method, byte[] body, string contentType, string token,
            string certificatePin, Action<CompanionHttpResult> completed)
        {
            using (var request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 20;
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.SetRequestHeader("Content-Type", contentType);
                }
                if(!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
                request.certificateHandler = new PinnedCertificate(certificatePin);
                active.Add(request);
                try
                {
                    yield return request.SendWebRequest();
                    completed(new CompanionHttpResult { Status = request.responseCode, Data = request.downloadHandler.data });
                }
                finally { active.Remove(request); }
            }
        }
        public void AbortAll()
        {
            foreach (var request in active) request.Abort();
        }
        private sealed class PinnedCertificate : CertificateHandler
        {
            private readonly string pin;
            public PinnedCertificate(string pin) { this.pin = pin; }
            protected override bool ValidateCertificate(byte[] data)
            {
                if (data == null || string.IsNullOrEmpty(pin)) return false;
                using (var sha = SHA256.Create())
                    return string.Equals(BitConverter.ToString(sha.ComputeHash(data)).Replace("-", ""), pin, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // Immutable ownership travels with every caption and audio download.
    public sealed class CompanionTurn
    {
        public string Conversation, Request, Selection, Snapshot;
        public int LocalGeneration, ServerGeneration, Epoch;
        public bool Matches(CompanionJob job, string conversation, string selection, int generation, int epoch)
        {
            return job != null && job.requestId == Request && job.generation == ServerGeneration &&
                conversation == Conversation && selection == Selection && generation == LocalGeneration && epoch == Epoch &&
                (job.result == null || (job.result.authorityEpoch == Epoch && job.result.selectionId == Selection &&
                !string.IsNullOrEmpty(job.result.referenceSnapshotId)));
        }
    }
}
