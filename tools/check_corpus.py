"""Corpus integrity and coverage review, independent of the later .NET runner."""
import hashlib
import json
from pathlib import Path

p=Path(__file__).resolve().parents[1]/'fixtures/cases.json'
data=json.loads(p.read_text(encoding='utf8'))
cases=data['cases']; names=[c['name'] for c in cases]
assert len(names)==len(set(names))==49
assert sum('error' in c['expected'] for c in cases)==9
for c in cases:
    if 'error' in c['expected']: continue
    assert c['expected']['ledger']=='notIntegrated'
    assert c['expected']['onChainTruth']=='notVerified'
    assert c['expected']['resend']=='notAuthorized'
    assert c['trace']['events'] or c['name']=='timeout-no-observation'
assert {'partial','refunded','failed','pending','unknown','completed'} <= {c['expected'].get('route') for c in cases}
assert {'reportedSucceeded','reportedFailed','bindingMismatch','unsupportedClaim','unknown'} <= {c['expected'].get('service') for c in cases}
assert {'current','stale','expired','reorg','unavailable','unknown','unassessed'} <= {c['expected'].get('freshness') for c in cases}
pair={c['name']:c for c in cases}
assert pair['complete-service-success']['trace']['events'][0] == pair['paid-service-failed']['trace']['events'][0]
assert pair['above-js-safe-integer']['trace']['intent']['minimumAtomic']=='9007199254740993'
assert int(pair['uint256-maximum']['trace']['intent']['minimumAtomic'])==2**256-1
print(json.dumps({'schema':'g2-corpus-review-v1','cases':49,'validExpected':40,'invalidExpected':9,
 'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),
 'result':'Coverage/integrity assertions passed. Expected table authored before runner; not an external audit.'}))
