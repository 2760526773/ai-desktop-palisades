# AI Desktop Palisades v0.3.2

## Highlights

- Fixed desktop restore behavior so system reboot and app restart no longer break managed fence targets
- Added startup repair for abnormal shutdowns to rebuild managed item references from `managed-items`
- Added startup cleanup for stale shortcut references
- Added menu toggle for Windows auto-start
- Added visible menu status text for auto-start (`已开启` / `已关闭`)
- Fixed process not exiting after clicking menu `退出` by converting the background save loop to a background thread

## User-visible Changes

- Manual `退出` still restores desktop items back to the desktop root
- System shutdown / reboot no longer triggers desktop restore
- App-internal `重启` no longer restores desktop items before restarting
- On abnormal shutdown, the next launch attempts to repair managed fence items automatically
- The fence menu now includes a working auto-start toggle

## Files To Upload

- `AI-Desktop-Palisades-v0.3.2-win-x64.zip`

## Notes

- Upstream project: https://github.com/Xstoudi/Palisades
- Inspiration: https://github.com/Twometer/NoFences
- This repository is a modified fork focused on AI-powered desktop organization for Windows
