import subprocess
import sys

result = subprocess.run(
    ["dotnet", "build"],
    cwd=r"C:\Users\qdsyq\Desktop\godot-floating-objects",
    capture_output=True,
    text=True,
    shell=True
)
print(result.stdout + result.stderr)
sys.exit(result.returncode)
