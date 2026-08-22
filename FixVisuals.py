with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

# Fix policy (don't disable rounding if _disableFloat)
pw = pw.replace('int policy = (_disableRoundedCorners || _disableFloat) ? 1 : DWMWCP_ROUND;', 'int policy = _disableRoundedCorners ? 1 : DWMWCP_ROUND;')

# Fix border color to use DWM Default (0xFFFFFFFF)
pw = pw.replace('int borderColor = _popupIsDark ? 0x00202020 : 0x00E8E8E8;', 'int borderColor = unchecked((int)0xFFFFFFFF); // Let Windows automatically color the border based on the window theme')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)

print('Fixed policy and border color')
