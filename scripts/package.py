from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import hashlib,json
root=Path(__file__).resolve().parents[1];out=root/'output'
def pack(target,files,base):
    with ZipFile(target,'w',ZIP_DEFLATED,compresslevel=6) as z:
        for f in files:z.write(f,f.relative_to(base))
    with ZipFile(target) as z:
        assert z.testzip() is None
        assert not any('/data/' in n or n.endswith('.db') for n in z.namelist())
    return {'name':target.name,'bytes':target.stat().st_size,'sha256':hashlib.sha256(target.read_bytes()).hexdigest()}
app=out/'Supportly-App'
app_files=[p for p in app.rglob('*') if p.is_file() and 'data' not in p.relative_to(app).parts]
source=[]
for folder in ['src','scripts','tests']:
    source += [p for p in (root/folder).rglob('*') if p.is_file() and not any(x in p.relative_to(root).parts for x in ['bin','obj','__pycache__'])]
source += [root/name for name in ['Supportly.slnx','NuGet.Config','compose.yaml','README.md','.gitignore','.dockerignore','package.json','package-lock.json','START-SUPPORTLY.cmd','STOP-SUPPORTLY.cmd']]
source += [p for p in (out/'evidence').rglob('*') if p.is_file()]
source += [out/'Bao-cao-SUPPORTLY-7-tuan.pdf',out/'Bao-cao-SUPPORTLY-7-tuan.md']
manifest=[pack(out/'Supportly-App.zip',app_files,app),pack(out/'Supportly-Source.zip',source,root)]
(out/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps(manifest,indent=2))
