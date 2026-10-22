"""Generate WPF brushes from the reviewed FigCraft tokens; no native-file editing."""
import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--tokens', type=Path, default=root / 'design/figcraft/theme-tokens.json')
parser.add_argument('--output', type=Path, default=root / 'src/YuzuhaToolkit.Desktop/Themes')
args = parser.parse_args()
tokens = json.loads(args.tokens.read_text(encoding='utf-8'))
brand = json.loads((root / 'design/figcraft/brand-fixed.json').read_text(encoding='utf-8'))
ns = 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'
x = 'http://schemas.microsoft.com/winfx/2006/xaml'
ET.register_namespace('', ns)
ET.register_namespace('x', x)
args.output.mkdir(parents=True, exist_ok=True)
for mode, index in [('Light', 1), ('Dark', 2)]:
    resource = ET.Element('{' + ns + '}ResourceDictionary')
    for token in tokens['variables']:
        value = token['values']['VariableCollectionId:1/mode' + str(index)]['value']
        color = '#' + ''.join(f'{round(value[channel] * 255):02X}' for channel in ['r', 'g', 'b'])
        props = {'{' + x + '}Key': token['name'].replace('/', '.'), 'Color': color}
        if value.get('a', 1) < 1:
            props['Opacity'] = str(value['a'])
        ET.SubElement(resource, '{' + ns + '}SolidColorBrush', props)
    for key, color in brand.items():
        ET.SubElement(resource, '{' + ns + '}SolidColorBrush', {'{' + x + '}Key': key, 'Color': color})
    ET.SubElement(resource, '{' + ns + '}SolidColorBrush', {'{' + x + '}Key': 'Accent', 'Color': '#2563EB'})
    gradient = ET.SubElement(resource, '{' + ns + '}LinearGradientBrush', {'{' + x + '}Key': 'Brand.HeroGradient', 'StartPoint': '0,0', 'EndPoint': '0,1'})
    ET.SubElement(gradient, '{' + ns + '}GradientStop', {'Color': '#071426', 'Offset': '0'})
    ET.SubElement(gradient, '{' + ns + '}GradientStop', {'Color': '#153A66', 'Offset': '1'})
    ET.indent(resource, space='  ')
    (args.output / (mode + '.xaml')).write_text(ET.tostring(resource, encoding='unicode') + '\n', encoding='utf-8')
print('Generated Light/Dark WPF resources from reviewed FigCraft tokens.')
