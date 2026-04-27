# Installations (wsis2)

This document captures the current deployment details observed on `tewelde@wsis2`.

## Host
- Hostname: intpas-ubuntu
- Kernel: Linux 4.15.0-197-generic x86_64 (Ubuntu)
- Public IP: 64.225.3.202
- Other interfaces: 10.17.0.5, 192.168.21.10, 192.168.70.10, 192.168.31.10, 172.16.4.37, 192.168.50.10, 10.132.186.127

## Services
- nginx: active (reverse proxy)
- mssql-server: active (SQL Server)
- postgresql@10-main: active (PostgreSQL 10)

### .NET bots and services
- intapspayment.service
  - Description: INTAPS Payment bot
  - ExecStart: /usr/bin/dotnet /var/IntapsPay/App/TgBotApp.dll
  - WorkingDirectory: /var/IntapsPay/App
  - Environment: ASPNETCORE_ENVIRONMENT=Production
  - Status: active (running)
- tlt_pay.service
  - Description: TLT Pay Service
  - ExecStart: /usr/bin/dotnet /usr/bin/tlt/paybot/app/TgBotApp.dll
  - WorkingDirectory: /usr/bin/tlt/paybot/app
  - Status: active (running)
- intapstask.service
  - Description: Intaps Task Managment Telegram Bot
  - ExecStart: /bin/bash /var/IntapsTask/bin/start.sh
  - start.sh:
    - cd /var/IntapsTask/bin/
    - dotnet TgBotApp.dll
  - Status: active (running)

## Listening ports
- 82/TCP
  - Process: dotnet (PID 1217)
  - Binary: /usr/bin/dotnet
  - App: /var/IntapsPay/App/TgBotApp.dll
- 83/TCP
  - Process: dotnet (PID 1172)
  - Binary: /usr/bin/dotnet
  - App: /usr/bin/tlt/paybot/app/TgBotApp.dll
- 8084/TCP (bound to 64.225.3.202)
  - Process: dotnet (PID 1273)
  - Binary: /usr/bin/dotnet
  - App: /var/IntapsTask/bin/TgBotApp.dll
- 8222/TCP
  - Process: nginx

## Nginx configuration
- Enabled sites:
  - /etc/nginx/sites-enabled/64.225.3.202.vhost -> /etc/nginx/sites-available/64.225.3.202.vhost
  - /etc/nginx/sites-enabled/bank-gateway.conf -> /etc/nginx/sites-available/bank-gateway.conf
  - /etc/nginx/sites-enabled/default -> /etc/nginx/sites-available/default
- /etc/nginx/sites-available/64.225.3.202.vhost contains multiple server blocks and reverse proxies for app.intaps.com.

## App configuration and database settings

### /var/IntapsPay/App/appsettings.json
- ConnectionStrings:TGBot
  - Host=64.225.3.202
  - Database=IntapsPay
  - Username=IntapsPayment
  - Password=KkVX9YZJ
- WeBirrCheckOut
  - EndPoint=https://api.webirr.com/einvoice/api/
  - APIKey=dmFZz72KRnWFpfC8
  - MerchantID=091929
- SmartLedger:Web
  - http://app.intaps.com:82
- Bot
  - BotToken=1873551819:AAFTTZUklUpV1xZgCnzXH1JtQHf2iz3M5j8
  - BotApp=TgBot.SmartLedger.SmartLedgerBot
  - Admin=teweldetg
  - DebugMode=true
  - TimeOffset=0
- Kestrel:Endpoints:HTTP:Url
  - http://app.intaps.com:82

### /usr/bin/tlt/paybot/app/appsettings.json
- ConnectionStrings:TGBot
  - Host=64.225.3.202
  - Database=TLTPay
  - Username=IntapsPayment
  - Password=KkVX9YZJ
- WeBirrCheckOut
  - EndPoint=https://api.webirr.com/einvoice/api/
  - APIKey=dmFZz72KRnWFpfC8
  - MerchantID=091929
- SmartLedger:Web
  - http://app.intaps.com:83
- Bot
  - BotToken=6759827116:AAFZNcYnSXbYnP5Ivoj8D5XlUdjWCtHNW-A
  - BotApp=TgBot.SmartLedger.SmartLedgerBot
  - Admin=teweldetg
  - DebugMode=true
  - TimeOffset=0
- Kestrel:Endpoints:HTTP:Url
  - http://app.intaps.com:83

### /var/IntapsTask/bin/appsettings.json
- ConnectionStrings:TGBot
  - Host=64.225.3.202
  - Database=IntapsPay
  - Username=IntapsPayment
  - Password=KkVX9YZJ
- WeBirrCheckOut
  - EndPoint=https://api.webirr.com/einvoice/api/
  - APIKey=dmFZz72KRnWFpfC8
  - MerchantID=091929
- SmartLedger:Web
  - http://64.225.3.202:8084
- Bot
  - BotToken=1909308210:AAHmpuW32zxTivtIewdr5Q2XTq5O4AIO8jo
  - BotApp=TgBot.Tasks.TaskBot
  - Admin=teweldetg
  - InstPage=https://api.webirr.com:441/portal/checkoutinst?wbc_checkout={wbc_checkout}
  - LM=WeFund.LM
  - DebugMode=false
- Kestrel:Endpoints:HTTP:Url
  - http://64.225.3.202:8084
