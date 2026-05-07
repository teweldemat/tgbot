# Installations

This document captures the deployment details observed and changed during the
PostgreSQL migration work. Secrets are intentionally omitted; read live service
configuration on the host when privileged access is needed.

## Payment bot

### Current host

- Host: `tewelde@rc.intaps.com`
- Service: `tgbot-payment.service`
- Status after migration: enabled and active
- App user/group: `tgbot-payment:tgbot-payment`
- App root: `/opt/tgbot/payment`
- Current release symlink: `/opt/tgbot/payment/current`
- Shared config: `/etc/tgbot-payment/appsettings.json`
- Public URL prefix: `https://rc.intaps.com/pay/`
- Internal Kestrel endpoint: `http://127.0.0.1:5082`
- Bot app: `TgBot.SmartLedger.SmartLedgerBot`
- Systemd ordering: `After=postgresql@12-main.service` and
  `Wants=postgresql@12-main.service`; do not use `Requires=` for PostgreSQL,
  because a PostgreSQL maintenance stop should not leave the bot permanently
  stopped after PostgreSQL is started again.

### PostgreSQL

- Host: local PostgreSQL on `rc.intaps.com`
- Server version observed: PostgreSQL 12
- Database: `intaps_pay`
- App role: `tgbot_payment`
- EF migration history:
  - `20260102013940_InitPostgres`
  - `20260102013949_InitPostgres`

### Nginx

The rc nginx site `/etc/nginx/sites-available/164.92.70.14.conf` proxies the
payment bot under `/pay/`:

```nginx
location = /pay {
    return 301 /pay/;
}

location /pay/ {
    proxy_pass http://127.0.0.1:5082/;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-Prefix /pay;
}
```

### Deployment commands

Publish from the development machine:

```sh
dotnet publish TgBot/TgBotApp.csproj -c Release -r linux-x64 --self-contained true -o /tmp/tgbot-publish /p:PublishSingleFile=false
```

Create a timestamped release on `rc.intaps.com`, extract the package into
`/opt/tgbot/payment/releases/<timestamp>`, replace the published
`appsettings.json` with a symlink to `/etc/tgbot-payment/appsettings.json`,
update `/opt/tgbot/payment/current`, then restart:

```sh
sudo systemctl restart tgbot-payment.service
```

Useful checks:

```sh
systemctl status tgbot-payment.service --no-pager -l
sudo journalctl -u tgbot-payment.service -n 80 --no-pager
curl -i http://127.0.0.1:5082/sl/summary/
curl -i https://rc.intaps.com/pay/sl/summary/
sudo -u postgres psql -d intaps_pay -c 'select count(*) from "Payment";'
```

### Migration notes

- Data was copied from the old SQL Server `IntapsPay` database on
  `app.intaps.com` into PostgreSQL `intaps_pay` on `rc.intaps.com`.
- `WFDialogStack` was intentionally cleared after migration per operator
  request. The live bot may create new dialog rows after startup.
- Smoke-test counts after migration:
  - `Payment`: 974
  - `TgUserState`: 66
  - `AuditRecord`: 25086
  - `WFDialogStack`: 0
- Backup on the previous host:
  `/var/opt/mssql/backups/IntapsPay_20260427102155.bak`
  - Size: 2.4 GB
  - SHA-256:
    `70a14204a3a923406647a77a8c7c12872cd602381d672f830914ba2646fe164e`
- Local backup for the retired TLT payment bot:
  `~/intaps/important-backups/TLTPay_20260427104325.bak`
  - Size: 303 MB
  - SHA-256:
    `432761a1336d9b1f083391afc7811aa4226285c3bc7936e41b18ca58046b660c`

### Previous host

- Host: `tewelde@app.intaps.com`
- Service: `intapspayment.service`
- Previous app path: `/var/IntapsPay/App/TgBotApp.dll`
- Previous database: SQL Server database `IntapsPay`
- Status after migration:
  - `intapspayment.service` removed from systemd.
  - `/var/IntapsPay/App` removed.
  - `intapstask.service` removed from systemd after confirming it was
    superseded by the rc deployment.
  - `/var/IntapsTask` removed.
  - SQL Server database `IntapsPay` dropped after backup verification.
  - SQL Server database files under `/var/IntapsPay/DB` removed.
  - `tlt_pay.service` removed from systemd.
  - `/usr/bin/tlt/paybot` removed.
  - SQL Server database `TLTPay` backed up locally, dropped, and removed from
    the server.
  - SQL Server itself was left running because other hosted services still have
    active connections and systemd references to it.
