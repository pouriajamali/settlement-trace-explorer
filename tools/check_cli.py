"""Black-box published CLI and HTML readback checks from fresh directories."""
from html.parser import HTMLParser
import hashlib
import json
import platform
from pathlib import Path
import subprocess
import tempfile

root=Path(__file__).resolve().parents[1]
art=root/'artifacts'; dll=art/'package/TraceCli.dll'
class ResultParser(HTMLParser):
    def __init__(self):super().__init__(convert_charrefs=True);self.active=False;self.parts=[]
    def handle_starttag(self,tag,attrs):
        if tag=='pre' and dict(attrs).get('id')=='runner-result':self.active=True
    def handle_endtag(self,tag):
        if tag=='pre':self.active=False
    def handle_data(self,data):
        if self.active:self.parts.append(data)

checks=0
def check(ok):
    global checks
    assert ok
    checks+=1
with tempfile.TemporaryDirectory(prefix='cli-check-',dir=art) as temp:
    cwd=Path(temp)
    def run(*args):return subprocess.run(['dotnet',str(dll),*args],cwd=cwd,capture_output=True,text=True,timeout=30)
    output=cwd/'viewer.html'
    a=run('replay',str(art/'demo.json'),'--html',str(output))
    check(a.returncode==0)
    cli=json.loads(a.stdout)
    check(cli==json.loads((art/'demo-result.json').read_text(encoding='utf8')))
    parser=ResultParser(); parser.feed(output.read_text(encoding='utf-8-sig'))
    check(json.loads(''.join(parser.parts))==cli)
    check(cli['route']=='completed' and cli['service']=='reportedFailed')
    before=output.read_bytes()
    check(run('replay',str(art/'demo.json'),'--html',str(output)).returncode==3)
    check(output.read_bytes()==before)
    check(run('replay',str(cwd/'missing.json')).returncode==3)
    bad=cwd/'bad.json';bad.write_text('{',encoding='utf8')
    b=run('replay',str(bad));check(b.returncode==2 and json.loads(b.stderr)['error']=='INPUT_INVALID')
    big=cwd/'big.json';big.write_bytes(b' '*(1024*1024+1))
    b=run('replay',str(big));check(b.returncode==2 and json.loads(b.stderr)['error']=='INPUT_LIMIT')
    check(run().returncode==2)
manifest={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(dll.parent.iterdir()) if p.is_file()}
report=dict(schema='g2-cli-check-v1',checks=checks,failed=0,freshCwd=True,packageSha256=manifest,
            host=platform.platform(),dotnetVersion=subprocess.check_output(['dotnet','--version'],cwd=root,text=True).strip(),
            limits='Framework-dependent net10.0 DLL on the host recorded here; no live service, financial effect or verifier validation.')
(art/'cli-verification.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf8')
print(json.dumps(report,indent=2))
