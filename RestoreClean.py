import json
import re

log_path = r'C:\Users\yassinh\.gemini\antigravity\brain\c51732c7-c76f-40e9-9fb1-e68164f251b0\.system_generated\logs\transcript.jsonl'
best_content = ''
best_len = 0

with open(log_path, 'r', encoding='utf-8') as f:
    for line in f:
        if 'GroupCard.xaml.cs' in line and 'VIEW_FILE' in line:
            data = json.loads(line)
            content = data.get('content', '')
            
            if 'Showing lines' in content and 'KeepOpen_Toggled' not in content:
                lines = content.split('\n')
                extracted = []
                capturing = False
                for l in lines:
                    if l.startswith('Showing lines'):
                        capturing = True
                        continue
                    if l.startswith('The above content does NOT show'):
                        capturing = False
                        break
                    if capturing:
                        match = re.match(r'^\d+:\s(.*)', l)
                        if match:
                            extracted.append(match.group(1))
                
                if len(extracted) > best_len:
                    best_len = len(extracted)
                    best_content = '\n'.join(extracted)

if best_len > 0:
    with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml.cs', 'w', encoding='utf-8') as f:
        f.write(best_content)
    print(f'Extracted {best_len} lines.')
else:
    print('Not found')
