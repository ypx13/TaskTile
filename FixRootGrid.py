import re

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

pw = pw.replace('if (_disableAnimation) { RootGrid.Transitions?.Clear(); }', 'if (_disableAnimation && this.Content is FrameworkElement root) { root.Transitions?.Clear(); }')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml', 'r', encoding='utf-8') as f:
    xaml = f.read()

pattern = r'(<ToggleSwitch x:Name="MakeMainFocusToggle".*?/>)'
replacement = r'\1\n                                          <ToggleSwitch x:Name="KeepOpenToggle" Header="Keep group open when clicking outside" Toggled="SettingChanged" Margin="16,0,0,0"/>'

xaml = re.sub(pattern, replacement, xaml, flags=re.DOTALL)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml', 'w', encoding='utf-8') as f:
    f.write(xaml)

print('Fixed RootGrid and KeepOpenToggle')
