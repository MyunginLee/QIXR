# Phase 1 — 3-Qubit Mathematical Engine

Status: **implemented and validated** (2026-08-18)

## Implemented scope

- `QuantumStateEngine` owns one `2^n x 2^n` density matrix using basis order `|q0 q1 ...>`.
- Three qubits initialize as the `8x8` state `|000><000|`.
- Arbitrary-target single-qubit gates are embedded without affecting other qubits.
- Ordered, non-adjacent two-qubit gates are supported for deterministic test-state preparation.
- Every active pair contributes to one composite Heisenberg Hamiltonian:
  `H = sum J_ij/4 (X_i X_j + Y_i Y_j + Z_i Z_j)`.
- `QubitManager.FixedUpdate` performs one evolution step with the composite Hamiltonian.
- Partial trace accepts any ordered keep set, including pair states such as `(2, 0)`.
- Z measurement collapses only the selected qubit projector while updating the conditional global state.
- Density-matrix validation checks dimensions, finite values, unit trace, Hermiticity, positive semidefiniteness, and purity range.
- Scene qubit ordering now uses explicit, contiguous IDs. Duplicate or missing IDs fail at startup.

## Runtime migration

- `Qubit` is now an XR/rendering component and no longer builds full-system gate matrices during `Awake`.
- Existing H/X/Z/S interactions call the central engine and apply only to the touched qubit.
- The old two-qubit scene remains usable: Qubit 1 has ID `0`, and the scene override assigns Qubit 2 ID `1`.
- The legacy `ApplySpinExchange(J,time)` entry point remains available only for two-qubit debug code.

## Deterministic validation

Unity menu:

`QIXR > Validation > Run Three-Qubit Engine Checks`

The checks cover:

- `8x8 |000>` initialization
- local `X(Q2)` and `H(Q0)` embedding
- non-adjacent Q0-Q2 Heisenberg coupling and simultaneous pair coupling
- arbitrary partial trace
- GHZ preparation with internal CNOT test gates
- single-qubit GHZ measurement and conditional Q1/Q2 states
- two-qubit exchange regression
- rejection of a non-positive density matrix

Batch validation passed with Unity `6000.0.25f1`; latest log: `Logs/phase1-validation-final.log`.

## Deferred to later phases

Phase 1 intentionally does not add Qubit 3 to the XR scene, a third shell/Bloch sphere, three pair arcs, triad visualization, shader capacity, or three-qubit audio mapping. Those are Phase 2 metric work and Phase 3 XR/content work.
