"""Compare wire DTOs and independently recomputed metrics from actual C# sources."""
import json
from pathlib import Path

import numpy as np

from qixr_companion.contracts import Snapshot
from qixr_companion.physics import facts

root=Path(__file__).resolve().parents[2]
rows=json.loads((root/'.runtime/csharp-goldens.json').read_text())
for row in rows:
    s=Snapshot.model_validate(row['snapshot']); f=facts(s)
    for actual,reference in zip(f['nodes'],row['nodes']):
        np.testing.assert_allclose([actual['purity'],actual['blochRadius'],actual['renyi2Nats']],
            [reference['purity'],reference['radius'],reference['renyi2Nats']],atol=1e-7,rtol=0)
    for actual,reference in zip(f['pairs'],row['pairs']):
        np.testing.assert_allclose([actual['logarithmicNegativityBits'],actual['mutualInformationBits']],
            [reference['logarithmicNegativityBits'],reference['mutualInformationBits']],atol=1e-7,rtol=0)
print('PASS:',len(rows),'C# wire snapshots and Python metric comparisons')
