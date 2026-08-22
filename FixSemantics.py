with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml.cs', 'r', encoding='utf-8') as f:
    gc = f.read()

gc = gc.replace('group.LaunchSide', 'group.GroupLaunchSide')
gc = gc.replace('group.CompactAlign', 'group.CompactAlignment')
gc = gc.replace('group.GridCols', 'group.GridColumns')
gc = gc.replace('_group.LaunchSide', '_group.GroupLaunchSide')
gc = gc.replace('_group.CompactAlign', '_group.CompactAlignment')
gc = gc.replace('_group.GridCols', '_group.GridColumns')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml.cs', 'w', encoding='utf-8') as f:
    f.write(gc)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

# Add the missing boolean fields
pw = pw.replace('bool _makeMainFocus;', 'bool _makeMainFocus;\n    bool _disableRoundedCorners = false;\n    bool _disableFloat = false;\n    bool _popupIsDark = false;\n    bool _keepOpen = false;')

# Fix KeepOpen
pw = pw.replace('Settings.KeepOpen', 'Group.KeepOpen')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)
print('Semantic fixes applied')
