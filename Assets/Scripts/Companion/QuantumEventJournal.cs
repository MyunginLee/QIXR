using System;
using System.Collections.Generic;
using UnityEngine;

namespace Qixr.Companion
{
    [RequireComponent(typeof(QuantumContextExporter))]
    public sealed class QuantumEventJournal : MonoBehaviour
    {
        public sealed class Entry { public QuantumSnapshotDto Before, After; public QuantumEventDto Event; }
        private readonly Queue<Entry> pending = new Queue<Entry>();
        private QuantumContextExporter exporter;
        public bool Overflowed { get; private set; }
        public int Count => pending.Count;
        private void Awake() { exporter = GetComponent<QuantumContextExporter>(); }
        private void OnEnable() { QubitManager.MutationCompleted += Record; }
        private void OnDisable() { QubitManager.MutationCompleted -= Record; }
        public Entry Peek() => pending.Count > 0 ? pending.Peek() : null;
        public void Acknowledge() { if (pending.Count > 0) pending.Dequeue(); }
        public void ClearJournal() { pending.Clear(); Overflowed = false; exporter.EventSequence = 0; }
        private void Record(QuantumMutation mutation)
        {
            if (!QuantumContextExporter.IsAuthority || exporter.AuthorityEpoch < 1) return;
            if (pending.Count >= 128) { Overflowed = true; return; }
            QuantumSnapshotDto before = exporter.Capture(mutation.Before, mutation.BeforeVersion, exporter.EventSequence);
            exporter.EventSequence++;
            QuantumSnapshotDto after = exporter.Capture(mutation.After, mutation.AfterVersion, exporter.EventSequence);
            pending.Enqueue(new Entry { Before = before, After = after, Event = new QuantumEventDto {
                eventId = Guid.NewGuid().ToString("N"), sequence = exporter.EventSequence,
                authorityEpoch = exporter.AuthorityEpoch, actor = mutation.Actor, operation = mutation.Operation,
                target = mutation.Target, outcome = mutation.Outcome, probability = mutation.Probability,
                beforeSnapshotId = before.snapshotId, afterSnapshotId = after.snapshotId
            }});
        }
    }
}
