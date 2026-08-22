with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

# 1. Remove from constructor
# Find:
# int borderColor = unchecked((int)0xFFFFFFFF); // Let Windows automatically color the border based on the window theme
# if (_disableRoundedCorners) borderColor = DWMWA_COLOR_NONE;
# DwmSetWindowAttribute(h, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
old_logic = '''int borderColor = unchecked((int)0xFFFFFFFF); // Let Windows automatically color the border based on the window theme
        if (_disableRoundedCorners) borderColor = DWMWA_COLOR_NONE;
        DwmSetWindowAttribute(h, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));'''

pw = pw.replace(old_logic, '')

# 2. Add to LoadAndPosition
# Find:
# _root.RequestedTheme = popupIsDark ? ElementTheme.Dark : ElementTheme.Light; int darkAttr = popupIsDark ? 1 : 0; DwmSetWindowAttribute(WindowNative.GetWindowHandle(this), 20, ref darkAttr, sizeof(int));
target = '''_root.RequestedTheme = popupIsDark ? ElementTheme.Dark : ElementTheme.Light; int darkAttr = popupIsDark ? 1 : 0; DwmSetWindowAttribute(WindowNative.GetWindowHandle(this), 20, ref darkAttr, sizeof(int));'''

new_logic = target + '''
            int borderColor = popupIsDark ? 0x00333333 : 0x00E8E8E8;
            if (_disableRoundedCorners) borderColor = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
            DwmSetWindowAttribute(WindowNative.GetWindowHandle(this), 34, ref borderColor, sizeof(int));
'''

pw = pw.replace(target, new_logic)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)

print('Border logic moved to LoadAndPosition')
