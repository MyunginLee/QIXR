import math
import numpy as np
import pytest

from qixr_companion.contracts import Plan, Question, QuantumEvent
from qixr_companion.dialogue import answer
from qixr_companion.physics import facts,pack,preview,unpack,validate,check_event
from conftest import state


@pytest.mark.parametrize('name,purity,mi,pair_entangled,triad',[
    ('product',1,0,False,False),('bell',1,2,True,False),('ghz',1,1,False,True),
    ('classical',.5,1,False,False),('mixed',.125,0,False,False)])
def test_analytic_states(name,purity,mi,pair_entangled,triad):
    f=facts(state(name))
    assert f['globalPurity']==pytest.approx(purity)
    assert f['pairs'][0]['mutualInformationBits']==pytest.approx(mi)
    assert (f['pairs'][0]['entanglementStatus']=='detected') == pair_entangled
    assert f['genuineTripartitePureCriterion']==triad


def test_local_mixedness_cannot_distinguish_ghz_from_classical():
    for name in ['ghz','classical','mixed']:
        f=facts(state(name))
        assert f['nodes'][0]['renyi2Nats']==pytest.approx(math.log(2))
        assert f['nodes'][0]['blochRadius']==pytest.approx(0)
        assert f['nodes'][0]['entangledWithRest'] is (True if name=='ghz' else None)


def test_w_negativity():
    expected=math.log2((2+math.sqrt(5))/3)
    assert all(p['logarithmicNegativityBits']==pytest.approx(expected) for p in facts(state('w'))['pairs'])


def test_preview_preserves_source_and_ghz_measurement_conditioning():
    s=state('ghz'); before=list(s.density)
    result=preview(s,'measureZ',0)
    assert [b['probability'] for b in result['branches']]==pytest.approx([.5,.5])
    for branch,index in zip(result['branches'],[0,7]):
        a=np.array(branch['density']).reshape(-1,2)
        rho=(a[:,0]+1j*a[:,1]).reshape(8,8)
        expected=np.zeros((8,8)); expected[index,index]=1
        np.testing.assert_allclose(rho,expected)
    assert s.density==before
    p=preview(state(),'H',2)
    assert p['facts']['nodes'][2]['p0']==pytest.approx(.5)
    assert p['facts']['nodes'][0]['p0']==pytest.approx(1)


@pytest.mark.parametrize('bad',[np.eye(8),np.diag([1.1,-.1,0,0,0,0,0,0]),np.triu(np.ones((8,8)))/8])
def test_nonphysical_states_rejected(bad):
    with pytest.raises(ValueError): validate(state().model_copy(update={'density':pack(bad)}))


def test_invalid_event_and_idempotent_physics():
    before=state(); after=before.model_copy(update={'density':preview(before,'H',0)['density'],'stateVersion':1,'snapshotId':'after','eventSequence':1})
    event=QuantumEvent(eventId='e',sequence=1,authorityEpoch=1,operation='H',target=0,beforeSnapshotId='product',afterSnapshotId='after')
    check_event(event,before,after)
    with pytest.raises(ValueError): check_event(event.model_copy(update={'operation':'X'}),before,after)


def test_no_fabricated_target_or_entanglement():
    s=state('classical')
    q=Question(requestId='r',selectionId='s',referenceSnapshotId=s.snapshotId,text='내 상태',targetQubitIds=[0],language='ko')
    result=answer(q,s,Plan(intent='qubit'))
    assert '판단할 수는 없어' in result['text']
    result=answer(q.model_copy(update={'targetQubitIds':[]}),s,Plan(intent='qubit'))
    assert result['status']=='needs_selection'
    result=answer(q.model_copy(update={'targetQubitIds':[0,1]}),state('ghz'),Plan(intent='pair'))
    assert '우리 셋의 얽힘까지 부정' in result['text']
