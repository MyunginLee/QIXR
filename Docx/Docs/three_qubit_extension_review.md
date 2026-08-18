# QIXR 3-큐빗 확장: 물리·상호작용·시각화 검토 및 구현 명세

## 목적과 결론

이 문서는 `Docx/00_main_with_supp.tex`의 2-큐빗 QIXR을 **모든 기존 기능(잡기, H/X/Z/S 게이트, 근접 교환 상호작용, 얽힘 시각화·음향, 측정, 파동함수 바닥/천장 표현)**을 유지한 채 3-큐빗 버전으로 확장하기 위한 선행 검토다.

핵심 결론은 “Qubit 3” 프리팹을 복제하는 것만으로는 충분하지 않다는 점이다. 3-큐빗 시스템은 8차원 힐베르트 공간과 8×8 전역 밀도행렬을 사용한다. 따라서 (1) 단일 큐빗 연산의 전역 삽입, (2) 모든 큐빗 쌍의 교환 해밀토니언 삽입, (3) 부분추적, (4) 쌍별 얽힘과 진정한 3자 상관관계의 구분, (5) 측정 이후 조건부 상태 갱신을 한 개의 일관된 양자 상태 엔진에서 처리해야 한다.

3개는 성능상 가볍다. 복소 8×8 밀도행렬은 64개의 복소수이며, 병목은 행렬 크기보다 XR 렌더링·TrailRenderer·매 프레임 객체 검색이다. 즉 이번 변경의 위험은 성능보다 **물리적 정확성과 시각적 의미의 혼동**이다.

## 검토 범위와 현재 기준선

연구 문서의 기준선은 두 큐빗, 네 개의 단일-큐빗 게이트(H/X/Z/S), 거리 기반 Heisenberg exchange, Bloch 반지름 축소, 두 큐빗 사이의 호(arc), Z-기저 측정이다. 해당 설명은 `00_main_with_supp.tex`의 “Quantum Intuition: Principle and Implementation” 절(특히 density matrix, exchange interaction, entanglement, measurement)에 있다.

현재 Unity 구현에서 직접 확인한 관련 지점은 다음과 같다.

| 대상 | 현재 역할 | 3-큐빗에서의 문제/변경 |
| --- | --- | --- |
| `Assets/Scripts/Qubit/QubitManager.cs` | 전역 밀도행렬, 단일-큐빗 측정, 거리 검사 | 전역 상태를 소유해야 하는 올바른 출발점이지만, 2-스핀 4×4 행렬을 3-큐빗 공간에 삽입하지 않으며 단일 게이트를 얽힘 그래프 전체에 적용한다. |
| `Assets/Scripts/Qubit/Gates.cs` | H/X/Z/S 및 4×4 exchange Hamiltonian | 2-큐빗 전용 `Hamiltonian2Spins`/`SpinExchange`를 임의의 쌍 `(i,j)`에 대한 8×8 연산자로 일반화해야 한다. |
| `Assets/Scripts/Qubit/Qubit.cs` | 큐빗별 Bloch 표현 및 게이트 충돌 | `Awake()` 순서로 인덱스와 텐서 행렬을 만든다. 씬 로딩 순서에 의존하므로 고정된 큐빗 ID와 중앙 연산 API로 대체해야 한다. |
| `Assets/Scripts/Entanglement/Entanglement.cs` | 2-큐빗 거리/boolean/트레일 시각화 | 인접 인덱스 `(j,j+1)`만 검사하고 boolean `entangled[]` 하나를 사용한다. 세 쌍 AB/AC/BC 및 3자 상태를 표현하지 못한다. |
| `Assets/Environment/Orbit/Floor.cs`와 `Hydrogen-Floor.shader` | 각 큐빗의 바닥 wavefunction | C# 배열은 동적 길이이나 shader 배열 `_Centers[2]`, `_WaveFunctionParams[2]`가 2개로 고정되어 있다. 최소 3개 이상으로 바꿔야 한다. |
| `Assets/Scenes/main.unity` | QubitShell 1/2 및 두 큐빗 배치 | Qubit 3, shell/dot/line/오디오/상호작용 참조, 초기 위치, 태그와 정렬 순서를 명시적으로 추가해야 한다. |

## 확장 후의 양자 모델

### 1. 상태, 순서, 불변조건

고정 순서 `Q0, Q1, Q2`를 씬의 `GameObject.FindGameObjectsWithTag` 결과가 아니라 Inspector의 `QubitId`(0, 1, 2)로 정의한다. 계산 기저의 순서는 다음처럼 문서와 코드에서 단 한 번 정한다.

`|q0 q1 q2⟩ = |000⟩, |001⟩, …, |111⟩`.

전역 상태는

`ρ ∈ C^(8×8), ρ = ρ†, ρ ⪰ 0, Tr(ρ)=1`

이다. 초기 상태는 `ρ0 = |000⟩⟨000|`로 한다. 모든 단위ary 연산은 `ρ ← UρU†`로 적용한다. 매 개발 빌드/테스트에서 Hermiticity, trace 1, positive-semidefinite 허용오차를 검사한다.

순수 상태만 다루는 선택지도 가능하지만, 현재 시스템은 이미 density matrix를 사용하고 측정·부분추적·향후 decoherence를 목표로 한다. 따라서 3-큐빗도 density-matrix 모델을 유지한다.

### 2. 단일-큐빗 게이트: 로컬 연산이어야 함

기존 H/X/Z/S 기능은 유지한다. 다만 사용자가 `Qi`를 게이트에 넣으면 오직 `Qi`에만

`U_i = I ⊗ … ⊗ U ⊗ … ⊗ I`

를 적용한다. 예를 들어 Q2에 X를 적용하면 `I ⊗ I ⊗ X`다. 큐빗이 얽혀 있어도 로컬 게이트가 다른 큐빗에 물리적으로 “전파”되지는 않는다. 전역 상태가 바뀌므로 다른 큐빗의 reduced state와 시각화는 바뀔 수 있지만, 그것은 로컬 게이트를 복제해서 적용한 결과가 아니다.

따라서 `ApplyGateAcrossEntanglement`와 `ResolveGateTargets`의 그래프 전파 의미는 제거하고, `QuantumState.ApplySingleQubitGate(qubitId, gate)`로 교체한다.

### 3. 거리 기반 교환 상호작용: 세 쌍 모두

각 쌍 `(i,j) ∈ {(0,1),(0,2),(1,2)}`에 대해 거리 `dij`에서 coupling `Jij(dij)`를 계산한다. 현재의 거리 임계값과 부드러운 coupling 함수라는 인터랙션 은유는 유지할 수 있다. 다만 해밀토니언을 전체 공간에 삽입해야 한다.

`H(t) = Σ_(i<j) Jij(t) / 4 · (Xi Xj + Yi Yj + Zi Zj)`.

여기서 `Xi Xj`는 Q_i에 X, Q_j에 X, 나머지에 I를 텐서곱한 8×8 행렬이다. 한 업데이트의 작은 시간 간격 `Δt`에는

`U(t+Δt,t) ≈ exp[-i H(t) Δt]`

를 적용한다. 3-큐빗에서는 두 개 이상의 쌍이 동시에 가까울 수 있고, pair Hamiltonian들이 일반적으로 commute하지 않는다. 그러므로 다음 중 하나를 명시적으로 선택한다.

1. **권장: 합성 해밀토니언.** 매 physics tick에 세 pair term을 더한 8×8 `H(t)` 하나를 지수화한다. 3개에서는 충분히 저렴하고 가장 명료하다.
2. **대안: 1차/2차 Trotter 분해.** `U01(Δt) U02(Δt) U12(Δt)`를 작은 `Δt`에 적용한다. 더 많은 큐빗으로 확장할 때 유용하지만 순서·오차를 문서화해야 한다.

현재 코드의 `time = 1f`를 매 0.1초마다 다시 적용하는 방식은 물리 시간 적분으로 바꾼다. `Δt = Time.fixedDeltaTime × simulationTimeScale`를 쓰고, 상태 업데이트는 `FixedUpdate` 또는 일정한 simulation tick에서 정확히 한 번 수행한다.

### 4. reduced state와 Bloch sphere

각 Q_i의 reduced density matrix는 나머지 두 큐빗을 추적 소거하여 얻는다.

`ρ_i = Tr_{0,1,2 \ {i}}(ρ)`.

Bloch vector는 `b_i = (Tr(ρ_i X), Tr(ρ_i Y), Tr(ρ_i Z))`로 계산한다. 길이 `r_i = ||b_i||`는 0에서 1이다.

- `r_i = 1`: Q_i가 나머지와 분리된 순수 상태. 기존의 완전한 Bloch sphere를 유지한다.
- `0 < r_i < 1`: Q_i가 나머지와 상관되어 reduced state가 mixed. 기존의 축소 sphere 은유를 유지한다.
- `r_i = 0`: Q_i의 국소 상태가 maximally mixed. sphere 중심점까지 축소하되, “큐빗이 사라졌다”가 아니라 “국소적으로는 완전한 방향 정보를 갖지 않는다”는 범례를 제공한다.

현재 `PartialTrace(index)`는 일반 형태에 가까우므로, 3-큐빗과 임의 부분집합에 대해 검증 가능한 범용 `PartialTrace(keepQubitIds)` API로 재작성한다. 2-큐빗 reduced state `ρij`도 필요하다.

### 5. 얽힘은 한 개의 boolean이나 한 개의 edge 값이 아님

3-큐빗에서 “얽힘”은 최소 세 층으로 나누어 보여야 한다.

| 표시 층 | 물리량 | XR 의미 | 중요한 해석 제한 |
| --- | --- | --- |
| 노드 | `r_i` 및 `S2(ρ_i) = -ln Tr(ρ_i²)` | sphere 축소·노드 색/밝기 | Q_i와 나머지 둘 사이의 상관관계이지 특정 상대와의 pair entanglement는 아님. |
| 쌍 edge AB/AC/BC | `ρij = Tr_k(ρ)`의 two-qubit entanglement, 권장값은 logarithmic negativity `EN(ρij)=log2 ||ρij^(T_j)||_1` | 해당 두 노드 사이 arc의 존재·굵기·음량 | 단순 `S_i+S_j-S_ij`는 mutual information(전체 상관관계)이며 일반적으로 entanglement 자체가 아니다. 이 값을 쓸 경우 “correlation”으로 명명한다. |
| 3자 hyperedge | 세 큐빗 모두에 걸친 correlation | 세 점을 잇는 삼각 막/중심 매듭/3성 화음 | GHZ-type 3-tangle은 W-type genuine tripartite entanglement를 0으로 둘 수 있으므로 단독 일반 지표로 쓰면 안 된다. |

권장 초기 설계는 정확성과 교육성을 분리해 보여 주는 것이다.

- pair arc: `EN(ρij) > ε`일 때만 표시한다.
- central triad: 세 bipartition entropy `S(ρ0), S(ρ1), S(ρ2)`가 모두 양수일 때 표시하고 “three-party correlation”으로 라벨한다.
- optional GHZ badge: pure-state 조건에서만 3-tangle을 계산해 GHZ-type임을 별도로 보인다. W 상태에는 0일 수 있음을 UI/논문에서 명시한다.

이 구분은 GHZ 상태에서 특히 필요하다. GHZ `(|000⟩+|111⟩)/√2`는 각 큐빗의 sphere가 0까지 축소되지만, 어떤 두 큐빗의 reduced state는 entangled가 아니다. 따라서 3개의 pair arc만으로 GHZ를 렌더링하면 잘못된 그림이 된다.

### 6. 측정

Q_i의 Z-측정은 전역 projector

`Π_i(s) = I ⊗ … ⊗ |s⟩⟨s| ⊗ … ⊗ I`

로 처리한다. 결과 확률은 `p_s = Tr(Π_i(s)ρ)`, 사후 상태는 `ρ' = Π_i(s)ρΠ_i(s)/p_s`다. **사용자가 Q_i 하나를 측정하면 Q_i만 측정한다.** 얽힘 그래프의 모든 큐빗을 순차적으로 임의 측정하는 현재 동작은 제거한다. 다만 측정 결과에 따라 나머지 큐빗의 conditional reduced state가 바뀌며, 이것이 collapse와 correlation을 보여 주는 핵심 피드백이다.

선택 기능으로 “joint measurement”를 별도 도구로 제공할 수 있으나, single measurement와 같은 버튼/제스처에 숨기지 않는다.

## 권장 소프트웨어 구조

### 단일 진실원천

`QuantumStateEngine`(또는 현 `QubitManager`의 전면 개편)이 오직 하나의 `ρ`, qubit ordering, time evolution, 측정 RNG seed, 검증을 소유한다. `Qubit` MonoBehaviour는 렌더링과 XR 입력만 담당한다.

공개 API의 최소 예시는 다음과 같다.

```csharp
Initialize(IReadOnlyList<QubitHandle> orderedQubits, InitialState.Zero);
ApplySingleQubitGate(int qubitId, ComplexMatrix gate2x2);
StepExchange(IReadOnlyDictionary<QubitPairId, float> couplingJ, float deltaTime);
MeasureZ(int qubitId, IRandomSource rng); // outcome와 probability 반환
GetSingleQubitState(int qubitId);          // ρ_i
GetPairState(int firstId, int secondId);   // ρ_ij
GetVisualizationMetrics();
```

### 행렬 유틸리티

반드시 테스트 가능한 순수 함수로 분리한다.

- `EmbedSingle(U, target, n)` → 2^n×2^n
- `EmbedPair(A, first, second, n)` → 2^n×2^n; 인접하지 않은 Q0–Q2도 처리
- `PartialTrace(rho, keepSet, n)` → 2^|keepSet|×2^|keepSet|
- `ProjectiveMeasureZ(rho, target, sample)`
- `BuildHeisenbergHamiltonian(pairCouplings, n)`
- `ValidateDensityMatrix(rho, tolerance)`

`ComplexMatrix`가 eigenvalue/singular-value API를 안정적으로 제공하는지 확인한다. 제공하지 않으면 2×2 및 4×4 hermitian matrix 전용의 검증된 numerical routine을 추가하거나 MathNet의 안정된 complex linear algebra API로 옮긴다. negativity의 partial transpose와 trace norm은 unit test 없이는 시각화에 연결하지 않는다.

### 씬·프리팹·셰이더

1. Qubit 1과 같은 구조의 **Qubit 3**을 만들되 `QubitId=2`를 Inspector에서 명시한다.
2. QubitShell 3, dot, line renderer, collider, grab interaction, audio, gate guide, tag/layer를 모두 대응시킨다.
3. `QubitRegistry`가 ID 중복·누락을 시작 시 실패로 보고하게 한다. 태그 검색의 배열 순서를 물리/계산 인덱스로 사용하지 않는다.
4. `Hydrogen-Floor.shader`의 `_Centers[2]`, `_WaveFunctionParams[2]`를 최소 `[3]`로 바꾸고, C#에서 `_NumQubits=3`을 보장한다. 향후 N 확장을 원하면 shader의 정적 상한 `MAX_QUBITS`를 별도 상수로 둔다.
5. 한 개의 `EntanglementVisualController`가 node 3개, pair arc 3개, triad 1개를 pool로 소유한다. 300개의 매 프레임 TrailRenderer gradient 재생성은 피하고, material property/particle parameter를 갱신한다.
6. 초기 3개 배치는 정삼각형 또는 충분히 떨어진 세 점으로 시작한다. 모두 가까운 상태로 시작하면 pair coupling 세 개가 동시에 켜져 초심자가 원인을 구분할 수 없다.

## 기존 XR 기능을 보존하는 상호작용 설계

| 기존 기능 | 3-큐빗 보존 방식 |
| --- | --- |
| 잡기/이동 | 세 큐빗 모두 동일하게 grab 가능. 거리 HUD 또는 미세한 halo로 어떤 pair coupling이 켜질지 미리 알린다. |
| H/X/Z/S gate | 사용자가 넣은 한 큐빗에만 적용. 적용 전/후 해당 노드의 Bloch vector와 연결된 edge/triad 변화를 애니메이션한다. |
| 근접 entanglement | AB, AC, BC 각각 독립 coupling을 보여 준다. 두 쌍 이상이 켜질 때는 triad가 점진적으로 등장한다. |
| Bloch sphere | reduced state에 따라 세 sphere 모두 독립적으로 회전/축소한다. 한 노드가 줄었다고 곧바로 특정 다른 한 노드와 Bell pair라는 뜻은 아니라는 legend를 둔다. |
| arc/strings | 세 arc를 서로 다른 색 또는 방향성 없는 pair label로 표시한다. edge 세 개가 겹칠 때 depth/opacity/curvature를 다르게 한다. |
| wavefunction floor/top | 노드별 색은 유지하되, pair overlap과 global triad 상태를 구분한다. 단순 보라색 하나를 “entangled”라는 전역 boolean으로 쓰지 않는다. |
| audio | node tone은 각 `⟨Z_i⟩`, pair tone은 `EN(ρij)`, triad는 세 bipartition entropy의 조합으로 제어한다. edge가 3개일 때 clipping을 피하도록 limiter/voice budget을 둔다. |
| measurement | 선택된 한 Q_i의 결과 0/1와 확률을 보여 준다. 측정 직후 모든 node/edge/triad를 사후 `ρ'`에서 다시 계산한다. |
| pause/time freeze | quantum simulation tick, visual animation, audio modulation을 분리한다. pause는 `ρ` evolution을 멈추되 사용자가 현재 metric을 관찰할 수 있어야 한다. |

## 단계별 구현 순서

### Phase 0 — 기준선 고정

- 현재 2-큐빗 동작을 영상과 state-log로 기록한다.
- 새 `QuantumStateEngine`에 2-큐빗 regression test를 먼저 작성한다.
- 기존 논문에서 주장한 H/X/Z/S, Bell-like exchange, Z measurement 결과를 수치 oracle로 만든다.

### Phase 1 — 3-큐빗 수학 엔진

- 명시적 ID와 8×8 `ρ` 초기화.
- arbitrary target 단일 게이트와 arbitrary pair Heisenberg embedding 구현.
- `FixedUpdate` 기반 합성 해밀토니언 time step 구현.
- 일반 partial trace, 단일 Z measurement, density-matrix invariant validator 구현.
- 이 단계에서는 기존 visual을 연결하지 않고 테스트만 통과시킨다.

### Phase 2 — 정량 metric과 시각화 데이터

- 세 `ρ_i`, 세 `ρ_ij`, Bloch vector/radius, S2 또는 von Neumann entropy 계산.
- pair negativity와 triad 조건 구현·검증.
- `EntanglementSnapshot` 한 개를 매 tick 생산하고 renderer/audio가 이를 읽게 한다.

### Phase 3 — XR 콘텐츠

- Qubit 3/shell/dot/line/prefab/scene 추가.
- 3-node/3-edge/1-triad visual과 wave shader 상한 수정.
- accessibility legend, 색각 보조, node/edge/triad label을 추가.
- XR 컨트롤러의 Grip으로 Q0을 직접 집어 Q1 근처에 놓고 release한 뒤, Q2를 다시 집어 Q0 또는 Q1 근처에 놓는 조작 안내를 제공한다. 시스템이 큐빗을 자동 이동시키거나 합류시키지 않는다.

## 필수 acceptance test

수치 허용오차는 먼저 정한다(예: trace/Hermiticity `1e-8`, float 렌더링 `1e-4`). 아래는 단위 테스트와 XR 수동 테스트 모두에 필요하다.

| 시나리오 | 준비/연산 | 기대 상태·시각화 |
| --- | --- | --- |
| Product baseline | `|000⟩` | 세 sphere 반지름 1, 모든 pair edge/triad 0. |
| Q2 local gate | Q2에 H 또는 X | Q0/Q1에 게이트가 적용되지 않음. `I⊗I⊗U`와 일치. |
| Bell pair + spectator | Q0–Q1만 entangle, Q2는 분리 | Q0/Q1 반지름 축소, AB edge 양수, Q2 반지름 1, AC/BC/triad 0. |
| GHZ | `H(Q0); CNOT(0→1); CNOT(0→2)` — 테스트용 controlled gate 필요 | 세 node가 maximally mixed; pair negativity는 0일 수 있음; central triad는 양수. pair arc만으로 GHZ를 표현하지 않음. |
| W-type reference | unitary preparation circuit 또는 신뢰 가능한 reference state | pairwise와 global correlation 패턴이 GHZ와 다름; GHZ 3-tangle=0을 “no correlation”으로 해석하지 않음. |
| Simultaneous proximity | AB와 AC 또는 세 pair가 임계값 안 | 8×8 합성 H로 정상 진화, trace/positivity 유지, 프레임 안정. |
| Measurement | GHZ에서 Q0 Z-measure | 0/1이 1/2, 사후 Q1/Q2가 같은 conditional 결과를 보임, triad 재계산. |
| Regression | 기존 2-큐빗 scene/configuration | 새 엔진에서 기존 기능과 수치적으로 동등. |

GHZ와 W 검증을 위해 최소한 **controlled two-qubit gate(CNOT 또는 CZ)**를 내부 test API로는 추가해야 한다. 교육 UI에 즉시 노출할 필요는 없지만, 3-큐빗 얽힘 시각화가 실제로 구별되는지 검증하려면 필요하다.

## 구현 전에 결정해야 할 연구/디자인 선택

- 3개 큐빗 모두를 **교환 상호작용으로만** 얽히게 할지, 교육용 CNOT/CZ를 UI에 추가할지.
- triad visual이 의미하는 것을 “genuine tripartite entanglement”가 아닌 보수적인 “three-party correlation”으로 시작할지. 권장은 후자다.
- entropy는 기존 2차 Rényi entropy를 continuity를 위해 유지할지, pair correlation의 명료성을 위해 von Neumann entropy/negativity를 병행할지.
- 사용자에게 edge와 triad metric 수치를 표시할지, 초심자 모드에서는 qualitative legend만 제공할지.
- 3-큐빗 버전을 기존 2-큐빗 실험과 별도 scene/experimental condition으로 둘지. 권장은 기존 scene을 보존하고 `ThreeQubitScene`을 별도로 만들어 regression과 연구 비교를 가능하게 하는 것이다.

## 이번 검토에서 의도적으로 하지 않은 일

이 문서는 구현 계획이다. Qubit 3 프리팹 추가, 엔진 재작성, shader 변경, 논문 LaTeX 수정은 아직 수행하지 않았다. 먼저 위의 metric/interaction 결정을 확정한 뒤, 수학 엔진 → 자동 검증 → XR visual  구현해야 기존 2-큐빗 기능을 보존하면서 물리적으로 방어 가능한 3-큐빗 버전을 만들 수 있다.
