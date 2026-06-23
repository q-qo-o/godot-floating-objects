import subprocess, sys, os
os.chdir(r'C:\Users\qdsyq\Desktop\godot-floating-objects')
with open('build_log.txt', 'w') as f:
    r = subprocess.run(['dotnet', 'build'], capture_output=True, text=True, shell=True)
    f.write(r.stdout + r.stderr)
    print(r.stdout + r.stderr, end='')
    sys.exit(r.returncode)
