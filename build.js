const { execSync } = require('child_process');
try {
    const output = execSync('dotnet build', {
        cwd: 'C:\\Users\\qdsyq\\Desktop\\godot-floating-objects',
        shell: 'cmd.exe',
        encoding: 'utf8',
        timeout: 300000
    });
    process.stdout.write(output);
    process.exit(0);
} catch (e) {
    if (e.stdout) process.stdout.write(e.stdout);
    if (e.stderr) process.stderr.write(e.stderr);
    process.exit(e.status || 1);
}
