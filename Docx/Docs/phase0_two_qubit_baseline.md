# Phase 0 — 2-큐빗 기준선 고정

상위 계획: [3-큐빗 확장 검토](three_qubit_extension_review.md)

## 완료한 준비 작업

- `Assets/Editor/QixrTwoQubitBaselineChecks.cs`를 추가했다. Unity Editor 메뉴 `QIXR > Validation > Run Two-Qubit Math Baseline`은 씬을 바꾸지 않고 다음을 확인한다.
  - H/X/Z/S의 unitary 성질
  - `H |0⟩⟨0| H† = |+⟩⟨+|`
  - 현재 truncated exchange exponential의 unitary 성질(`J=1, t=0.1`), trace 보존, Hermiticity
- `Assets/Scripts/Validation/TwoQubitBaselineRecorder.cs`를 추가했다. `QubitManager` GameObject에 붙여 `Record On Start`를 켜거나 Context Menu의 `Log Two-Qubit Baseline Snapshot`을 실행하면, 전역 `ρ`, 두 reduced state `ρ0`, `ρ1`, trace, purity, S2 entropy를 Unity log에 기록한다. 상태를 변경하지 않는다.

## 아직 실행해서 확보해야 하는 산출물

코드만으로 Quest 3의 실제 tracking, physics, audio, XR frame time을 측정할 수는 없다. 아래 절차로 **2-큐빗 기준 자료**를 확보한 뒤 그 결과를 이 문서 하단에 붙인다. 그 전에는 Phase 0을 “준비됨, 실기기 capture 대기” 상태로 본다.

1. Unity Editor에서 `QIXR > Validation > Run Two-Qubit Math Baseline`을 실행한다. Console의 `Two-qubit math baseline passed`를 저장한다.
2. `main.unity`의 `QubitManager`에 `TwoQubitBaselineRecorder`를 추가한다.
3. Inspector에서 `Record On Start`를 켜고, capture delay를 `0`, `0.5`, `1`, `2`초로 둔다. 현재 scene은 0.5초에 Q0에 X를 자동 적용하므로 이 시점은 반드시 기록한다.
4. Editor Play Mode에서 한 번, Quest 3 development build에서 한 번 실행한다. Console/Player log에서 `TWO_QUBIT_SNAPSHOT` 블록 전체를 복사한다.
5. 각 플랫폼에서 아래 수동 시나리오를 영상으로 녹화한다. 화면 녹화와 해당 시점의 log label을 함께 보관한다.
   - 초기 `|00⟩` 상태
   - Q0 또는 Q1에 H/X/Z/S 각각 적용
   - 두 큐빗을 멀리 둔 상태
   - 두 큐빗을 interaction threshold 안으로 가져온 뒤 exchange animation 관찰
   - 한 큐빗 Z 측정 후 결과와 시각화 변화 관찰
6. Quest 3에서는 Unity Profiler 또는 OVR Metrics를 사용해 각 시나리오의 CPU/GPU frame time, FPS, GC allocation을 기록한다. 3-큐빗 목표는 동일한 scene quality에서 최소 72 FPS를 유지하는 것이다.

## 통과 기준

| 항목 | 기준 |
| --- | --- |
| Matrix check | Editor 메뉴가 error 없이 pass한다. |
| Density matrix | 각 snapshot에서 `Tr(ρ) ≈ 1`, `ρ = ρ†`, purity가 물리 범위 `0 < Tr(ρ²) ≤ 1`에 있다. |
| Local gate | 게이트 대상 Bloch sphere가 예상 축으로 변한다. |
| Exchange | 근접 시 arc/audio/Bloch-radius 변화가 나타나고, 멀리 두면 안정된다. |
| Measurement | 0 또는 1 결과가 기록되고, snapshot의 사후 상태가 정규화된다. |
| XR performance | Quest 3에서 심각한 frame drop·GC spike·audio clipping이 없다. 수치 결과를 아래에 기록한다. |

## 확보한 기준 자료

아직 미실행. 위 절차를 실행한 뒤 다음 형식으로 추가한다.

```text
Unity version / build commit:
Quest OS / headset:
Editor matrix check:
Snapshot logs:
Performance: median FPS, 1% low FPS, CPU/GPU frame time, GC/frame
Known deviations:
```

## Phase 1 진입 조건

1. Editor 수치 검증 pass와 Editor·Quest 3 snapshot을 보관한다.
2. 기존 2-큐빗 주요 동작의 화면 녹화를 보관한다.
3. 자동 X 적용, interaction tick, 각 visual/audio effect가 의도된 baseline인지 연구팀이 승인한다.

이 세 조건이 충족되면 Phase 1에서만 8×8 상태 엔진과 3-큐빗 연산을 구현한다. 이 기준선이 없으면, 3-큐빗 리팩터링 뒤의 차이가 새 기능의 오류인지 기존 동작의 변화인지 판별할 수 없다.
