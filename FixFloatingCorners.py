with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

target = '''        else
        {
            // Respect explicit LaunchSide from settings
            var launchSide = finalLaunchSide;'''

new_target = '''        else
        {
            if (!_disableRoundedCorners)
            {
                double scale = GetDpiForWindow(hwnd) / 96.0;
                int r = (int)(8 * scale);
                IntPtr hRgn = CreateRoundRectRgn(0, 0, physW + 1, physH + 1, r * 2, r * 2);
                if (hRgn != IntPtr.Zero) SetWindowRgn(hwnd, hRgn, true);
            }
            else
            {
                SetWindowRgn(hwnd, IntPtr.Zero, true);
            }

            // Respect explicit LaunchSide from settings
            var launchSide = finalLaunchSide;'''

if target in pw:
    pw = pw.replace(target, new_target)
    
    # We also need to remove the SetWindowRgn(hwnd, IntPtr.Zero, true); from the end of the else block.
    target2 = '''            if (hRgn != IntPtr.Zero) {
                SetWindowRgn(hwnd, hRgn, true);
            }
        } else {
            SetWindowRgn(hwnd, IntPtr.Zero, true);
        }'''
    
    new_target2 = '''            if (hRgn != IntPtr.Zero) {
                SetWindowRgn(hwnd, hRgn, true);
            }
        }'''
    
    pw = pw.replace(target2, new_target2)

    with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
        f.write(pw)
    print('Added SetWindowRgn for floating mode')
else:
    print('Target not found')
