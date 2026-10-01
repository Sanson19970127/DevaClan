"""Export current canonical content into a new folder without replacing artwork."""
import argparse
from pathlib import Path
import shutil
from content_io import load_content, write_sections

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, type=Path, help='New, empty export directory')
    parser.add_argument('--include-art', action='store_true', help='Copy current artwork unchanged')
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    output = args.output.resolve()
    if output.exists() and any(output.iterdir()):
        parser.error('Output must be empty; existing content and art will not be overwritten.')
    write_sections(load_content(root/'json'), output/'json')
    if args.include_art: shutil.copytree(root/'textures', output/'textures')
    print('Exported current content to', output)

if __name__ == '__main__': main()
