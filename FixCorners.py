import re

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

# Add PInvoke declarations
if 'CreateRoundRectRgn' not in pw:
    pinvoke_target = 'static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, string lpIconPath, out ushort lpiIcon);'
    pinvoke_new = pinvoke_target + '''
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);
        [DllImport("user32.dll")]
        static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);
'''
    pw = pw.replace(pinvoke_target, pinvoke_new)

# Add logic after MoveAndResize
move_target = 'AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, physW, physH));'
move_new = move_target + '''

        // Clip HWND corners to match XAML if docked (disableFloat) and not disableRoundedCorners
        if (_disableFloat && !_disableRoundedCorners) {
            double scale = GetDpiForWindow(hwnd) / 96.0;
            int radius = (int)(8 * scale);
            int ellipse = radius * 2;
            IntPtr hRgn = IntPtr.Zero;
            
            if (isTop) {
                hRgn = CreateRoundRectRgn(0, -ellipse, physW + 1, physH + 1, ellipse, ellipse);
            } else if (isLeft) {
                hRgn = CreateRoundRectRgn(-ellipse, 0, physW + 1, physH + 1, ellipse, ellipse);
            } else if (isRight) {
                hRgn = CreateRoundRectRgn(0, 0, physW + ellipse, physH + 1, ellipse, ellipse);
            } else { // Bottom taskbar
                hRgn = CreateRoundRectRgn(0, 0, physW + 1, physH + ellipse, ellipse, ellipse);
            }
            if (hRgn != IntPtr.Zero) {
                SetWindowRgn(hwnd, hRgn, true);
            }
        }
'''
pw = pw.replace(move_target, move_new)

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)

print('Added SetWindowRgn logic')
