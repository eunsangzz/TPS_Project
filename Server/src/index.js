const express = require("express");

const app = express();
const port = process.env.PORT || 3000;

app.get("/ping", (req, res) => {
  res.json({
    message: "pong",
    serverTime: new Date().toISOString(),
  });
});

app.listen(port, () => {
  console.log(`TPS server is running at http://localhost:${port}`);
});
