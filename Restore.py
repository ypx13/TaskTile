import json
import re

log_path = r'C:\Users\yassinh\.gemini\antigravity\brain\c51732c7-c76f-40e9-9fb1-e68164f251b0\.system_generated\logs\transcript.jsonl'
best_content = ''
best_len = 0

with open(log_path, 'r', encoding='utf-8') as f:
    for line in f:
        if 'GroupCard : UserControl' in line:
            data = json.loads(line)
            content = data.get('content', '')
            
            # The file might be in a diff or view_file output.
            # We just want to see if we can find a large chunk.
            if len(content) > best_len:
                # If it's a diff, we can't just take the content.
                # If it's a VIEW_FILE, it has 'Showing lines ...'
                if 'Showing lines' in content:
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
                else:
                    # Might be a REPLACE_FILE_CONTENT diff or similar
                    pass

if best_len > 0:
    with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml.cs', 'w', encoding='utf-8') as f:
        f.write(best_content)
    print(f'Extracted {best_len} lines.')
else:
    print('Not found')
