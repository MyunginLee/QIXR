# Phase 3 — Three-Qubit XR Content

Status: **implemented and editor-validated** (2026-08-18)

## Scene and prefab

- Added `Assets/Prefab/Qubit 3.prefab` with explicit `QubitId=2`.
- Added `Qubit 3` and `QubitShell 3` to `Assets/Scenes/main.unity`.
- The three starting positions form a separated triangle so no interaction begins accidentally.
- Qubit 3 retains the source prefab's collider, XR grab interactable, Bloch dot, line renderer, input actions, gate audio, and procedural node audio.
- An editor builder/validator can recreate and audit these references from `QIXR > Phase 3 > Build and Validate Three-Qubit Scene`.

## Visual semantics

- After Phase 2 integration, three labeled arcs represent pair logarithmic negativity for Q0–Q1, Q0–Q2, and Q1–Q2. Physical proximity/coupling remains a separate input to evolution and XR guidance.
- Pair identity is communicated by both text and color: cyan, amber, and magenta. Color is not the only signal.
- Arc curvature differs per pair so overlapping connections remain readable.
- The center diamond is labeled **three-party correlation**, not genuine tripartite entanglement.
- The diamond appears when all three single-qubit reduced states have nonzero Rényi-2 entropy. This is a conservative visualization condition, not a GHZ/W classifier.
- The world-space prompt explicitly tells the user to hold the XR controller Grip, grab a named qubit, move it, and release it. It never moves or joins qubits automatically.
- Pair arcs display `E_N` and appear only for positive logarithmic negativity. See `phase2_entanglement_metrics.md` for the metric definition.

## Existing visual and audio behavior

- The correlation-trail field remains, but uses a pool of 96 lightweight TrailRenderers instead of 300 cube primitives.
- Trail gradients update only when the mean node entropy changes materially rather than being allocated for every trail on every frame.
- Qubits and shells are matched by explicit Qubit IDs, not tag discovery order.
- After Phase 2 integration, each qubit's audio reads node entropy, incident pair logarithmic negativity, and triad strength from the shared snapshot.
- A minimum visual/grab scale is retained when a reduced Bloch radius reaches zero.

## Wavefunction floor/top

- `Hydrogen-Floor.shader` now has a static `MAX_QUBITS=8` capacity.
- Centers, wave parameters, and colors are all per-qubit arrays.
- `FloorElectrons` sorts by explicit ID and uploads `_NumQubits=3` for the main scene.

## Validation

The Phase 3 editor validator checks:

- exactly three active qubits with contiguous IDs 0, 1, and 2;
- Qubit tag, dot, line renderer, XR grab component, input-action references, and audio clips;
- QubitShell 1/2/3;
- central visual controller and wave controllers;
- Qubit 3 prefab ID;
- expanded shader arrays.

Latest batch log: `Logs/phase3-final.log`.

## Required headset check

Editor validation cannot verify controller feel, stereo readability, GPU fill rate, or spatial-audio balance. On Quest 3, verify grabbing all three nodes, all four local gates, each of the three pair arcs, the guided label at normal viewing distance, measurement recovery, floor/top wave rendering, and stable frame timing with two or three simultaneous interactions.
