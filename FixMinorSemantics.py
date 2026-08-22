with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml.cs', 'r', encoding='utf-8') as f:
    gc = f.read()

gc = gc.replace('RemoveGroup(_group)', 'RemoveGroup(_group.Id)')
gc = gc.replace('CompactAlignmentment', 'CompactAlignment')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml.cs', 'w', encoding='utf-8') as f:
    f.write(gc)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

# PopupWindow.cs(592,36): error CS1061: 'AppSettings' does not contain a definition for 'KeepOpen'
# Let's see what is there exactly
lines = pw.split('\n')
for i, line in enumerate(lines):
    if 'KeepOpen' in line and 'Settings.' in line:
        lines[i] = line.replace('Settings.KeepOpen', 'group.KeepOpen')
    elif 'KeepOpen' in line and 'App.Settings.' in line:
        lines[i] = line.replace('App.Settings.KeepOpen', 'group.KeepOpen')
    elif 'KeepOpen' in line and 'AppSettings.' in line:
        lines[i] = line.replace('AppSettings.KeepOpen', 'group.KeepOpen')

pw = '\n'.join(lines)
pw = pw.replace('AppSettings.KeepOpen', 'Group.KeepOpen')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)

print('Minor semantic errors fixed.')
