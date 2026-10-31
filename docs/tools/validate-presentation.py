import re, json, hashlib, csv, xml.etree.ElementTree as ET
from pathlib import Path
from urllib.parse import unquote
from PIL import Image, ImageOps, ImageDraw
root=Path.cwd()
documents=[root/'README.md',*[root/'docs'/f for f in ['DEVELOPMENT.md','API_GUIDE.md','VISUAL_ATLAS.md','PORTFOLIO_PRESENTATION_REPORT.md']]]
def slug(s):
    s=re.sub(r'<[^>]*>','',s).lower()
    s=re.sub(r'[^\w\- ]','',s,flags=re.UNICODE)
    return s.replace(' ','-')
def anchors(p):
    result=set(); counts={}
    for h in re.findall(r'^#{1,6}\s+(.+?)\s*$',p.read_text(encoding='utf-8-sig'),re.M):
        key=slug(h); n=counts.get(key,0); counts[key]=n+1
        result.add(key+(f'-{n}' if n else ''))
    return result
refs=[]; broken=[]; image_count=0
for p in documents:
    content=p.read_text(encoding='utf-8-sig')
    targets=re.findall(r'!?\[[^\]]*\]\(([^\s)]+)(?:\s+"[^"]*")?\)',content)
    targets+=re.findall(r'(?:src|srcset|href)="([^" ]+)"',content)
    image_count+=len(re.findall(r'!\[|<img ',content))
    for target in targets:
        if re.match(r'^[a-zA-Z]+:',target):continue
        rel,_,anchor=unquote(target).partition('#')
        dest=(p.parent/rel).resolve() if rel else p
        if not dest.exists():broken.append(f'{p.name}: missing {target}')
        elif anchor and dest.suffix.lower()=='.md' and anchor not in anchors(dest):broken.append(f'{p.name}: missing anchor {target}')
        refs.append(target)
svgs=[]
for p in (root/'docs/assets/graphics').glob('*.svg'):
    tree=ET.parse(p); el=tree.getroot(); width=int(el.get('width'));height=int(el.get('height'))
    assert el.tag=='{http://www.w3.org/2000/svg}svg'
    assert not re.search(r'<script|<foreignObject|https?://(?!www.w3.org/2000/svg)',p.read_text(),re.I)
    with Image.open(p.with_suffix('.png')) as im:
        assert im.size==(width,height),(p,im.size,(width,height))
        im.verify()
    svgs.append({'name':p.name,'width':width,'height':height})
shots=[]
for p in (root/'docs/assets/screenshots').glob('*.jpg'):
    with Image.open(p) as im: im.verify()
    with Image.open(p) as im: shots.append({'name':p.name,'dimensions':list(im.size),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
source=[]
with (root/'verification/runtime-closure/verified-source-sha256.csv').open(encoding='utf-8-sig',newline='') as f:
    for row in csv.DictReader(f):
        p=root/row['Path']; actual=hashlib.sha256(p.read_bytes()).hexdigest().upper() if p.exists() else None
        if actual!=row['SHA256']:source.append(row['Path'])
before=json.loads((root/'docs/assets/verification-before.json').read_text(encoding='utf-8-sig'))
changed=[r['Path'] for r in before if hashlib.sha256((root/r['Path']).read_bytes()).hexdigest().upper()!=r['SHA256']]
assert not broken,broken
assert not source,source
assert not changed,changed
assert len(svgs)==11 and len(shots)==2
files=sorted((root/'docs/assets/graphics').glob('*.png'))
files=[f for f in files if f.name!='visual-review.png']
canvas=Image.new('RGB',(1600,450*((len(files)+1)//2)),'#dce5ed'); draw=ImageDraw.Draw(canvas)
for i,p in enumerate(files):
    im=Image.open(p).convert('RGB'); im.thumbnail((780,410));xy=((i%2)*800+10,(i//2)*450+30)
    canvas.paste(im,xy);draw.text(((i%2)*800+10,(i//2)*450+8),p.stem,fill='black')
canvas.save(root/'docs/assets/graphics/visual-review.png')
result={'status':'passed','markdownDocumentsChecked':len(documents),'localReferencesChecked':len(refs),'brokenReferences':broken,'svgAssets':svgs,'screenshots':shots,'sourceSnapshotEntriesChecked':sum(1 for _ in csv.DictReader((root/'verification/runtime-closure/verified-source-sha256.csv').open(encoding='utf-8-sig'))),'sourceDifferences':source,'historicalEvidenceFilesChecked':len(before),'historicalEvidenceDifferences':changed,'testsRerun':False}
(root/'docs/assets/presentation-validation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['svgAssets','screenshots']},indent=2))
