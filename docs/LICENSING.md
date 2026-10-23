# Manual bank-payment subscriptions

Branch: `marketing`. This is a separate .NET 10 licence server with an owner website and a desktop Subscription tab. No payment gateway is involved. Hosting has not been provisioned by this change.

## Test locally in Visual Studio / PowerShell

1. Run `./scripts/Start-LicenceServer.ps1 -Setup` from a terminal. Choose an owner username, a password of at least 16 characters, and your bank-transfer instructions. Setup stores a password hash in ignored `licence-data/server-settings.json`; it never puts the password in the desktop app.
2. Run `./scripts/Start-LicenceServer.ps1` and leave that terminal open. Browse to `http://localhost:5080`, sign in, and create a school licence with a paid-through date. Copy its key when it appears; the raw key is shown once.
3. In another PowerShell terminal, run `./scripts/Configure-LicenceClient.ps1 -ServerUrl http://localhost:5080 -LocalDevelopment`. This writes ignored `licensing.local.json`. In Visual Studio, choose **Debug**, set **SchoolExamScheduler.Desktop** as the startup project, and run it. Keep the server running separately.
4. Open **Subscription**, paste the school key, and activate. Create or change a roster. Try switching the licence off in the owner website, then attempt another save/generation: the app should refuse the change, while PDF/Excel exports and existing reports stay available. After re-enabling, click Refresh subscription (also checked automatically every five minutes).
5. To test offline operation, activate while online, then stop the server. Saves can continue with the last signed allowance. Its deadline appears on Subscription. Once 24 hours have passed since the last successful check, or the paid-through date is reached, writes stop until a successful online check. No first-time offline activation is allowed.

`--demo` uses a separate sample school database but requires the same active subscription for changes and generation. The app can always open without a configured server, in read-only mode. Release builds reject HTTP, and never publish/use the local-development configuration. Do not distribute an unconfigured production build.

## Owner's monthly workflow

- Create one licence per school. Enter the school's contact details and allowed computer installations (default two). Give the school its licence key, bank details, and the payment reference displayed on the owner page.
- Check your bank statement yourself. Open the school's card, choose a later **paid-through date**, enter the bank statement reference and optional notes, then **Record payment / extend**. This records an audit entry; it does not charge money or issue a tax invoice.
- Dates are inclusive, in South Africa (UTC+2). Paid through 31 October means access ends at midnight entering 1 November in South Africa. Expiry happens automatically; the server need not be manually switched off each month.
- **Switch off licence** blocks subsequent online checks. Offline clients can retain access for the accepted maximum 24-hour allowance, never past their paid-through date. It is not an instantaneous offline kill switch.
- **Enable licence** removes the manual block. Extending payment does not automatically undo a manual disable; do both if appropriate. The school then refreshes its subscription.
- **Replace lost key** invalidates the old key online and shows the replacement once. **Reset installations** frees slots after computer replacement. Replace the key too if the old computer should not activate again. Previously issued offline allowances still expire naturally.
- Audit history records changes, the owner, timestamps, and payment references. Stale admin forms are rejected so a second browser session cannot silently overwrite a newer change.

There is one owner account in this first version. Update bank/contact instructions in the protected `server-settings.json` and restart the server. To change the password, stop the server, retain a protected backup of settings, move the settings file aside, rerun `--setup` in the same data directory, then restart. Keep the existing database and signing key.

## Publish a real server

Use a Linux server with Docker Compose, a domain/subdomain pointed to it, and ports 80/443 open. The included configuration runs one licence-server instance behind Caddy, which obtains HTTPS certificates. Only Caddy's ports are public. Check that the example private subnet is available on your host; if changed, update the server's trusted proxy address too.

From `deployment/licensing`:

```sh
# Create .env here (do not commit it):
# LICENCE_DOMAIN=licences.your-domain.co.za
# OWNER_EMAIL=you@your-domain.co.za
docker compose build
docker compose run --rm -it licence-server --setup
docker compose up -d
```

Visit `https://YOUR_DOMAIN`, sign in, and create a test school. Then on your Windows build machine:

```powershell
./scripts/Configure-LicenceClient.ps1 -ServerUrl https://YOUR_DOMAIN
./scripts/Publish-Windows.ps1
```

Distribute the complete published Windows folder. `licensing.json` contains only the HTTPS origin and public signing key. Fetch this from your own trusted server over HTTPS; never put the private signing key, owner password/hash, server settings, or bank administration credentials in the desktop package. Changing hosts can retain the public key by moving the original server data directory. Rotating the private signing key requires updating the desktop public key too.

The Docker image and Compose files are deployment assets; the container build was blocked by an HTTP 403 while downloading Microsoft’s base image, so the container deployment still needs a smoke test on your host. The ASP.NET server/admin/API and desktop build are covered by automated checks.

## Data, access, and recovery

The server stores school/contact names, licence-key hashes (not recoverable raw keys), paid-through dates, installation IDs, last check times and owner audit history. Checks send the school key, random installation ID and request nonce; teacher records, examinations and rosters stay on the school's computer. Protect and retain customer information appropriately; manual EFT does not remove your invoicing, tax or privacy responsibilities.

Each server response is RSA signed and tied to the licence key and installation. A replayed response cannot satisfy a new online request. Cached signed access lasts at most 24 hours, is capped by payment expiry, and rejects substantial clock rollback. An explicit online denial clears cached access immediately. As with any locally installed software, this is a licensing mechanism, not protection against someone modifying the executable or operating system.

Expired/disabled/offline-expired customers can read existing data and export reports. Save/delete/commit operations are gated in the data store, and generation is checked before the solver starts and before results commit. Backup and restore remain accessible for recovery. Local licence state is separate from school database backups. If the local activation file is lost or damaged, reactivate; the owner may need to release an installation slot.

Back up the **whole server data directory**, including `licences.db`, `signing-key.pem`, `server-settings.json`, and `cookie-keys/`, to a private encrypted location. Stop the server for a filesystem copy so the SQLite database/WAL are consistent. Never delete the signing key on an existing database: startup refuses to silently generate a new identity. Test restoring this backup before selling subscriptions. For Docker, back up the named licence-data volume; `docker compose down` preserves it, while `down -v` deletes it. Restrict host/volume access to the owner and server account. The included server runs as a non-root user, uses owner authentication, CSRF protection, HTTPS in production, and request rate limits. Keep the host and .NET/container images updated.
