with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

# 1. Fix CreateRoundRectRgn bounds
pw = pw.replace('physW + 1', 'physW')
pw = pw.replace('physH + 1', 'physH')

# 2. Fix the animation issue
target_anim = '''          // Strip scale animation if Desktop Mode is active to prevent visual edge clipping
          if (_isDesktopMode && _popIn.Children.Count > 1)
          {
              _popIn.Children.RemoveAt(1); // remove the ScaleY animation
              _rootScale.ScaleY = 1;
          }'''

new_anim = '''          // Strip translate animation to prevent visual edge clipping (black window showing behind)
          if (_popIn.Children.Count > 1)
          {
              _popIn.Children.RemoveAt(1); // remove the TranslateY animation
              _rootScale.ScaleY = 1;
          }'''

if target_anim in pw:
    pw = pw.replace(target_anim, new_anim)
    print('Fixed animation issue')
else:
    print('Target anim not found')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)
print('Fixed PopupWindow.cs')
