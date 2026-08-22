with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

target = '''                double scale = GetDpiForWindow(hwnd) / 96.0;
                int r = (int)(8 * scale);'''

new_target = '''                double dpiScale = GetDpiForWindow(hwnd) / 96.0;
                int r = (int)(8 * dpiScale);'''

pw = pw.replace(target, new_target)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)
print('Fixed scale variable collision')
