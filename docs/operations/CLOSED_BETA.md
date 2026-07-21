# Closed beta operation

Set `Beta__RegistrationOpen=false` and provide `Beta__InviteCode` from a secret store. Existing users can continue signing in; only new registrations require the invite code. Keep the public default open only for local automated tests.

Before staging: enable HTTPS, replace the fake email provider, rotate database credentials, configure persistent data-protection keys, set allowed hosts, run the full test suite and backup drill, and nominate incident owners.
