"""Independent small-density-matrix reference. All mutations return new arrays."""
import math

import numpy as np

from .contracts import Snapshot

VERSION = "density-reference-1"
GATES = {
    "H": np.array([[1, 1], [1, -1]], complex) / np.sqrt(2),
    "X": np.array([[0, 1], [1, 0]], complex),
    "Y": np.array([[0, -1j], [1j, 0]], complex),
    "Z": np.diag([1, -1]).astype(complex),
    "S": np.diag([1, 1j]).astype(complex),
}


def unpack(snapshot: Snapshot):
    a = np.array(snapshot.density, dtype=np.float64).reshape(-1, 2)
    return (a[:, 0] + 1j * a[:, 1]).reshape(2**snapshot.qubitCount, -1)


def pack(rho):
    return np.stack((rho.real, rho.imag), axis=-1).ravel().tolist()


def validate(snapshot):
    rho = unpack(snapshot)
    tol = 1e-5 if snapshot.densityEncoding.endswith("f32") else 1e-7
    if not np.isfinite(rho).all() or not np.allclose(rho, rho.conj().T, atol=tol, rtol=0):
        raise ValueError("Density matrix must be finite and Hermitian")
    if abs(np.trace(rho) - 1) > tol or np.linalg.eigvalsh(rho).min() < -tol:
        raise ValueError("Density matrix must have trace 1 and be positive semidefinite")
    return rho, tol


def reduced(rho, keep, n):
    # Explicit index construction is independent from the C# partial-trace helper.
    keep = list(keep)
    rest = [i for i in range(n) if i not in keep]
    tensor = rho.reshape([2] * (2 * n))
    perm = keep + rest + [i+n for i in keep] + [i+n for i in rest]
    tensor = tensor.transpose(perm).reshape(2**len(keep), 2**len(rest), 2**len(keep), 2**len(rest))
    return np.einsum("abcb->ac", tensor)


def entropy(rho):
    w = np.clip(np.linalg.eigvalsh(rho), 0, 1)
    w = w[w > 1e-14]
    return float(-np.sum(w * np.log2(w)))


def facts(snapshot):
    rho, tol = validate(snapshot)
    n = snapshot.qubitCount
    purity = float(np.trace(rho @ rho).real)
    pure = abs(purity-1) <= tol * 4
    nodes = []
    for q in range(n):
        local = reduced(rho, [q], n)
        p = float(np.trace(local @ local).real)
        bloch = [float(2*local[0, 1].real), float(-2*local[0, 1].imag), float((local[0, 0]-local[1, 1]).real)]
        nodes.append(dict(qubitId=q, label=f"Q{q+1}", bloch=bloch,
                          blochRadius=float(np.linalg.norm(bloch)), purity=p,
                          renyi2Nats=-math.log(max(1e-15, min(1, p))),
                          entropyBits=entropy(local), p0=float(np.clip(local[0, 0].real, 0, 1)),
                          p1=float(np.clip(local[1, 1].real, 0, 1)),
                          entangledWithRest=(p < 1-tol*4) if pure else None))
    pairs = []
    for a in range(n):
        for b in range(a+1, n):
            pair = reduced(rho, [a, b], n)
            pt = pair.reshape(2, 2, 2, 2).transpose(0, 3, 2, 1).reshape(4, 4)
            eig = np.linalg.eigvalsh(pt)
            negativity = float(np.abs(eig[eig < 0]).sum())
            pairs.append(dict(first=a, second=b, negativity=negativity,
                              logarithmicNegativityBits=float(np.log2(1+2*negativity)),
                              mutualInformationBits=max(0., nodes[a]['entropyBits']+nodes[b]['entropyBits']-entropy(pair)),
                              entanglementStatus="detected" if eig.min() < -tol*4 else "below_resolution"))
    return dict(calculationVersion=VERSION, snapshotId=snapshot.snapshotId, stateVersion=snapshot.stateVersion,
                authorityEpoch=snapshot.authorityEpoch, tolerance=tol, globalPurity=purity,
                globallyPure=pure, nodes=nodes, pairs=pairs,
                genuineTripartitePureCriterion=bool(n == 3 and pure and all(x['entangledWithRest'] for x in nodes)))


def apply_gate(rho, gate, target, n):
    if gate not in GATES or target not in range(n):
        raise ValueError("Unsupported gate or target")
    u = np.array([[1]], complex)
    for q in range(n):
        u = np.kron(u, GATES[gate] if q == target else np.eye(2))
    return u @ rho @ u.conj().T


def measure_branches(rho, target, n):
    if target not in range(n):
        raise ValueError("Invalid measurement target")
    branches = []
    for outcome in (0, 1):
        mask = np.array([((i >> (n-1-target)) & 1) == outcome for i in range(2**n)])
        projected = rho * np.outer(mask, mask)
        p = float(np.trace(projected).real)
        if p > 1e-12:
            branches.append(dict(outcome=outcome, probability=p, density=pack(projected/p)))
    return branches


def preview(snapshot, operation, target):
    rho, _ = validate(snapshot)
    if operation == "measureZ":
        return dict(kind="sandbox", sourceSnapshotId=snapshot.snapshotId, operation=operation,
                    target=target, branches=measure_branches(rho, target, snapshot.qubitCount))
    state = apply_gate(rho, operation, target, snapshot.qubitCount)
    fork = snapshot.model_copy(update={"density": pack(state), "snapshotId": "sandbox-"+snapshot.snapshotId})
    return dict(kind="sandbox", sourceSnapshotId=snapshot.snapshotId, operation=operation,
                target=target, density=fork.density, facts=facts(fork))


def check_event(event, before, after):
    if before.qubitCount != after.qubitCount or event.target >= before.qubitCount:
        raise ValueError("Event target or state shape mismatch")
    rho, t = validate(before)
    actual, t2 = validate(after)
    if event.operation == "measureZ":
        branch = next((b for b in measure_branches(rho, event.target, before.qubitCount) if b['outcome'] == event.outcome), None)
        if branch is None or event.probability is None or abs(branch['probability']-event.probability) > max(t, t2)*8:
            raise ValueError("Measurement outcome/probability contradicts the state")
        a = np.array(branch['density']).reshape(-1, 2)
        expected = (a[:, 0]+1j*a[:, 1]).reshape(rho.shape)
    else:
        expected = apply_gate(rho, event.operation, event.target, before.qubitCount)
    if not np.allclose(expected, actual, atol=max(t, t2)*8, rtol=0):
        raise ValueError("Event does not reproduce the after-state")
