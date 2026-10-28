# QIXR repository and source audit

Inspection date: 2026-10-27. Local checkout: `/home/myungin/Projects/27_Research/QIXR`. Scope: identify the current implementation and constraints for the [quantum companion design](QUANTUM_COMPANION_DESIGN.md).

## 1 Repository baseline

Repository: [MyunginLee/QIXR](https://github.com/MyunginLee/QIXR). A full normal clone fetched the remote branches. “Latest branch” here means the branch whose tip has the newest committer timestamp among fetched remote branches; it is not a claim about GitHub branch creation time or a release designation.

| Remote branch | Tip | Tip committer time | Subject |
| --- | --- | --- | --- |
| `test-multiuser` | `a0fba5a` | 2026-09-17 00:06:16 −04:00 | 3 qubits ribbon |
| `main` | `16f33a3` | 2025-11-22 00:28:30 −05:00 | Revert README change |
| `Quest3` | `fcd89fc` | 2025-09-29 12:10:37 −04:00 | light config fixed |
| `user-study` | `a9064ca` | 2025-06-16 15:56:52 +09:00 | Update Qubit.prefab |
| `wqd` | `8776128` | 2025-05-13 11:50:31 +09:00 | main |
| `Time-freeze` | `eb68588` | 2025-04-14 14:19:54 −04:00 | Time freeze input work |
| `devel` | `6de9617` | 2025-04-14 12:39:42 −04:00 | Movement |
| `Floor-Schrodinger` | `4188fe2` | 2024-10-13 18:59:33 −04:00 | Add qubit viz and sky |

The checkout tracks `origin/test-multiuser`. `git rev-list --left-right --count origin/main...origin/test-multiuser` returned `0 10`: main is an ancestor, with ten additional commits on the latest branch. Its recent work includes a three-qubit engine, quantitative metrics, scene guidance, ribbon visualization and sonification. No repository `AGENTS.md` was found. No Git LFS pointer files were found in tracked content.

## 2 Current architecture

| Source | Observed role and design implication |
| --- | --- |
| `ProjectSettings/ProjectVersion.txt` | Unity `6000.0.25f1`. Preserve this version for the initial bridge. |
| `Packages/manifest.json` | XR Interaction Toolkit 3.0.7, OpenXR 1.13.1, Meta packages, URP 17.0.3 and Netcode for GameObjects 1.10.0 are declared. A package declaration does not identify the active application transport. |
| `Assets/Scripts/Colocation/SessionManager.cs` | Application sessions use Photon Fusion Host/Client/Single paths; Fusion assets are checked into `Assets/Photon`. Android scripting defines enable Fusion. |
| `ProjectSettings/EditorBuildSettings.asset` | Enabled build scene is `Assets/Scenes/MusicalRibbonVisualization.unity`. README's general main-scene instructions are older than this build configuration. |
| `Assets/Scripts/QuantumStateEngine.cs` | Shared density-matrix engine, q0-most-significant basis ordering, single/two-qubit operations, Heisenberg evolution, partial trace, Z measurement, state validation and state version. Runtime configuration is three qubits, although the dense engine accepts a wider count. |
| `Assets/Scripts/Qubit/QubitManager.cs` | Owns the engine and immutable metric snapshot; evolves locally or under Fusion authority. Existing read points are `GetDensityMatrix`, `GetEntanglementSnapshot`, `GetPairCoupling`. Gate and measurement entry points already exist. |
| `Assets/Scripts/EntanglementSnapshot.cs` | Local Bloch/purity/entropy, pair negativity/logarithmic negativity/mutual information, global purity and a three-party heuristic. Units and assumptions need explicit export. |
| `Assets/Scripts/Colocation/QuantumSessionState.cs` | Authority runs evolution and publishes an 8×8 matrix as 128 float32 values, plus poses and measurement metadata. Nominal snapshot/pose publication interval is 1/20 second. AI transport is a separate addition. |
| `Assets/Scripts/Qubit/Qubit.cs` | Stable internal qubit ID, grab ownership requests, gate interaction and Bloch-vector-driven dot placement. Selection can reuse these objects. |
| `Assets/Scripts/Entanglement/Entanglement.cs` | Pair ribbon strength uses logarithmic negativity and activation/release thresholds. Presentation thresholds must accompany state questions about visible connections. |
| `Assets/Scripts/Entanglement/QuantumArpeggioComposer.cs` | Dominant pair, pair strength, local Bloch-derived phase and mutual information drive local monophonic music; default entry threshold is 0.01. Voice must coexist with this audio path. |
| `Assets/Editor/QixrThreeQubitEngineChecks.cs`, `QixrPhase2MetricChecks.cs`, `QixrTwoQubitBaselineChecks.cs` | Existing editor math/metric checks include product, Bell, GHZ, W and measurement cases. Reuse these before extending coverage. |

Search of application scripts found no existing microphone, speech recognition, LLM client or HTTP/WebSocket conversation implementation. Add a companion boundary instead of assuming a server API already exists.

The existing status file, [CURRENT_IMPLEMENTATION_STATUS.md](CURRENT_IMPLEMENTATION_STATUS.md), reports prior compilation and outstanding manual audio/Quest regressions. Those are repository-reported historical states, not checks rerun during this inspection.

## 3 Scientific interpretation issues

`QubitManager.IsQubitEntangled` checks positive local Renyi-2 entropy. `EntanglementMetrics.Build` sets its three-party marker when all three local entropies exceed a threshold. Neither condition checks global purity. These predicates are useful within their original assumptions but cannot independently certify entanglement for every density matrix accepted by the engine.

An independent scalar calculation of the checked-in formulas gives:

| Reference state | Global purity | Every local S2 in nats | Every pair E_N in bits | Current three-party formula |
| --- | --- | --- | --- | --- |
| Pure GHZ | 1 | ln(2) | 0 | true, strength 1 |
| Equal mixture of the 000 and 111 projectors | 0.5 | ln(2) | 0 | true, strength 1 |
| I8/8, a product of maximally mixed local states | 0.125 | ln(2) | 0 | true, strength 1 |

The latter two are separable, so exposing this marker as a general entanglement statement would be misleading. The scalar calculation used Python's standard library and analytic state properties; it did **not** execute the C# engine. Default unitary evolution and conditioned measurement from a pure initial state do not imply that these mixtures arise in ordinary current gameplay. They remain relevant to imported states, future noise models, network tolerances and explanatory claims.

The code computes local S2 with natural logarithms, while pair logarithmic negativity and other information quantities use base two. A short Bloch vector, a zero pair metric, a three-party marker, a hidden ribbon and silent arpeggio answer different questions. In particular, the current music can be silent for a state with non-pairwise entanglement.

Only locally authoritative measurement execution raises `MeasurementCompleted`; Fusion separately maintains last-measurement fields and a reliable disentanglement notification. A complete companion event history therefore needs an authority-side journal and publication, not just a client subscription to that local event. Discrete gate history is also not preserved as a complete explanatory log today.

## 4 Machine and execution boundary

Read-only inspection found Ubuntu 24.04.5, `aarch64`, 20 CPU cores, NVIDIA GB10 and driver 580.178.04, approximately 121 GiB memory and about 3.4 TiB available disk at the project filesystem. Docker is available and `docker ps` returned no running containers at inspection time. The system Python did not have NumPy, SciPy, PyTorch, FastAPI or vLLM importable. These facts do not exclude other user environments or stopped services.

`file Assets/Photon/Fusion/Plugins/NanoSockets/Linux/libnanosockets.so` identifies an x86-64 ELF binary. It cannot run natively as ARM64. Unity's [6.0 system requirements](https://docs.unity3d.com/6000.0/Documentation/Manual/system-requirements.html) list ordinary Linux Editor/player/server support on x64; Embedded Linux is a separate path. Consequently, using this machine as an AI service is the first design, with no assumption that the existing Unity/Fusion host runs here.

This task cloned and inspected code, checked hardware, performed the scalar diagnostic and wrote design documentation. It did not install dependencies, download model weights, compile Unity/C#, run existing editor checks, execute a headset build, measure inference latency or start a server.

## 5 Paper and nearby research

The supplied Google Scholar citation page was inaccessible through the research browser. The matching QIXR title and authors were verified through the sources below.

- **Published QIXR paper:** Jamie Ngoc Dinh, Marven Wong, Matthew Brooks, Charles Tahan and Myungin Lee, *Quantum Intuition XR: Tangible Quantum Mechanics using Interactive XR Experience*, IEEE TVCG 32(5), 3668–3678, 2026. [DOI](https://doi.org/10.1109/TVCG.2026.3679913), [publisher record](https://ieeexplore.ieee.org/abstract/document/11460295), [PubMed record and abstract](https://pubmed.ncbi.nlm.nih.gov/41921171/), [author project page](https://www.myunginlee.com/qixr). The published abstract includes technical validation, specialist feedback and a non-expert user study. The full final article was not accessible here; its study sample, effect sizes and detailed final methods are not assumed.
- **Accessible earlier text:** [arXiv 2504.08984v1, 11 April 2025](https://arxiv.org/html/2504.08984v1). The body describes manipulating qubits with H/X/Z/S gates, proximity-driven exchange, multimodal feedback, technical checks and preliminary expert evaluation. Its described current two-qubit path and future multi-user plans must not override the inspected three-qubit branch or substitute for the final article.
- **Embodiment and personality:** Sonlu et al., [*Effects of Embodiment and Personality in LLM-Based Conversational Agents*, IEEE VR 2025](https://www.cs.bilkent.edu.tr/~gudukbay/publications/papers/conf_papers/Sonlu_et_al_IEEE_VR_2025.pdf). It studies personality and embodiment in scientific-topic conversation, including quantum topics; personification alone is an insufficient novelty claim.
- **Quantum tutoring:** Elhaimeur and Chrisochoides, [*From Prototype to Classroom: An Intelligent Tutoring System for Quantum Education*, 2026 preprint](https://arxiv.org/abs/2604.24807). This establishes relevant quantum-education-assistant prior work, with a different teaching-system focus. Its abstract and submission metadata were inspected; this is not a verified peer-reviewed outcome.
- **Adjacent scene control:** [*BreastfeedingSupport Training Using Guard-Mediated LLM-VR Scene Control: Implementation and Technical Evaluation*](https://www.mdpi.com/2076-3417/16/17/8591). A publisher-indexed excerpt describes guarded state-dependent control. Full-page access was rate-limited; retain this as a related-work follow-up, not a completed detailed comparison.
- **Pair separability criterion:** [Horodecki et al., *Separability of Mixed States: Necessary and Sufficient Conditions*](https://arxiv.org/abs/quant-ph/9605038). The 2×2 PPT result supports the pair-level distinction; it must not be generalized automatically to arbitrary three-party claims.

This is a focused initial literature check. The proposed contribution remains a hypothesis about quantum-specific, state-and-event-grounded interaction; neither priority nor improved learning has been established.
