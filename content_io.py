"""Read the same JSON tree as Plugin.Awake; reject ambiguous definitions early."""
from pathlib import Path
import hashlib
import json


def load_content(root=None):
    root = Path(root) if root is not None else Path(__file__).resolve().parent / 'json'
    files = sorted(root.rglob('*.json'), key=lambda p: p.relative_to(root).as_posix())
    if not files:
        raise ValueError(f'No content JSON files in {root}')
    result, sources = {}, {}
    for path in files:
        document = json.loads(path.read_text(encoding='utf-8-sig'))
        if not isinstance(document, dict):
            raise ValueError(f'{path}: expected a JSON object')
        for section, items in document.items():
            if not isinstance(items, list):
                raise ValueError(f'{path}: {section} must be an array')
            for item in items:
                identity = item.get('id', item.get('key')) if isinstance(item, dict) else None
                if not isinstance(identity, str) or not identity:
                    raise ValueError(f'{path}: {section} item needs an id or key')
                pair = (section, identity)
                if pair in sources:
                    raise ValueError(f'Duplicate {section}/{identity}: {sources[pair]} and {path}')
                sources[pair] = path
                result.setdefault(section, []).append(item)
    return result


def content_hashes(root=None):
    root = Path(root) if root is not None else Path(__file__).resolve().parent / 'json'
    return {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(root.rglob('*.json'))}


def write_sections(data, root):
    """Write section files into a new tree; never silently erase old content."""
    root = Path(root)
    if root.exists() and any(root.rglob('*.json')):
        raise ValueError(f'Refusing to overwrite populated content tree: {root}')
    root.mkdir(parents=True, exist_ok=True)
    for section, items in data.items():
        (root / (section + '.json')).write_text(
            json.dumps({section: items}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
