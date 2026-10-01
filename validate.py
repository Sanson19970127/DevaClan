from pathlib import Path
import json, sys, re
import jsonschema
from content_io import load_content
R=Path(__file__).resolve().parent
data=load_content(R/'json')
schema_root=R.parent/'trainworks-source/schemas'
schema=json.loads((schema_root/'base.json').read_text('utf-8'))
store={}
baseurl='https://raw.githubusercontent.com/Monster-Train-2-Modding-Group/Trainworks-Reloaded/refs/heads/main/schemas/'
for f in schema_root.rglob('*.json'):
    j=json.loads(f.read_text('utf-8-sig'));store[baseurl+f.relative_to(schema_root).as_posix()]=j
resolver=jsonschema.RefResolver(base_uri=baseurl+'base.json',referrer=schema,store=store)
errors=[]
for e in jsonschema.Draft7Validator(schema,resolver=resolver).iter_errors({k:v for k,v in data.items() if k!='huds'}):
    errors.append(f'{list(e.absolute_path)}: {e.message[:350]}')
allids={o['id'] for arr in data.values() for o in arr if 'id' in o}
code='\n'.join(p.read_text('utf-8') for p in (R/'src').glob('*.cs'))
classes=set(re.findall(r'class\s+(\w+)',code))
def scan(o,path=''):
    if isinstance(o,dict):
        if 'mod_reference' in o:return
        for k,v in o.items():
            if k=='path' and isinstance(v,str) and not (R/v).is_file():errors.append('Missing art '+v)
            if k in ('names','descriptions','titles','tooltip_texts','tooltip_titles','card_tooltips','character_tooltips','texts') and isinstance(v,dict) and ('english' not in v or 'chinese' not in v):errors.append('Missing translation '+path+'.'+k)
            scan(v,path+'.'+k)
    elif isinstance(o,list):
        for i,v in enumerate(o):scan(v,f'{path}[{i}]')
    elif isinstance(o,str) and o.startswith('@') and o[1:] not in allids|classes:errors.append('Unresolved '+path+': '+o)
scan(data)
for kind,items in data.items():
    ids=[i.get('id',i.get('key')) for i in items]
    if len(ids)!=len(set(ids)):errors.append('Duplicate IDs in '+kind)
for e in data['effects']:
    if e['name'] in ('@CardEffectDeva','@CardEffectDevaExorcise'):errors.append('Retired universal effect '+e['id'])
    if e['name'] in ('CardEffectAddStatusEffect','@CardEffectOtherAllyStatus'):
        assert e.get('param_int',0)==0, e['id']+' must always apply (native param_int is chance)'
for c in data['cards']:
    if not c.get('effects'):errors.append('Empty card effect '+c['id'])
assert len(data['relics'])==10
assert len([c for c in data['cards'] if c.get('card_type')=='room'])==3
assert len([c for c in data['cards'] if c.get('card_type')=='equipment'])==3
assert len(data['classes'][0]['champions'])==2
assert len([c for c in data['cards'] if c.get('pools')==['UnitsAllBanner','@DevaBannerPool']])==9
characters={c['id']:c for c in data['characters']}
tokens={'Perfect1','Perfect2','Perfect3','Giant','Firewall'}
for id in tokens:
    assert {'status':'cardless','count':1} in characters[id]['starting_status_effects'], id+' must be cardless'
assert not any(s['status']=='cardless' for s in characters['NayutaConstruct']['starting_status_effects']), 'Starter must remain a normal card'
random_ids=set(re.findall(r'"([^"\n]+)"', re.search(r'RandomConstructs\s*=\s*\{([^}]+)',code).group(1)))
assert random_ids=={'GuardConstruct','ResonanceConstruct','Idaten','Vajra','EmberConverter','AttackConstruct'}, 'Random pool must contain only six regular Constructs'
assert not random_ids.intersection(tokens|{'NayutaConstruct','Mara','Nayuta'})
effects={e['id']:e for e in data['effects']}
cards={c['id']:c for c in data['cards']}
relics={r['id']:r for r in data['relics']}
assert relics['SingingBowl']['relic_effects']==['@Marker'], 'Bowl must not attach upgrades to cards'
assert relics['SingingBowl']['descriptions']['chinese']=='一个友方角色使用主动能力后，使其获得+3/+3。'
assert not {'BowlUpgrade','BowlIncant','BowlEffect','IncantGrowth'}.intersection(allids), 'Remove obsolete global spell trigger'
assert next(s for s in data['status_effects'] if s['id']=='temporary')['display_category']=='persistent'
chant=effects['ChantEffect']
assert chant['name']=='@CardEffectOtherAllyStatus' and chant['target_mode']=='drop_target_character' and chant['target_team']=='monsters'
assert chant['param_status_effects'][0]['count']==2
assert cards['Chant']['targetless'] is False and cards['Chant']['targets_room'] is True
assert cards['Chant']['cooldown']==1 and cards['Chant']['can_ability_target_other_floors'] is False
assert {s['status'] for s in characters['Idaten']['starting_status_effects']}=={'ambush','burst'}
assert not any(s.get('status')=='haste' for s in characters['Idaten']['starting_status_effects'])
for cid in ['Perfect1','Perfect2','Perfect3','Firewall']:
    assert {'status':'@temporary','count':1} in characters[cid]['starting_status_effects']
for cid in ['Mantra','NayutaConstruct']:
    assert cards[cid]['pools']==['StarterCardsOnly'], cid+' must join the native starter pool, not the draft pool'
assert effects['AsuraArenaAttach']['target_ignore_bosses'] is True
assert effects['AsuraArenaAttach']['target_team']=='heroes'
for trigger in data['character_triggers']:
    if trigger['id'].startswith('NayutaRecreateSummon'):
        assert 'directly in front' in trigger['descriptions']['english'] and '20/20' in trigger['descriptions']['chinese']
assert characters['Giant']['size']==2 and characters['Giant']['attack_damage']==20 and characters['Giant']['health']==20
purify=cards['Purify']; assert purify['effects']==['@PurifySilence','@PurifyDamage']
assert effects['PurifySilence']['name']=='@CardEffectTurnSilence' and effects['PurifyDamage']['name']=='CardEffectDamage' and effects['PurifyDamage']['param_int']==5
assert 'effect1.power' in purify['descriptions']['english'] and 'effect1.power' in purify['descriptions']['chinese']
assert '@Permafrost' in cards['SixRealms']['traits'] and '@Retain' not in cards['SixRealms']['traits']
assert effects['ExorciseEffect']['name']=='CardEffectDamage' and effects['ExorciseCharge']['param_int']==2
assert cards['Exorcise']['triggers']==['@ExorciseSlay']
slay=next(t for t in data['card_triggers'] if t['id']=='ExorciseSlay')
assert slay['trigger']=='on_kill' and slay['effects']==['@ExorciseCharge']
assert not cards['ChargedSpear'].get('descriptions') and not cards['AsuraDrive'].get('descriptions')
assert effects['SamadhiEffect']['param_status_effects']==[{'status':'untouchable','count':1}]
assert cards['Anoint']['effects']==['@AnointEffect','@AnointConvert']
assert cards['HeavenlyWrath']['effects']==['@HeavenlyWrathEffect','@HeavenlyWrathAttack']
assert cards['AsuraSecret']['effects']==['@AsuraSecretEffect','@AsuraSecretDaze']
replacements={r['key'] for r in data.get('replacement_texts',[])} | {s['id'] for s in data['status_effects'] if s.get('replacement_texts')}
for key in re.findall(r'\[\.sanson\.DevaClan_([^\]]+)\]',json.dumps(data)):
    assert key in replacements, 'Unregistered keyword '+key
dynamic_cards={'Mantra':'effect0.power','Chant':'effect0.status0.power','SirenSong':'effect0.status0.power','Nectar':'effect0.status0.power','Exorcise':'effect0.power','Asceticism':'effect0.power','Feast':'effect0.status0.power','Anoint':'effect0.status0.power','BookOfRebirth':'effect0.status0.power','HeavenlyWrath':'effect0.status0.power','Purify':'effect1.power','SixRealms':'effect0.power'}
for cid,token in dynamic_cards.items():
    assert token in cards[cid].get('descriptions',{}).get('english',''), f'{cid} missing dynamic text token'
print('\n'.join(errors) if errors else 'PASS: schema, local references, bilingual text, placeholder files, effect handlers, content counts.')
(R/'validation.json').write_text(json.dumps({'passed':not errors,'errors':errors,'counts':{k:len(v) for k,v in data.items()}},ensure_ascii=False,indent=2),'utf-8')
sys.exit(bool(errors))
