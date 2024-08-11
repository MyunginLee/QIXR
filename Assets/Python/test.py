import UnityEngine
import qutip as qt
from numpy import sqrt

arr = list()

zero = qt.basis(2, 0)
one= qt.basis(2, 1)

for object in UnityEngine.GameObject.FindObjectsOfType(UnityEngine.GameObject):
    if object.name == "Dot":
        alpha = object.GetComponent("Measure").GetAlpha()
        beta = object.GetComponent("Measure").GetBeta()
        qubit = alpha * zero + beta * one
        arr.append(qubit)
        UnityEngine.Debug.Log(alpha)
        UnityEngine.Debug.Log(beta)


psi = qt.tensor(*arr)

UnityEngine.Debug.Log(qt.entropy_vn(psi.ptrace([0])))
UnityEngine.Debug.Log(qt.entropy_vn(psi.ptrace([1])))
UnityEngine.Debug.Log(qt.entropy_vn(psi.ptrace([0,1])))
UnityEngine.Debug.Log(qt.entropy_vn(psi.ptrace([0])) + qt.entropy_vn(psi.ptrace([1])) - qt.entropy_vn(psi.ptrace([0,1])))


#qt.cnot()
#qt.hadamard_transform()
#qt.sigmax()
#qt.sigmay()
#qt.sigmaz()