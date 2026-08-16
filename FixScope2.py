with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

target = '''        else if (_isDesktopMode)
        {'''
new_target = '''        bool isTop = false, isLeft = false, isRight = false;
        else if (_isDesktopMode)
        {'''

pw = pw.replace(target, new_target)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)

print('Fixed isTop declaration')
