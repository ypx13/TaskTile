with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

new_lines = []
removed = False
for i, line in enumerate(lines):
    if i >= 1050 and i <= 1060 and line.strip() == '}' and not removed:
        # Just check the surrounding context to be safe
        if lines[i-1].strip() == '}' and lines[i-2].strip() == '}':
            removed = True
            continue
    new_lines.append(line)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.writelines(new_lines)
print('Removed line' if removed else 'Not removed')
