with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml', 'r', encoding='utf-8') as f:
    xaml = f.read()

# Fix toggles being sub-options (removing Margin="16,0,0,0")
xaml = xaml.replace('Margin="16,0,0,0"', 'Margin="0,0,0,0"')

# Rename 'Keep group open when clicking outside' to 'Disable lose focus'
xaml = xaml.replace('Keep group open when clicking outside', 'Disable lose focus')

with open(r'C:\Users\yassinh\.gemini\TaskTile\Controls\GroupCard.xaml', 'w', encoding='utf-8') as f:
    f.write(xaml)

print('GroupCard xaml updated')
