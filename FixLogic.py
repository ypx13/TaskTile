with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

# 1. Fix DisableRoundedCorners border color
pw = pw.replace('if (_disableRoundedCorners) borderColor = 0x00000000;', 'if (_disableRoundedCorners) borderColor = DWMWA_COLOR_NONE;')

# 2. KeepOpen toggle implementation
pw = pw.replace('if (!_isClosing && !_makeMainFocus)', 'if (!_isClosing && !_makeMainFocus && !_keepOpen)')

# 3. DisableAutoHide
pw = pw.replace('_taskbarTracker.Start();', 'if (!_disableAutoHide) _taskbarTracker.Start();')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)
print('Applied logic fixes')
