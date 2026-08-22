import winreg
try:
    key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, r'Software\Microsoft\Windows\CurrentVersion\Themes\Personalize')
    apps_light, _ = winreg.QueryValueEx(key, 'AppsUseLightTheme')
    system_light, _ = winreg.QueryValueEx(key, 'SystemUsesLightTheme')
    print(f'AppsUseLightTheme: {apps_light}, SystemUsesLightTheme: {system_light}')
except Exception as e:
    print('Could not read registry:', e)
