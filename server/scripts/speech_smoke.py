"""Synthetic speech -> real ASR -> real answer -> real TTS over HTTPS.

This is a software loopback test, not microphone/headset or human evidence.
"""
import io
import json
from pathlib import Path
import ssl
import time
import wave

import httpx
import numpy as np
from scipy.signal import resample_poly

from qixr_companion.physics import pack
from qixr_companion.voice import Voice

root=Path(__file__).resolve().parents[2]; runtime=root/'.runtime'
v=Voice(runtime/'models'); v.load()
def auth(token): return {'Authorization':'Bearer '+token}
report=[]
with httpx.Client(base_url='https://127.0.0.1:8786',verify=ssl.create_default_context(cafile=str(runtime/'tls/server.crt')),timeout=120) as c:
    r=c.post('/v1/rooms',headers=auth((runtime/'admin.token').read_text().strip()),json={'name':'speech loopback'}); r.raise_for_status()
    room=r.json(); rid=room['roomId']
    c.post(f'/v1/rooms/{rid}/epoch',headers=auth(room['authorityToken']),json={'expectedEpoch':0}).raise_for_status()
    psi=np.zeros(8,complex); psi[[0,6]]=1/np.sqrt(2)
    c.post(f'/v1/rooms/{rid}/snapshots',headers=auth(room['authorityToken']),json=dict(roomId=rid,authorityEpoch=1,snapshotId='bell',stateVersion=0,density=pack(np.outer(psi,psi.conj())))).raise_for_status()
    for language,text in [('ko','내 측정 확률을 알려 줘.'),('en','What are my odds of getting zero?')]:
        joined=c.post(f'/v1/rooms/{rid}/conversations',headers=auth(room['joinToken']),json={'participantId':'speech-'+language}).json()
        cid=joined['conversationId']; headers=auth(joined['conversationToken'])
        synthetic=v.speak_sync(text,language)
        with wave.open(io.BytesIO(synthetic)) as f:
            rate=f.getframerate(); samples=np.frombuffer(f.readframes(f.getnframes()),dtype='<i2').astype(float)/32768
        samples=resample_poly(samples,16000,rate)
        out=io.BytesIO()
        with wave.open(out,'wb') as f:
            f.setnchannels(1); f.setsampwidth(2); f.setframerate(16000); f.writeframes((samples*32767).astype('<i2').tobytes())
        start=time.monotonic()
        r=c.post(f'/v1/conversations/{cid}/transcribe?language={language}',headers=headers,content=out.getvalue()); r.raise_for_status()
        transcript=r.json()['text']; asr=time.monotonic()-start
        print(json.dumps({'stage':'transcript','language':language,'input':text,'transcript':transcript,'asrSeconds':asr},ensure_ascii=False),flush=True)
        q=dict(requestId='speech',selectionId='q1',targetQubitIds=[0],referenceSnapshotId='bell',language=language,text=transcript,speak=True)
        c.post(f'/v1/conversations/{cid}/questions',headers=headers,json=q).raise_for_status()
        first_audio=None; result=None
        while True:
            result=c.get(f'/v1/conversations/{cid}/requests/speech',headers=headers).json()
            if result['audioCount'] and first_audio is None:
                data=c.get(f'/v1/conversations/{cid}/requests/speech/audio/0',headers=headers); data.raise_for_status()
                (runtime/f'first-answer-{language}.wav').write_bytes(data.content)
                first_audio=time.monotonic()-start
            if result['state'] in ['done','error','cancelled']: break
            time.sleep(.03)
        assert result['state']=='done',result
        if result['result']['intent']!='probability':
            failure=dict(language=language,input=text,transcript=transcript,intent=result['result']['intent'],asrSeconds=asr)
            with (runtime/'speech-failures.jsonl').open('a') as f: f.write(json.dumps(failure,ensure_ascii=False)+'\n')
            c.delete('/v1/rooms/'+rid,headers=auth((runtime/'admin.token').read_text().strip())).raise_for_status()
            raise AssertionError('Speech routing failed: '+json.dumps(failure,ensure_ascii=False))
        assert abs(result['result']['facts']['nodes'][0]['p0']-.5)<1e-7
        row=dict(language=language,input=text,transcript=transcript,asrSeconds=asr,
                 firstAudioAvailableSeconds=first_audio,planner=result['result']['planner'],status=result['result']['status'])
        report.append(row); print(json.dumps(row,ensure_ascii=False),flush=True)
        c.delete('/v1/conversations/'+cid,headers=headers).raise_for_status()
    c.delete('/v1/rooms/'+rid,headers=auth((runtime/'admin.token').read_text().strip())).raise_for_status()
(runtime/'speech-smoke.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
