# TPS Project Server

Simple Node.js server for testing Unity client-to-server communication.

Production URL:

```text
https://tps-project.onrender.com
```

## Setup

```powershell
cd Server
npm install
npm start
```

## Test

Open this URL in a browser:

```text
http://localhost:3000/ping
```

Expected response:

```json
{
  "message": "pong",
  "serverTime": "2026-08-31T00:00:00.000Z"
}
```

## Auth Test

Guest login:

```powershell
Invoke-RestMethod http://localhost:3000/auth/guest `
  -Method Post `
  -ContentType "application/json" `
  -Body '{"displayName":"Guest"}'
```

Demo user login:

```powershell
Invoke-RestMethod http://localhost:3000/auth/login `
  -Method Post `
  -ContentType "application/json" `
  -Body '{"username":"player","password":"1234"}'
```

Load player data with the returned token:

```powershell
$token = "paste-token-here"
Invoke-RestMethod http://localhost:3000/player-data `
  -Headers @{ Authorization = "Bearer $token" }
```

Save player data:

```powershell
$token = "paste-token-here"
Invoke-RestMethod http://localhost:3000/player-data `
  -Method Put `
  -ContentType "application/json" `
  -Headers @{ Authorization = "Bearer $token" } `
  -Body '{"level":2,"xp":150,"coins":250,"selectedWeapon":"Rifle"}'
```
