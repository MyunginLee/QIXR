"""Real gateway smoke/load test. Uses isolated rooms; never injects into a Unity room."""
import argparse
import asyncio
import json
from pathlib import Path
import ssl
import statistics
import time

import httpx
import numpy as np

from qixr_companion.physics import pack

parser=argparse.ArgumentParser()
parser.add_argument('--url',default='https://127.0.0.1:8786')
parser.add_argument('--concurrency',default='1,2,4,8')
parser.add_argument('--voice',action='store_true')
parser.add_argument('--language',choices=['en','ko'],default='en')
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]; runtime=root/'.runtime'
def auth(token): return {'Authorization':'Bearer '+token}

async def main():
    context=ssl.create_default_context(cafile=str(runtime/'tls/server.crt'))
    async with httpx.AsyncClient(base_url=args.url,verify=context,timeout=120) as c:
        room=(await c.post('/v1/rooms',headers=auth((runtime/'admin.token').read_text().strip()),json={'name':'isolated load test'}))
        room.raise_for_status(); room=room.json(); rid=room['roomId']
        await c.post(f'/v1/rooms/{rid}/epoch',headers=auth(room['authorityToken']),json={'expectedEpoch':0})
        psi=np.zeros(8,complex); psi[[0,6]]=1/np.sqrt(2)
        snapshot=dict(roomId=rid,authorityEpoch=1,snapshotId='bell-load',stateVersion=0,density=pack(np.outer(psi,psi.conj())))
        r=await c.post(f'/v1/rooms/{rid}/snapshots',headers=auth(room['authorityToken']),json=snapshot); r.raise_for_status()
        members=[]
        for i in range(8):
            r=await c.post(f'/v1/rooms/{rid}/conversations',headers=auth(room['joinToken']),json={'participantId':f'load-{i}'})
            r.raise_for_status(); members.append(r.json())
        async def turn(i,run):
            member=members[i]; cid=member['conversationId']; h=auth(member['conversationToken'])
            start=time.monotonic(); request_id=f'load-{run}-{i}'
            # Deliberately requires the real model router rather than the keyword fast path.
            q=dict(requestId=request_id,selectionId='load',referenceSnapshotId='bell-load',targetQubitIds=[0],
                   language=args.language,text=('Could either member tell its partner something instantly?' if args.language=='en'
                       else '이 관계만으로 한쪽에서 고른 정보를 상대에게 곧바로 전달할 수 있나요?'),speak=args.voice)
            r=await c.post(f'/v1/conversations/{cid}/questions',headers=h,json=q); r.raise_for_status()
            while True:
                result=(await c.get(f'/v1/conversations/{cid}/requests/{request_id}',headers=h)).json()
                if result['state'] in ['done','error','cancelled']:
                    body=result.get('result') or {}
                    return dict(client=i,state=result['state'],planner=body.get('planner'),status=body.get('status'),
                        intent=body.get('intent'),captionSeconds=body.get('captionSeconds'),firstAudioSeconds=body.get('firstAudioSeconds'),
                        totalSeconds=time.monotonic()-start,modelTiming=body.get('modelTiming'))
                await asyncio.sleep(.05)
        reports=[]
        for index,n in enumerate(map(int,args.concurrency.split(','))):
            turns=await asyncio.gather(*(turn(i,index) for i in range(n)))
            report=dict(concurrency=n,voice=args.voice,turns=turns)
            captions=[t['captionSeconds'] for t in turns if t['captionSeconds'] is not None]
            if captions: report.update(medianCaptionSeconds=statistics.median(captions),maxCaptionSeconds=max(captions))
            reports.append(report); print(json.dumps(report,ensure_ascii=False),flush=True)
        (runtime/('load-voice.json' if args.voice else 'load-text.json')).write_text(json.dumps(reports,ensure_ascii=False,indent=2))
        # Remove participant credentials and fixture room after testing.
        for member in members: await c.delete('/v1/conversations/'+member['conversationId'],headers=auth(member['conversationToken']))
        r=await c.delete('/v1/rooms/'+rid,headers=auth((runtime/'admin.token').read_text().strip())); r.raise_for_status()

asyncio.run(main())
