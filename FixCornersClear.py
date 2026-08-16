with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

target = '''if (hRgn != IntPtr.Zero) {
                SetWindowRgn(hwnd, hRgn, true);
            }
        }'''

new_target = '''if (hRgn != IntPtr.Zero) {
                SetWindowRgn(hwnd, hRgn, true);
            }
        } else {
            SetWindowRgn(hwnd, IntPtr.Zero, true);
        }'''

pw = pw.replace(target, new_target)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)

print('Added clear region logic')
