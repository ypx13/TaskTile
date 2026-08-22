with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

# Disable Float gap
pw = pw.replace('int gap = 8;', 'int gap = _disableFloat ? 0 : 8;')
pw = pw.replace('int gap = 8; // distance from taskbar', 'int gap = _disableFloat ? 0 : 8; // distance from taskbar')

# Disable float sharp corners
pw = pw.replace('int cornerPref = DWMWCP_ROUND;', 'int cornerPref = _disableRoundedCorners ? 1 : (_disableFloat ? 1 : DWMWCP_ROUND);')

# Disable animation
pw = pw.replace('_isClosing = false;', '_isClosing = false;\n        if (_disableAnimation) { RootGrid.Transitions?.Clear(); }')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)
print('Applied more logic fixes')
