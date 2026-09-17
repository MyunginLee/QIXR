# Current Implementation Status

Updated: 2026-08-18

## Scope

The active build target is a three-qubit Quest/XR experience. `QubitManager` evolves one shared 8x8 density matrix; visualization, audio, and networking read the resulting `EntanglementSnapshot` instead of calculating separate local quantum states.

## Scenes

| Scene | Status | Intended use |
| --- | --- | --- |
| `Assets/Scenes/main.unity` | Preserved | Legacy gravity-trail presentation. |
| `Assets/Scenes/RibbonVisualization.unity` | Implemented | Ribbon-only visual presentation; B / Space holds pair `E_N` labels. |
| `Assets/Scenes/MusicalRibbonVisualization.unity` | Implemented, manual audio regression pending | Ribbon presentation with monophonic quantum arpeggio. Added to Build Settings. |

`MusicalRibbonSceneBuilder` can recreate or refresh the musical scene from the ribbon scene through `QIXR/Visualization/Create or Refresh Musical Ribbon Scene`.

## Quantum state and entanglement

- Three qubits use a global density matrix, pairwise logarithmic negativity (`E_N`), mutual information, local Renyi-2 entropy, and a three-party correlation estimate.
- Heisenberg evolution orthonormalizes the approximate unitary columns before applying it. PSD validation retains small positive pivots instead of converting them to zero.
- `QubitManager` stops simulation if state validation fails; this is intentional fail-safe behaviour.
- Pair visuals are driven by their own `E_N`, not a global binary entanglement flag.
- Musical ribbon visual threshold is `E_N >= 0.0001` (`0.00005` release threshold). This makes weak but nonzero Q1-Q3 correlations visible; it does not fabricate a ribbon for `E_N = 0`.

### Required manual verification

1. In `MusicalRibbonVisualization`, bring Q1 and Q3 within the coupling distance, wait for evolution, then hold B (Quest) or Space (Editor) to inspect `E_N`.
2. If Q1-Q3 still has no ribbon, capture the displayed `E_N` value. A displayed zero is a simulation/coupling problem; a positive value without a ribbon is a visualization bug.
3. Re-test repeated pair formation and measurement after any quantum-engine change.

## Ribbon and UI interaction

- The ribbon scene does not instantiate the legacy trail particle pool.
- Pair `E_N` labels are hidden by default in ribbon-only scenes and visible only while holding B / Space.
- Qubit-attached world-space UI compensates for qubit scale so it does not shrink with an entangled qubit.
- Unity editor input uses the Input System path; the previous legacy `UnityEngine.Input` mouse read was removed from shared-anchor hosting.

## Audio

### Active musical-scene path

`QuantumArpeggioComposer` is local-only and monophonic:

- strongest pairwise `E_N` selects the active pair;
- relative pair phase selects a circle-of-fifths root and arpeggio direction;
- `E_N` selects open fifth, triad, or iridescent extension;
- mutual information changes step rate;
- measurement emits a short monophonic cadence.

### Legacy FM suppression

`QubitAudio` contains the original FM callback. In `MusicalRibbonVisualization` it is disabled both in the scene and at runtime through `QubitAudio.SuppressLegacyFm`; active legacy `AudioSource` instances are stopped when the arpeggio composer starts. Main and non-musical ribbon scenes retain legacy FM by default.

### Required manual verification

Stop and restart Play Mode in `MusicalRibbonVisualization`. Only the monophonic arpeggio should remain. If any continuous/FM-like sound remains, report the hierarchy object with a playing `AudioSource`; the source is then outside `QubitAudio` and must be muted separately.

## Multiuser / colocation

- Shared-state authority is handled through `QuantumSessionState`; clients render the replicated density matrix.
- Photon/Fusion handles session transport. Spatial anchors and avatars remain device/platform concerns.
- Local visual trails and local audio are intentionally not network-synchronized.

## Build and validation

- Runtime and Editor C# assemblies compile successfully with Unity 6000.0.25f1 response files.
- Existing obsolete-API warnings remain in template/legacy scripts; no new compile errors are known.
- A complete Unity Play Mode and Quest-device regression pass has not been run after the latest FM suppression change.
