with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml', 'r', encoding='utf-8') as f:
    xaml = f.read()

# Add KeepOpenToggle
xaml = xaml.replace('<ToggleSwitch x:Name="MakeMainFocusToggle" Header="Make main focus" Toggled="SettingChanged" Margin="16,0,0,0"/>', '<ToggleSwitch x:Name="MakeMainFocusToggle" Header="Make main focus" Toggled="SettingChanged" Margin="16,0,0,0"/>\n                                          <ToggleSwitch x:Name="KeepOpenToggle" Header="Keep group open when clicking outside" Toggled="SettingChanged" Margin="16,0,0,0"/>')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml', 'w', encoding='utf-8') as f:
    f.write(xaml)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml.cs', 'r', encoding='utf-8') as f:
    cs = f.read()

# Add KeepOpen to cs Populate
cs = cs.replace('MakeMainFocusToggle.IsOn = group.MakeMainFocus;', 'MakeMainFocusToggle.IsOn = group.MakeMainFocus;\n        KeepOpenToggle.IsOn = group.KeepOpen;')

# Add KeepOpen to SettingChanged
cs = cs.replace('_group.MakeMainFocus = MakeMainFocusToggle.IsOn;', '_group.MakeMainFocus = MakeMainFocusToggle.IsOn;\n        _group.KeepOpen = KeepOpenToggle.IsOn;')

# Handle Visibility logic for MakeMainFocusToggle
cs = cs.replace('GroupService.Instance.Save();', '''GroupService.Instance.Save();
        
        // UI logic
        bool isClassic = _group.PopupStyle == 0;
        bool isModern = _group.PopupStyle == 2;
        MakeMainFocusToggle.Visibility = (isClassic || isModern) ? Visibility.Visible : Visibility.Collapsed;
        LaunchPositionCombo.Visibility = (isClassic || isModern) ? Visibility.Visible : Visibility.Collapsed;
''')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml.cs', 'w', encoding='utf-8') as f:
    f.write(cs)

print('GroupCard toggles added and visibility logic applied')
