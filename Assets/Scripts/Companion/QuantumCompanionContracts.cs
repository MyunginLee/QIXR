using System;

namespace Qixr.Companion
{
    [Serializable] public class CompanionConfig
    {
        public string serverUrl = "https://localhost:8786";
        public string certificateSha256;
        public string roomId;
        public string authorityToken;
        public string joinToken;
        public string participantId;
        public string language = "en";
    }
    [Serializable] public class PresentationDto
    {
        public string scene;
        public double ribbonOn = 0.0001, ribbonOff = 0.00005, arpeggioOn = 0.01;
        public string mappingVersion = "qixr-2026-09";
        public bool[] visiblePairs = new bool[0];
    }
    [Serializable] public class CouplingDto { public int first, second; public double strength; }
    [Serializable] public class QuantumSnapshotDto
    {
        public int schemaVersion = 1;
        public string roomId, snapshotId, capturedAtUtc;
        public int authorityEpoch, eventSequence, qubitCount;
        public long stateVersion;
        public double simulationTime;
        public string basisOrder = "q0-msb", densityEncoding = "complex-row-major-f64";
        public double[] density;
        public CouplingDto[] couplings;
        public PresentationDto presentation;
    }
    [Serializable] public class QuantumEventDto
    {
        public string eventId, actor, operation, beforeSnapshotId, afterSnapshotId;
        public int sequence, authorityEpoch, target, outcome;
        public double probability;
    }
    [Serializable] public class CompanionQuestion
    {
        public string requestId, selectionId, referenceSnapshotId, referenceEventId;
        public string referenceMode = "captured", language = "en", text, visibility = "private";
        public int[] targetQubitIds;
        public bool speak = true;
    }
    [Serializable] public class CompanionNode
    {
        public int qubitId;
        public string label;
        public double blochRadius, purity, renyi2Nats, entropyBits, p0, p1;
        public double[] bloch;
    }
    [Serializable] public class CompanionFacts { public CompanionNode[] nodes; public double globalPurity; }
    [Serializable] public class CompanionNextAction { public string label, question, kind; }
    [Serializable] public class CompanionReference
    {
        public string label, snapshotId;
        public long stateVersion;
        public int eventSequence;
        public int[] targetQubitIds;
        public bool showState;
    }
    [Serializable] public class CompanionComparison { public string snapshotId; public CompanionNode[] nodes; }
    [Serializable] public class PreviewBranch { public int outcome; public double probability; public double[] density; }
    [Serializable] public class PreviewDto
    {
        public string kind, sourceSnapshotId, operation;
        public int target;
        public double[] density;
        public CompanionFacts facts;
        public PreviewBranch[] branches;
    }
    [Serializable] public class CompanionAnswer
    {
        public string text, status, referenceSnapshotId, selectionId, planner, intent, voiceStatus, referenceMode;
        public int authorityEpoch;
        public int[] targetQubitIds;
        public PreviewDto preview;
        public string detailText, agentPolicy, responseLanguage;
        public CompanionFacts facts;
        public CompanionReference referenceCue;
        public CompanionComparison comparison;
        public CompanionNextAction nextAction;
        public string[] factIds, sources;
    }
    [Serializable] public class CompanionJob
    {
        public string requestId, state;
        public int generation, audioCount;
        public CompanionAnswer result;
    }
    [Serializable] public class RoomStatusDto { public int authorityEpoch, eventSequence; public string latestSnapshotId; }
    [Serializable] public class JoinDto { public string participantId; }
    [Serializable] public class JoinResultDto { public string conversationId, conversationToken; public int authorityEpoch; }
    [Serializable] public class EpochDto { public int expectedEpoch; }
    [Serializable] public class EpochResultDto { public int authorityEpoch; }
    [Serializable] public class ContextDto { public QuantumSnapshotDto snapshot; public string lastEventId; }
    [Serializable] public class TranscriptDto { public string text, language; }
    [Serializable] public class SharedMessage
    {
        public string type, speakerConversationId, requestId;
        public int messageSequence, audioIndex;
        public CompanionAnswer result;
    }
    [Serializable] public class SharedMessages { public SharedMessage[] messages; public int cursor; }

    /// <summary>Created only after a real, authoritative operation. Matrices are copies.</summary>
    public sealed class QuantumMutation
    {
        public string Actor, Operation;
        public int Target, Outcome;
        public double Probability;
        public long BeforeVersion, AfterVersion;
        public ComplexMatrix Before, After;
    }
}
