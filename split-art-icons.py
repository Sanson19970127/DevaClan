"""Split shared icon references without regenerating other content or artwork."""
from pathlib import Path
import json
import shutil
from content_io import load_content

ROOT = Path(__file__).resolve().parent

def main():
    data = load_content(ROOT / 'json')
    sprites = {s['id']: s for s in data['sprites']}

    def split(sprite_id, source):
        target = f'textures/icons/{sprite_id}.png'
        if not (ROOT / target).exists():
            shutil.copyfile(ROOT / source, ROOT / target)
        if sprite_id not in sprites:
            sprites[sprite_id] = {'id': sprite_id}
            data['sprites'].append(sprites[sprite_id])
        sprites[sprite_id]['path'] = target

    for hero in ('Mara', 'Nayuta'):
        for suffix in ('Icon', 'LockedIcon', 'Portrait'):
            sid = hero + suffix
            split(sid, sprites[sid]['path'])
    statuses = {s['id']: s for s in data['status_effects']}
    # Capture each existing image before redirecting references.
    temporary_source = sprites[statuses['temporary']['icon'][1:]]['path']
    split('KarmaIcon', sprites['KarmaIcon']['path'])
    split('TemporaryIcon', temporary_source)
    statuses['temporary']['icon'] = '@TemporaryIcon'
    statuses['karmergy']['icon'] = '@KarmaIcon'
    upgrades = {u['id']: u for u in data['upgrades']}
    for name in ('AsuraArena', 'MeditationChamber', 'AccelerationMatrix',
                 'ChargedSpear', 'SoulBackup', 'AsuraDrive'):
        upgrade = upgrades[name + 'Upgrade']
        split(name + 'Icon', sprites[upgrade['icon'][1:]]['path'])
        upgrade['icon'] = '@' + name + 'Icon'
    # Old files remain available; remove unused sprite registrations from the catalog.
    data['sprites'] = [s for s in data['sprites'] if s['id'] not in ('RoomIcon', 'EquipmentIcon')]
    for section in ('sprites', 'status_effects', 'upgrades'):
        path = ROOT / 'json' / (section + '.json')
        path.write_text(json.dumps({section: data[section]}, ensure_ascii=False, indent=2) + '\n', 'utf-8')
    print('Split 14 image files; clan small icon retains its own existing file.')

if __name__ == '__main__':
    main()
