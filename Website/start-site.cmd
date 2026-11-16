@echo off
setlocal
where node >nul 2>&1
if not errorlevel 1 (
  node "%~dp0open-site.mjs"
) else (
  "%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe" "%~dp0open-site.mjs"
)
if errorlevel 1 pause
