with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

# Fix scale name collision
pw = pw.replace('double scale = GetDpiForWindow(hwnd) / 96.0;', 'double dpiScale = GetDpiForWindow(hwnd) / 96.0;')
pw = pw.replace('int radius = (int)(8 * scale);', 'int radius = (int)(8 * dpiScale);')

# Move isTop out
old_scope = '''        if (_isDesktopMode)
        {'''

new_scope = '''        bool isTop = false, isLeft = false, isRight = false;
        if (_isDesktopMode)
        {'''

pw = pw.replace(old_scope, new_scope)

# Remove old declaration
old_decl = '''            // Respect explicit LaunchSide from settings
            var launchSide = finalLaunchSide;

            bool isTop = false, isLeft = false, isRight = false;'''

new_decl = '''            // Respect explicit LaunchSide from settings
            var launchSide = finalLaunchSide;'''

pw = pw.replace(old_decl, new_decl)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)

print('Fixed scope errors')
