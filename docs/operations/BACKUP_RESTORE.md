# Backup and restore

Run `powershell -File scripts/backup-restore-drill.ps1` from the repository. It creates a timestamped SQL backup under `artifacts/backups`, restores it into the exact temporary database `marketplace_restore_drill`, validates the number of public tables, and removes only that temporary database.

Production policy: encrypted daily backups, point-in-time WAL retention, off-site copy, quarterly restore drills, documented RPO/RTO and access restricted to Owner/SecuritySpecialist.
