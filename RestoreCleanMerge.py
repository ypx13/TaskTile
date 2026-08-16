import json
import re

log_path = r'C:\Users\yassinh\.gemini\antigravity\brain\c51732c7-c76f-40e9-9fb1-e68164f251b0\.system_generated\logs\transcript.jsonl'
lines_dict = {}

with open(log_path, 'r', encoding='utf-8') as f:
    for line in f:
        if 'GroupCard.xaml.cs' in line and 'VIEW_FILE' in line:
            data = json.loads(line)
            content = data.get('content', '')
            
            if 'Showing lines' in content and 'KeepOpen_Toggled' not in content:
                content_lines = content.split('\n')
                capturing = False
                for l in content_lines:
                    if l.startswith('Showing lines'):
                        capturing = True
                        continue
                    if l.startswith('The above content does NOT show'):
                        capturing = False
                        continue
                    if capturing:
                        match = re.match(r'^(\d+):\s(.*)', l)
                        if match:
                            line_num = int(match.group(1))
                            line_text = match.group(2)
                            lines_dict[line_num] = line_text

if lines_dict:
    max_line = max(lines_dict.keys())
    output = []
    for i in range(1, max_line + 1):
        output.append(lines_dict.get(i, f'// MISSING LINE {i}'))
    
    with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml.cs', 'w', encoding='utf-8') as out:
        out.write('\n'.join(output))
    
    print(f'Reconstructed file up to line {max_line}. Missing {sum(1 for i in range(1, max_line+1) if i not in lines_dict)} lines.')
else:
    print('Not found')
