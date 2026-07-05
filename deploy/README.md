# Deploying to the Proxmox LXC

Manual, on-demand deploy of `Barrelo.Api` (and, by default, the Board Simulator) from a Windows dev
machine to a Barrelo LXC container on the local network, over SSH. No GitHub Actions involved — this all
runs locally, and mirrors the same `linux-x64` publish layout the [release workflow](../.github/workflows/release.yml)
produces (`Barrelo.Api` at the root, `tools/BoardSimulator/Barrelo.BoardSimulator` alongside it).

Both processes run as system-wide `systemd` services under `/opt/barrelo`, matching the
[systemd section of the main README](../README.md#running-continuously-on-linux-systemd).

## One-time setup on the LXC

Run these once, logged into the container directly (`pct enter <vmid>` from the Proxmox host, or SSH
if already reachable). The deploy user needs `sudo` — the default user on most Proxmox LXC templates
already has it.

1. **Install prerequisites** (Debian/Ubuntu-based container):

   ```bash
   sudo apt update
   sudo apt install -y unzip openssh-server
   ```

   No .NET runtime needed — the published apps are self-contained.

2. **Enable SSH key login** for the account you'll deploy as (skip if already set up):

   ```bash
   mkdir -p ~/.ssh && chmod 700 ~/.ssh
   echo "<your Windows machine's public key>" >> ~/.ssh/authorized_keys
   chmod 600 ~/.ssh/authorized_keys
   ```

   From Windows, if you don't already have a key: `ssh-keygen -t ed25519` (in PowerShell), then copy
   `~/.ssh/id_ed25519.pub`'s contents into the line above. Confirm login works: `ssh <user>@<lxc-ip>`.

3. **Create `/opt/barrelo` and install both systemd units:**

   ```bash
   sudo mkdir -p /opt/barrelo
   ```

   Copy [`barrelo.service`](barrelo.service) and [`barrelo-boardsimulator.service`](barrelo-boardsimulator.service)
   from this repo to the box, e.g.:

   ```bash
   scp deploy/barrelo.service deploy/barrelo-boardsimulator.service <user>@<lxc-ip>:/tmp/
   ```

   then on the box:

   ```bash
   sudo mv /tmp/barrelo.service /tmp/barrelo-boardsimulator.service /etc/systemd/system/
   sudo systemctl daemon-reload
   sudo systemctl enable barrelo barrelo-boardsimulator
   ```

   `barrelo.service` binds the Api to `http://0.0.0.0:5295` and `barrelo-boardsimulator.service` binds the
   simulator to `http://0.0.0.0:5250` (both via `Environment=ASPNETCORE_URLS` in the unit files), so they're
   reachable from other devices on the LAN — including opening the simulator's own page remotely to throw
   darts against the deployed Api.

That's it — the first real deploy (below) populates `/opt/barrelo` and starts both services.

## Deploying

From the repo root on Windows:

```powershell
./deploy/deploy.ps1 -RemoteHost 192.168.1.50 -RemoteUser barrelo
```

This publishes `Barrelo.Api` and `Barrelo.BoardSimulator` as self-contained `linux-x64` builds into a single
staged tree (same layout as a release zip), copies it to the LXC over `scp`, then `sudo`-stops both
services, unpacks on top of `/opt/barrelo`, and restarts them — printing `systemctl status` for both so you
can confirm they came back up. `ssh -t` is used so you can enter your `sudo` password interactively if
prompted.

Pass `-NoBoardSimulator` to only publish/deploy/restart `Barrelo.Api` and leave the simulator service alone.

`/opt/barrelo/barrelo.db` (the SQLite database) is never part of the publish output, so it's untouched by
redeploys — data persists across versions.

### Parameters

| Param | Default | Meaning |
|---|---|---|
| `-RemoteHost` | *(required)* | LXC IP or hostname on the LAN. |
| `-RemoteUser` | *(required)* | SSH user configured in setup step 2 (needs `sudo`). |
| `-RemoteDir` | `/opt/barrelo` | Install directory on the LXC. |
| `-Rid` | `linux-x64` | Change to `linux-arm64` if the LXC/host is ARM. |
| `-NoBoardSimulator` | *(off)* | Skip publishing/deploying/restarting the Board Simulator. |

## Troubleshooting

- `ssh <user>@<host>` failing outright → re-check step 2 (key auth) or that the container's SSH server
  is running and reachable (same LAN/VLAN, Proxmox firewall rules).
- Service won't start after deploy → `ssh <user>@<host> "systemctl status barrelo barrelo-boardsimulator --no-pager -l"`
  and `journalctl -u barrelo -u barrelo-boardsimulator -n 50 --no-pager` for logs.
- Port 5295/5250 unreachable from other LAN devices, or the Api crashes on startup with an HTTPS
  dev-certificate error → check the unit's `ExecStart` still passes `--urls=http://0.0.0.0:<port>`. Both
  `appsettings.json` files hardcode a `"Urls"` key (the Api's includes an `https://` endpoint), and ASP.NET
  Core's config precedence means `appsettings.json` silently wins over an `ASPNETCORE_URLS` environment
  variable — command-line `--urls` is the one source that reliably overrides it. Also confirm nothing (ufw,
  Proxmox firewall) blocks the ports.
