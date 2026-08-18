# Phase 2 — Quantitative Entanglement Metrics

Status: **implemented and validated** (2026-08-18)

## Shared snapshot

`QubitManager` publishes one immutable `EntanglementSnapshot` whenever the quantum state's version changes. Bloch rendering, node scale, pair arcs, trail state, floor/top waves, and procedural audio all read this same snapshot.

The snapshot contains:

- three single-qubit reduced states `rho_i`;
- three two-qubit reduced states `rho_ij`;
- Bloch vector and radius per node;
- purity, second Rényi entropy in nats, and von Neumann entropy in bits per node;
- negativity, logarithmic negativity, pair entropy, and mutual information per pair;
- global purity and density-matrix validation;
- a conservative three-party correlation flag and normalized strength.

Reduced matrices are returned only as copies, and node/pair collections are read-only.

## Metric definitions

For node `i`:

`S2(rho_i) = -ln Tr(rho_i^2)`

For pair `ij`, the partial transpose is taken on the second qubit in the ordered pair:

`N(rho_ij) = (||rho_ij^(T_j)||_1 - 1) / 2`

`E_N(rho_ij) = log2 ||rho_ij^(T_j)||_1`

The trace norm is calculated from a symmetric real embedding of the complex Hermitian partial transpose and a Jacobi eigenvalue solver. Pair arcs are shown only when `E_N > 1e-7`.

The central triad is shown when all three single-qubit Rényi entropies exceed the correlation threshold. It is labeled **three-party correlation**, not genuine tripartite entanglement.

## Required interpretation

- Node entropy describes one qubit versus the remaining system; it does not identify a specific partner.
- Pair mutual information includes classical and quantum correlation and is not used as the entanglement arc criterion.
- Pair logarithmic negativity is the arc criterion.
- GHZ has a visible triad but zero pair-negativity arcs.
- Bell-pair-plus-spectator has one pair arc and no triad.
- W has nonzero pair negativities and a visible triad.

## Consumer changes

- Bloch dots use snapshot Bloch vectors.
- Node scale and wave color use node radius/entropy.
- Pair arc width, opacity, and numeric label use `E_N`.
- Trail activation uses node correlation rather than geometric proximity.
- Per-qubit audio amplitude uses node entropy, reverb uses maximum incident pair `E_N`, and the triad shifts one chord tone.
- Controller-distance checks remain separate and are used only for XR manipulation guidance and the physical exchange coupling.

## Validation

The deterministic editor check covers:

- product `|000>`: radius one, zero pair negativity, no triad;
- Bell Q0-Q1 plus Q2 spectator: `N=0.5`, `E_N=1`, one pair only, no triad;
- GHZ: all node radii zero, all pair negativities zero, triad strength one;
- W: three nonzero pair logarithmic negativities near `0.498`, triad visible;
- GHZ Z-measurement: state version advances and all pair/triad metrics disappear after collapse.

Unity menu: `QIXR > Validation > Run Phase 2 Entanglement Metrics`.

Latest combined validation log: `Logs/phase2-final.log`.
