import http from "node:http";
import process from "node:process";
import { WebSocketServer } from "ws";
import { Matchmaker } from "./room.js";
import { FLEET_ORDER } from "./rules.js";
import { createGameStore } from "./store/index.js";

const PORT = Number(process.env.PORT) || 8080;

const store = createGameStore();
const matchmaker = new Matchmaker(store);

const server = http.createServer((req, res) => {
  if (req.url === "/health" || req.url === "/") {
    res.writeHead(200, { "Content-Type": "text/plain; charset=utf-8" });
    res.end("ok");
    return;
  }
  if (req.url === "/rules") {
    res.writeHead(200, { "Content-Type": "application/json; charset=utf-8" });
    res.end(
      JSON.stringify({
        BoardSize: 10,
        Fleet: FLEET_ORDER,
        WebSocketPath: "/ws",
      }),
    );
    return;
  }
  res.writeHead(404);
  res.end();
});

const wss = new WebSocketServer({ server, path: "/ws" });

wss.on("connection", (ws) => {
  ws.on("message", (data) => {
    const raw = typeof data === "string" ? data : data.toString("utf8");
    void matchmaker.handleMessage(ws, raw);
  });

  ws.on("close", () => {
    matchmaker.removeFromQueue(ws);
  });
});

server.on("error", (e: NodeJS.ErrnoException) => {
  if (e.code === "EADDRINUSE") {
    console.error(`[game-server] port ${PORT} is already in use (set PORT env)`);
  } else {
    console.error("[game-server] HTTP server error:", e);
  }
  process.exit(1);
});

server.listen(PORT, () => {
  console.error(
    `[game-server] listening http://localhost:${PORT}  ws://localhost:${PORT}/ws`,
  );
});
