with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

target = '''        // Strip scale animation if Desktop Mode is active to prevent visual edge clipping
        if (_isDesktopMode && _popIn.Children.Count > 1)
        {
            _popIn.Children.RemoveAt(1); // remove the ScaleY animation
            _rootScale.ScaleY = 1;
        }'''

new_target = '''        // Strip translate animation to prevent visual edge clipping (black window showing behind)
        if (_popIn.Children.Count > 1)
        {
            _popIn.Children.RemoveAt(1); // remove the TranslateY animation
            _rootScale.ScaleY = 1;
        }'''

if target in pw:
    pw = pw.replace(target, new_target)
    print('Fixed animation issue')
else:
    print('Target not found')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)
