# TPS Project Server

Simple Node.js server for testing Unity client-to-server communication.

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
