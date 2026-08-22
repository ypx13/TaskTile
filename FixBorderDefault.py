with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

target = 'int borderColor = popupIsDark ? 0x00333333 : 0x00E8E8E8;'
new_target = 'int borderColor = unchecked((int)0xFFFFFFFF);'

if target in pw:
    pw = pw.replace(target, new_target)
    with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
        f.write(pw)
    print('Fixed border color to DWMWA_COLOR_DEFAULT')
else:
    print('Target not found')
