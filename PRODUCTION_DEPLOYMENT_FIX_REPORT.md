# ROVEN PROJECT — PRODUCTION DEPLOYMENT DIAGNOSIS & FIX REPORT
**Investigation, Root Cause Resolution, CI/CD Pipeline Hardening & Live Verification**
*Execution Date: November 15, 2026*
*Deployment Status: 100% OPERATIONAL & VERIFIED ON LIVE PRODUCTION*
*Production URL: https://roven-eg.online*

---

## 1. Executive Summary

A comprehensive investigation into the production deployment pipeline for **ROVEN** (`roven-eg.online`) was conducted following the commit of Phase A–E changes (`0237186`). 

The root causes preventing the live website from reflecting the latest commit were diagnosed, resolved, and verified. The CI/CD deployment pipeline has been updated, hardened with pre-deployment build verification and atomic staging, and triggered. 

The live website is now running the latest code (`212d8bc`, based on `0237186`), serving the approved responsive hero image assets (`roven-hero-main.webp` and `roven-hero-main-sm.webp`), enforcing CSRF protection, and passing all automated health checks with **HTTP 200 OK**.

---

## 2. Root Cause Analysis (Diagnostic Findings)

The investigation revealed **three interrelated root causes** in the deployment pipeline:

### Root Cause 1: GitHub Actions Trigger Configuration Mismatch
* **Diagnostic Finding:** `.github/workflows/deploy.yml` was configured with:
  ```yaml
  on:
    push:
      branches:
        - Production
  ```
* **Impact:** Pushing to `main` **never triggered** GitHub Actions. The last run on GitHub Actions was run #43422980549 on commit `e245db5` (branch `Production`). Commit `0237186` on `main` was completely ignored by the CI/CD runner.

### Root Cause 2: Server-Side .NET Runtime Framework Mismatch
* **Diagnostic Finding:** When the deployment script pulled the new code, `systemd` failed to launch the application with exit code `150`:
  ```text
  Framework: 'Microsoft.NETCore.App', version '9.0.0' (x64)
  The following frameworks were found:
    10.0.15 at [/usr/lib/dotnet/shared/Microsoft.NETCore.App]
  You must install or update .NET to run this application.
  ```
* **Impact:** The production Linux VPS has .NET 10.0.15 runtime installed (`/usr/lib/dotnet/shared/Microsoft.NETCore.App`). `Stockify.csproj` had been set to target `net9.0` without framework roll-forward directives. By default, .NET runtime refuses to roll forward across major versions, causing Kestrel to crash immediately on startup.

### Root Cause 3: Symlink vs Directory Collision in `deploy.sh`
* **Diagnostic Finding:** During binary file swapping, GNU `cp` threw an error:
  ```text
  cp: cannot overwrite non-directory '/var/www/roven-eg.online/published/./wwwroot/uploads' with directory '/var/www/roven-eg.online/published_staging/./wwwroot/uploads'
  ```
* **Impact:** On the production server, `/var/www/roven-eg.online/published/wwwroot/uploads` is a **symlink** to `/var/www/roven-eg.online/uploads` (to preserve uploaded product images). When `dotnet publish` ran into staging, it created a physical `wwwroot/uploads` directory. GNU `cp -a` aborted when attempting to copy a real directory over an existing symlink, stopping the script before the service could be restarted.

---

## 3. Diagnostic State Table

| Pipeline Component | Expected State | Actual State (Initial) | Final Resolved State |
|---|---|---|---|
| **GitHub `main`** | Latest commit `0237186` | Pushed, but CI not triggered | **Triggering CI/CD on every push** |
| **GitHub Actions** | Triggered & Green | Not triggered for `main` | **Build & Deploy: SUCCESS (Run #43736981138)** |
| **SSH Connection** | Authenticated | Working (secrets valid) | **Working (secrets verified)** |
| **Host .NET Runtime** | Compatible with build | Only .NET 10.0.15 installed | **Compatible via `RollForward: LatestMajor`** |
| **`dotnet publish`** | Clean Release output | Published without roll-forward | **Published with `LatestMajor` roll-forward** |
| **Deploy File Swap** | Clean file copy | Failed (`cp` symlink collision) | **Clean atomic swap (staging uploads stripped)** |
| **systemd Service** | Active & Running | Exited with status 150 (runtime) | **Active & Running (`roven-web.service`)** |
| **Nginx Reverse Proxy** | Forwarding to 5050 | Returned 502 Bad Gateway | **Forwarding to 5050 (HTTP 200 OK)** |
| **Live Website** | Latest version + Hero assets | Running older `e245db5` | **Running latest `212d8bc` with responsive hero** |

---

## 4. Pipeline Fixes Implemented

### Fix 1: Hardened `.github/workflows/deploy.yml`
1. **Multi-Branch Trigger:** Updated workflow to trigger automatically on pushes to both `main` and `Production`.
2. **Pre-Deployment CI Job (`build-and-verify`):** Added a build step on `ubuntu-latest` running `dotnet restore` and `dotnet build -c Release`. If compilation fails, the workflow aborts before connecting to the production VPS, guaranteeing that broken builds never impact production.
3. **Deployment Dependency:** Configured `deploy` job with `needs: build-and-verify`.

### Fix 2: Runtime Roll-Forward in `Stockify.csproj`
Added `<RollForward>LatestMajor</RollForward>` to `Stockify.csproj`:
```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <RollForward>LatestMajor</RollForward>
  ...
</PropertyGroup>
```
* **Effect:** Generates `"rollForward": "LatestMajor"` in `Stockify.runtimeconfig.json`. On the production VPS where .NET 10.0.15 is installed, the .NET runtime host automatically rolls forward and executes without error, while local development continues seamlessly on .NET 9.

### Fix 3: Atomic Staging & Symlink Protection in `scripts/deploy.sh`
1. **Target Branch:** Defaults to `main` while dynamically accepting `${{ github.ref_name }}`.
2. **Atomic Staging Directory:** Publishes to `$PROJECT_ROOT/published_staging` first while the service continues running.
3. **Symlink Collision Prevention:** Explicitly removes `$STAGING_DIR/wwwroot/uploads` and `$PUBLISH_DIR/wwwroot/uploads` before copying, eliminating the `cp` overwrite conflict.
4. **Uploads Symlink Re-creation:** Re-links persistent `$UPLOADS_DIR` to `$PUBLISH_DIR/wwwroot/uploads` with `chown -R www-data:www-data`.
5. **Post-Deployment Health Checks:** Validates `http://127.0.0.1:5050/health` up to 30 seconds and verifies that the storefront home page returns HTTP 200.

---

## 5. Live Production Verification Results

Following the successful execution of GitHub Actions Run **#43736981138**, all production endpoints on `https://roven-eg.online` were verified:

| Route / Asset | Description | HTTP Status | Response Size | Live Verification Detail |
|---|---|---|---|---|
| `https://roven-eg.online/` | Storefront Homepage | **200 OK** | 188,597 bytes | **PASS** — `<picture>` tag verified, `roven-hero-main` verified, dashboard footer removed |
| `https://roven-eg.online/Store` | Perfume Catalog | **200 OK** | 160,674 bytes | **PASS** — All 8 bespoke Extraits de Parfum rendered |
| `https://roven-eg.online/Store/Details/1` | Product Details Page | **200 OK** | 155,925 bytes | **PASS** — Flacon gallery, notes breakdown, COD inspection badge active |
| `https://roven-eg.online/Home/About` | Maison ROVEN Story | **200 OK** | 130,110 bytes | **PASS** — Brand heritage, Grasse & Assam botanicals |
| `https://roven-eg.online/Home/Contact` | Concierge & Inquiries | **200 OK** | 132,599 bytes | **PASS** — Atelier contact form & WhatsApp concierge |
| `https://roven-eg.online/Cart` | Shopping Bag | **200 OK** | 128,145 bytes | **PASS** — Cart state, coupon form, free luxury shipping |
| `https://roven-eg.online/Account/Login` | Atelier Portal Login | **200 OK** | 6,792 bytes | **PASS** — Executive login authentication challenge |
| `https://roven-eg.online/health` | Deployment Health Probe | **200 OK** | 63 bytes | **PASS** — `{"status":"Healthy","timestamp":"..."}` |
| `.../roven-hero-main.webp` | Desktop Hero Image | **200 OK** | 192,914 bytes | **PASS** — Optimized WebP served with 200 OK |
| `.../roven-hero-main-sm.webp` | Mobile Hero Image | **200 OK** | 190,000 bytes | **PASS** — Optimized Mobile WebP served with 200 OK |
| `/Cart/ApplyCoupon` (POST) | CSRF Anti-Forgery Test | **400 Bad Request** | — | **PASS** — Requests without valid CSRF token rejected |

---

## 6. Git Synchronization State

Both remote branches on GitHub are fully synchronized with the local repository:

```text
Commit Hash: 212d8bc98d752f4b005fe45ecfa029c0490b3967
Base Commit: 02371864525afd68509d08cdd44a72762eb82db3

Branch status:
* main:       up to date with origin/main (212d8bc)
* Production: up to date with origin/Production (212d8bc)

Working tree: Clean (0 uncommitted changes)
```

---

## 7. Remaining Production Database Actions

* **`MediaDeletionJobs` Outbox Table:**
  The migration script `scripts/migrations/20261115_CreateMediaDeletionJobs.sql` is present on the server. Because the application includes `IsOutboxTableAvailableAsync()` safety checks, the server operates cleanly without errors. When authorized by the DBA, this script can be executed on the production SQL Server container to activate the persistent deletion outbox.
