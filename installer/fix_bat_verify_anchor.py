"""修正 install.bat 的校验锚点:payload 里的 dotnet 是扁平布局,没有 host/fxr 子目录。"""
import os

BS = chr(92)
HERE = os.path.dirname(os.path.abspath(__file__))
p = os.path.join(HERE, 'install.bat')

t = open(p, 'rb').read().decode('gbk')
old = 'if not exist "' + '!GAME!' + BS + 'dotnet' + BS + 'host' + BS + 'fxr" goto COPY_FAIL'
new = 'if not exist "' + '!GAME!' + BS + 'dotnet' + BS + 'coreclr.dll" goto COPY_FAIL'

if new in t:
    print('已经是新锚点,无需修改')
elif old in t:
    t = t.replace(old, new, 1)
    open(p, 'wb').write(t.encode('gbk'))
    print('已改为: ' + new)
else:
    print('**未找到锚点,当前校验行:**')
    for l in t.splitlines():
        if 'goto COPY_FAIL' in l:
            print('   ' + l)

b = open(p, 'rb').read()
print('CRLF=%d  LF=%d  %s' % (b.count(b'\r\n'), b.count(b'\n'),
                              'OK' if b.count(b'\r\n') == b.count(b'\n') else '**换行符不一致**'))
