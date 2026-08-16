with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

target = '''        int x, y;
        if (launchAtCenter && popupStyle != 1 && popupStyle != 3) // Disallow for List/Compact
        {
            x = work.X + work.Width  / 2 - physW / 2;
            y = work.Y + work.Height / 2 - physH / 2;
        }
        bool isTop = false, isLeft = false, isRight = false;
        else if (_isDesktopMode)'''

new_target = '''        int x, y;
        bool isTop = false, isLeft = false, isRight = false;
        if (launchAtCenter && popupStyle != 1 && popupStyle != 3) // Disallow for List/Compact
        {
            x = work.X + work.Width  / 2 - physW / 2;
            y = work.Y + work.Height / 2 - physH / 2;
        }
        else if (_isDesktopMode)'''

pw = pw.replace(target, new_target)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)

print('Fixed isTop scope')
