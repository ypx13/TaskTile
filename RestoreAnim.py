with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'r', encoding='utf-8') as f:
    pw = f.read()

target = '''        // Strip translate animation to prevent visual edge clipping (black window showing behind)
        if (_popIn.Children.Count > 1)
        {
            _popIn.Children.RemoveAt(1); // remove the TranslateY animation
            _rootScale.ScaleY = 1;
        }'''

if target in pw:
    pw = pw.replace(target, '')
    print('Restored animation!')
else:
    print('Target not found')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Windows\PopupWindow.cs', 'w', encoding='utf-8') as f:
    f.write(pw)
