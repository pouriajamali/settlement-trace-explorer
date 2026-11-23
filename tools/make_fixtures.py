"""Original synthetic cases and explicit expected axes, authored before runner.

This is a corpus builder, not an oracle that calls/reimplements the runner.
The expected table is reviewed domain intent; test success is bounded to it.
"""
from copy import deepcopy
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
I = dict(requestId='demo-A', task='echo', model='synthetic-v1', inputDigest='a'*64,
         expectedOutputDigest='b'*64, sourceChain=1, destinationChain=137,
         assetId='synthetic:USD-A', decimals=6, minimumAtomic='1000000', chargeAtomic='1000000')
P = dict(status='DONE', substatus='COMPLETED', receiving=dict(amount='1000000',chainId=137,
         token=dict(address='synthetic:USD-A',decimals=6)), vendorExtra={'keep': ['native', 1]})
S = dict(requestId='demo-A', task='echo', model='synthetic-v1', inputDigest='a'*64,
         outputDigest='b'*64, chargeAtomic='1000000', destinationChain=137,
         assetId='synthetic:USD-A', decimals=6, state='succeeded', verificationType='synthetic-declaration')
def event(kind, native, n=0):
    return dict(id=f'{kind}-{n}', kind=kind, observedAt=f'2026-11-23T10:00:0{n}Z', native=native)
def trace(p=P,s=None):
    return dict(traceVersion='1.0',profile='lifi-status-observation-v1',intent=deepcopy(I),
                events=([event('provider',deepcopy(p))] if p is not None else []) +
                       ([event('service',deepcopy(s),1)] if s is not None else []))
cases=[]
def add(name,t,route='completed',delivery='reportedAtLeastMinimum',service='unknown',freshness='unassessed',duplicates=0,error=None):
    expected=dict(route=route,delivery=delivery,service=service,freshness=freshness,
                  ledger='notIntegrated',onChainTruth='notVerified',resend='notAuthorized',duplicates=duplicates)
    cases.append(dict(name=name,trace=t,expected=expected if error is None else dict(error=error)))
add('complete-no-service',trace())
add('complete-service-success',trace(s=S),service='reportedSucceeded')
z=deepcopy(S); z['state']='failed'; add('paid-service-failed',trace(s=z),service='reportedFailed')
for field,value in [('requestId','demo-B'),('task','different'),('model','different'),('inputDigest','c'*64),
                    ('outputDigest','c'*64),('chargeAtomic','2'),('assetId','synthetic:USD-B'),
                    ('destinationChain',1),('decimals',18)]:
    z=deepcopy(S);z[field]=value;add('service-wrong-'+field,trace(s=z),service='bindingMismatch')
z=deepcopy(S);z['verificationType']='zk';add('unsupported-proof',trace(s=z),service='unsupportedClaim')
z=deepcopy(S);z['state']='FUTURE';add('unknown-service-state',trace(s=z),service='unknown')
for status,sub,r in [('DONE','PARTIAL','partial'),('DONE','REFUNDED','refunded'),('FAILED','REFUNDED','refunded'),
                     ('PENDING','WAIT_DESTINATION_TRANSACTION','pending'),('FAILED','EXPIRED','failed'),
                     ('FAILED','UNKNOWN_ERROR','unknown'),('DONE','NEW_ENUM','unknown'),('NEW_ENUM',None,'unknown'),
                     ('NOT_FOUND',None,'unknown')]:
    z=deepcopy(P);z['status']=status
    if sub is None: z.pop('substatus',None)
    else:z['substatus']=sub
    d='reportedAtLeastMinimum' if r in ['partial','completed'] else 'unknown'
    add('route-'+status+'-'+str(sub),trace(z),route=r,delivery=d)
z=deepcopy(P);z['receiving']['amount']='1';add('below-minimum',trace(z),delivery='reportedBelowMinimum')
z=deepcopy(P);z['receiving']['amount']='0';add('zero-is-observed',trace(z),delivery='reportedBelowMinimum')
z=deepcopy(P);z.pop('receiving');add('missing-receiving',trace(z),delivery='unknown')
for field,value in [('chainId',1),('address','synthetic:USD-B'),('decimals',18)]:
    z=deepcopy(P)
    if field=='chainId':z['receiving'][field]=value
    else:z['receiving']['token'][field]=value
    add('wrong-delivery-'+field,trace(z),delivery='reportedAssetMismatch')
t=trace();t['intent']['minimumAtomic']='9007199254740993';t['events'][0]['native']['receiving']['amount']='9007199254740992'
add('above-js-safe-integer',t,delivery='reportedBelowMinimum')
t=trace();t['intent']['minimumAtomic']=str(2**256-1);t['events'][0]['native']['receiving']['amount']=str(2**256-1)
add('uint256-maximum',t)
t=trace(None);t['events']=[event('timeout',{'elapsedSeconds':60})];add('timeout-no-observation',t,route='unknown',delivery='unknown')
for state in ['current','stale','expired','reorg','unavailable','FUTURE']:
    t=trace();t['events'].append(event('freshness',{'state':state,'height':123,'commitment':'provider-declared'},2))
    add('freshness-'+state,t,delivery='reportedAtLeastMinimum' if state=='current' else 'unknown',freshness=state if state!='FUTURE' else 'unknown')
t=trace();t['events'].append(deepcopy(t['events'][0]));add('duplicate-exact',t,duplicates=1)
t=trace();e=deepcopy(t['events'][0]);e['native']['substatus']='PARTIAL';t['events'].append(e);add('duplicate-conflict',t,error='DUPLICATE_CONFLICT')
t=trace();t['events'].append(event('provider',dict(status='PENDING',substatus='WAIT_DESTINATION_TRANSACTION'),1))
add('new-observation-preserves-history',t,route='pending',delivery='unknown')
t=trace();t['events'].append(event('provider',P,1));t['events'][1]['observedAt']='2026-11-23T09:00:00Z';add('out-of-order',t,error='TIME_ORDER')
for bad in ['1.5','1e6','-1','01',str(2**256)]:
    t=trace();t['intent']['minimumAtomic']=bad;add('invalid-amount-'+bad,t,error='AMOUNT_INVALID')
t=trace();t['profile']='unimplemented';add('unknown-profile',t,error='PROFILE_UNSUPPORTED')
t=trace();t['events'][0]['kind']='financial-write';add('unknown-kind',t,error='KIND_UNSUPPORTED')
ROOT.joinpath('fixtures').mkdir(exist_ok=True)
ROOT.joinpath('fixtures/cases.json').write_text(json.dumps(dict(schema='g2-case-corpus-v1',origin='Original synthetic data, MIT; no live transactions or private handoff content.',cases=cases),indent=2)+'\n',encoding='utf8')
print(json.dumps(dict(cases=len(cases),valid=sum('error' not in c['expected'] for c in cases),invalid=sum('error' in c['expected'] for c in cases))))
