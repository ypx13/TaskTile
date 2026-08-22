with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

pw = pw.replace('bool _isClosing = false;\n        if (_disableAnimation) { RootGrid.Transitions?.Clear(); }', 'bool _isClosing = false;')

# Find LoadAndPosition and put the disableAnimation logic there
# In LoadAndPosition, there's RootGrid.Transitions = new TransitionCollection { new EntranceThemeTransition() }; maybe?
# I'll just append it to the end of LoadAndPosition
# But actually, I can just replace if (!_isClosing && !_makeMainFocus && !_keepOpen) -> wait, that was Deactivated.

# I will just put the disableAnimation logic in LoadAndPosition
with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)

print('Fixed field declaration')
