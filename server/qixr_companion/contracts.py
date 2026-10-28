from datetime import datetime, timezone
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, FiniteFloat, SecretStr, model_validator


class Record(BaseModel):
    model_config = ConfigDict(extra="forbid")


class Presentation(Record):
    scene: str = Field(default="unknown", max_length=128)
    ribbonOn: FiniteFloat = Field(default=0.0001, ge=0)
    ribbonOff: FiniteFloat = Field(default=0.00005, ge=0)
    arpeggioOn: FiniteFloat = Field(default=0.01, ge=0)
    mappingVersion: str = "qixr-2026-09"
    # Actual display/audio state, indexed by pairs (01,02,12). Empty means unavailable.
    visiblePairs: list[bool] = Field(default_factory=list, max_length=3)
    musicPlaying: bool | None = None


class Coupling(Record):
    first: int = Field(ge=0, le=2)
    second: int = Field(ge=0, le=2)
    strength: FiniteFloat


class Snapshot(Record):
    schemaVersion: Literal[1] = 1
    roomId: str = Field(min_length=1, max_length=64)
    authorityEpoch: int = Field(ge=1)
    snapshotId: str = Field(min_length=1, max_length=96)
    stateVersion: int = Field(ge=0)
    eventSequence: int = Field(default=0, ge=0)
    simulationTime: FiniteFloat = Field(default=0, ge=0)
    capturedAtUtc: str = Field(default_factory=lambda: datetime.now(timezone.utc).isoformat(), max_length=64)
    qubitCount: int = Field(default=3, ge=1, le=3)
    basisOrder: Literal["q0-msb"] = "q0-msb"
    densityEncoding: Literal["complex-row-major-f64", "complex-row-major-f32"] = "complex-row-major-f64"
    density: list[FiniteFloat] = Field(max_length=128)
    couplings: list[Coupling] = Field(default_factory=list, max_length=3)
    presentation: Presentation = Field(default_factory=Presentation)

    @model_validator(mode="after")
    def shape(self):
        if len(self.density) != 2 * (2**self.qubitCount)**2:
            raise ValueError("Density size does not match qubitCount")
        for c in self.couplings:
            if c.first == c.second or max(c.first, c.second) >= self.qubitCount:
                raise ValueError("Invalid coupling target")
        return self


class QuantumEvent(Record):
    eventId: str = Field(min_length=1, max_length=96)
    sequence: int = Field(ge=1)
    authorityEpoch: int = Field(ge=1)
    actor: str = Field(default="authority", max_length=96)
    operation: Literal["H", "X", "Z", "S", "measureZ"]
    target: int = Field(ge=0, le=2)
    beforeSnapshotId: str = Field(max_length=96)
    afterSnapshotId: str = Field(max_length=96)
    outcome: int | None = Field(default=None, ge=0, le=1)
    probability: FiniteFloat | None = Field(default=None, ge=0, le=1)


class Question(Record):
    requestId: str = Field(min_length=1, max_length=96, pattern=r"^[a-zA-Z0-9_-]+$")
    selectionId: str = Field(min_length=1, max_length=96)
    targetQubitIds: list[int] = Field(default_factory=list, max_length=2)
    referenceSnapshotId: str = Field(min_length=1, max_length=96)
    referenceEventId: str | None = Field(default=None, max_length=96)
    referenceMode: Literal["captured", "live"] = "captured"
    language: Literal["ko", "en"] = "en"
    text: str = Field(min_length=1, max_length=2000)
    speak: bool = False
    visibility: Literal["private", "room"] = "private"

    @model_validator(mode="after")
    def targets(self):
        if len(set(self.targetQubitIds)) != len(self.targetQubitIds) or any(q < 0 or q > 2 for q in self.targetQubitIds):
            raise ValueError("Targets must be distinct IDs in 0..2")
        return self


class Plan(Record):
    intent: Literal["qubit", "pair", "probability", "event", "presentation", "preview", "concept",
                    "identity", "help", "repair", "repeat", "detail", "compare", "acknowledge", "stop", "unsupported"]
    concept: Literal["superposition", "entanglement", "measurement", "gates", "sonification",
                     "distance", "signaling", "consciousness", "none"] = "none"
    # A vague hypothetical must never silently become an H operation.
    gate: Literal["H", "X", "Z", "S", "measureZ", "none"] = "none"


class Join(Record):
    participantId: str = Field(min_length=1, max_length=96)


class PasswordLogin(Join):
    password: SecretStr = Field(min_length=1, max_length=256)


class PasswordChange(Record):
    password: SecretStr = Field(min_length=8, max_length=256)


class RoomOptions(Record):
    name: str = Field(default="QIXR", max_length=96)
    allowShared: bool = False


class EpochChange(Record):
    expectedEpoch: int = Field(ge=0)
