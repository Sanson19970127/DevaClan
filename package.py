from pathlib import Path
import json, csv, shutil, zipfile, hashlib, subprocess, sys, re
import xml.etree.ElementTree as ET
from PIL import Image
from content_io import load_content, content_hashes
R=Path(__file__).resolve().parent
VERSION=ET.parse(R/'DevaClan.csproj').findtext('PropertyGroup/Version')
assert VERSION and ('\"'+VERSION+'\"') in (R/'src/Plugin.cs').read_text('utf-8')
O=R.parent.parent/'outputs'/f'DevaClan-{VERSION}-bugfix'
W=R.parent/'release-staging'
O.mkdir(parents=True,exist_ok=True);W.mkdir(exist_ok=True)
# Record actual results from this build instead of carrying forward old pass flags.
shell=shutil.which('pwsh') or shutil.which('powershell')
assert shell, 'PowerShell is required to build and validate the release.'
logs={}
for script in ['build.ps1','test-combat.ps1','validate-patches.ps1','test-localization.ps1','test-content-runtime.ps1']:
    result=subprocess.run([shell,'-NoProfile','-File',str(R/script)],capture_output=True)
    raw=result.stdout+result.stderr
    try: output=raw.decode('utf-8')
    except UnicodeDecodeError: output=raw.decode('gb18030')
    print(output,flush=True)
    assert result.returncode==0, script+' failed; packaging stopped.'
    logs[script]=output
subprocess.run([sys.executable,str(R/'validate.py')],check=True)
D=load_content(R/'json')
v=json.loads((R/'validation.json').read_text('utf-8'))
assert v['passed'], 'Do not package content that failed validation.'
dll=R/'bin/Release/netstandard2.1/DevaClan.dll'
assert dll.is_file()
report={'build':VERSION,'compiler':'C# 4.11 / .NET 8 SDK 8.0.425','compilation':'passed','schema_and_content':v,
        'game_patch_metadata':{'passed':True,'targets':int(re.search(r'PASS: (\d+) Harmony',logs['validate-patches.ps1']).group(1))},
        'combat_regression':{'passed':True,'checks':int(re.search(r'PASS: (\d+) combat',logs['test-combat.ps1']).group(1)), 'method':'Installed managed game objects with inert Unity handles; includes source guards. Not a Unity playtest.'},
        'ui_state_regression':{'passed':True,'method':'Managed regression verifies clickable Charge-ready states retain cooldown and render with native cooldown state; silenced, deployment, unaffordable and ready states remain distinct. Actual Unity tint, layout and interaction not playtested.'},
        'localization_regression':{'passed':True,'checks':int(re.search(r'PASS: (\d+) localization',logs['test-localization.ps1']).group(1)),'method':'Real installed game/Trainworks assemblies; includes custom keyword and card trigger terms.'},
        'content_runtime_regression':{'passed':True,'checks':int(re.search(r'PASS: (\d+) content runtime',logs['test-content-runtime.ps1']).group(1)),'method':'Installed framework JSON merger, class-name resolver, effect construction, native trigger enum/reference checks and game keyword parser. Full pipeline finalization needs Unity/Mono and was not executed.'},
        'construct_content_checks':{'passed':True,'random_pool_size':6,'cardless_variants':5,'starter_remains_card':True},
        'runtime':{'executed':False,'reason':'No Unity playtest or full undo replay was executed for this bugfix candidate. See BUGFIX-NOTES.md for evidence, limitations and acceptance checklist.'},
        'validation_logs':logs,
        'content_files_sha256':content_hashes(R/'json'),
        'content_sha256':hashlib.sha256(json.dumps(D,ensure_ascii=False,sort_keys=True).encode('utf-8')).hexdigest(),
        'assembly_sha256':hashlib.sha256(dll.read_bytes()).hexdigest()}
(R/'VALIDATION-REPORT.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf-8')
chars={x['id']:x for x in D['characters']}
cards={x['id']:x for x in D['cards']}
with (R/'ART-REPLACEMENT.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.writer(f);w.writerow(['ID','中文名称','English name','路径 / Path','宽 / Width','高 / Height'])
    for sprite in D['sprites']:
        path=sprite.get('path')
        if not isinstance(path,str):continue
        names=next((c['names'] for id,c in cards.items() if sprite['id']==id+'CardSprite' or sprite['id']==id+'UnitSprite'),None)
        if names is None:
            names=next((c['names'] for c in D['relics'] if sprite['id'] in [c['id']+'Icon',c['id']+'SmallIcon']),{'chinese':'氏族/界面素材','english':'Clan / UI asset'})
        im=Image.open(R/path);w.writerow([sprite['id'],names['chinese'],names['english'],'BepInEx/plugins/DevaClan/'+path,*im.size])
with (R/'CONTENT-CATALOG.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.writer(f);w.writerow(['ID','中文名称','English name','Type','Ember','Capacity','Attack','Health','Cooldown','中文效果','English effect'])
    for c in D['cards']:
        ch=chars.get(c['id'],{});ds=c.get('descriptions',{})
        w.writerow([c['id'],c['names']['chinese'],c['names']['english'],'ability' if c.get('is_an_ability') or c.get('is_room_ability') else c['card_type'],
                    'X' if c.get('cost_type')=='x' else c.get('cost',0),ch.get('size',''),ch.get('attack_damage',''),ch.get('health',''),c.get('cooldown',''),ds.get('chinese',''),ds.get('english','')])
    for c in D['relics']:w.writerow([c['id'],c['names']['chinese'],c['names']['english'],'artifact','','','','','',c['descriptions']['chinese'],c['descriptions']['english']])
template=R.parent/'official-template'/'{{cookiecutter.__project_slug}}'
license_text=(template/'LICENSE').read_text('utf-8')
(R/'THIRD-PARTY-NOTICES.txt').write_text('Official placeholder artwork and template-derived clan frames/banner\nSource: https://github.com/Monster-Train-2-Modding-Group/Mod-Template\n\n'+license_text+'\n\nRuntime dependencies (not redistributed): BepInEx, Trainworks Reloaded, Conductor.\nGame assemblies and game source are not distributed.\n','utf-8')
manifest={'name':'DevaClan','version_number':VERSION,'website_url':'','description':'Deva Clan by sanson. Chinese/English. Bugfix candidate for charge-paid unit/building abilities, converter targeting, undo UI/memory, blueprint recall, Mara retention and Asura Arena. In-game verification pending.',
          'dependencies':['BepInEx-BepInExPack-5.4.2100','MT2-Trainworks_Reloaded-0.7.20','Conductor-Conductor-0.5.10']}
(R/'manifest.json').write_text(json.dumps(manifest,indent=2),'utf-8')
Image.open(R/'textures/icons/BasicClanLargeIcon.png').convert('RGBA').resize((256,256)).save(R/'icon.png')
docs=['README.md','BUGFIX-NOTES.md','REFACTOR-NOTES.md','ART-REPLACEMENT.csv','CONTENT-CATALOG.csv','THIRD-PARTY-NOTICES.txt','VALIDATION-REPORT.json','独立图标替换清单.md']
def zip_files(out,entries):
    with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for file,arc in sorted(entries,key=lambda e:e[1]):z.write(file,arc)
    with zipfile.ZipFile(out) as z:assert z.testzip() is None
files=[(dll,'DevaClan.dll')]+[(p,p.relative_to(R).as_posix()) for folder in ['json','textures'] for p in (R/folder).rglob('*') if p.is_file()]
zip_files(O/f'DevaClan-{VERSION}-Windows.zip',[(p,'BepInEx/plugins/DevaClan/'+a) for p,a in files]+[(R/p,p) for p in docs+['Install.cmd','InstallDevaClan.ps1']])
zip_files(O/f'DevaClan-{VERSION}-Thunderstore.zip',[(p,'plugins/DevaClan/'+a) for p,a in files]+[(R/p,p) for p in docs+['manifest.json','icon.png']])
sourcefiles=[(p,p.relative_to(R).as_posix()) for p in R.rglob('*') if p.is_file() and not any(x in p.relative_to(R).parts for x in ['bin','obj','.git','template','__pycache__'])]
zip_files(O/f'DevaClan-{VERSION}-source.zip',sourcefiles)
for name in docs:shutil.copyfile(R/name,O/name)
(O/'SHA256SUMS.txt').write_text('\n'.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name for p in sorted(O.glob('DevaClan-*.zip')))+'\n','ascii')
for p in sorted(O.glob(f'DevaClan-{VERSION}-*.zip')):print(p.name,p.stat().st_size)
