import numpy as np
import pytest

from qixr_companion.contracts import Snapshot
from qixr_companion.physics import pack


def state(name='product'):
    if name == 'product':
        psi=np.eye(8,dtype=complex)[0]; rho=np.outer(psi,psi.conj())
    elif name == 'bell':
        psi=np.zeros(8,complex); psi[[0,6]]=1/np.sqrt(2); rho=np.outer(psi,psi.conj())
    elif name == 'ghz':
        psi=np.zeros(8,complex); psi[[0,7]]=1/np.sqrt(2); rho=np.outer(psi,psi.conj())
    elif name == 'w':
        psi=np.zeros(8,complex); psi[[1,2,4]]=1/np.sqrt(3); rho=np.outer(psi,psi.conj())
    elif name == 'classical':
        rho=np.zeros((8,8),complex); rho[0,0]=rho[7,7]=.5
    elif name == 'mixed': rho=np.eye(8)/8
    else: raise ValueError(name)
    return Snapshot(roomId='room', authorityEpoch=1,snapshotId=name,stateVersion=0,density=pack(rho))


@pytest.fixture
def snapshot(): return state()
