# Development workflow scripts

PowerShell scripts that press the launcher's buttons through Windows UI Automation, so that an AI agent or a developer can stop and
start the server the same way the owner does. See [docs/WORKFLOW-AND-COMMANDS.md](../../docs/WORKFLOW-AND-COMMANDS.md), section 7.

- `stop_server.ps1`: needs exactly one launcher and one CoreServer process; clicks STOP SERVER, answers the "Shutdown pending" box,
  waits up to 240 s for CoreServer to exit and reports whether port 10300 still listens. It never kills anything.
- `start_server.ps1`: needs one launcher and no CoreServer; clicks START SERVER, answers "Low available memory" with Yes, stops with exit 7
  when a dialog needs a person ("Unsaved bot goals", "Invalid bot goals", "Cannot start", "Unable to start server"), waits up to 480 s for port 10300.

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\dev-workflow\stop_server.ps1`. Only when the owner asked.
Exit codes: 0 ok; 2 wrong process count; 3 or 5 button missing or disabled; 4 server still running after 240 s; 6 port not listening; 7 dialog needs a person.
