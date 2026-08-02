#!/usr/bin/env node
// mbxhub-mcp — minimal MCP stdio adapter for MBXHub (https://mbxhub.com).
// No MusicBee code lives here: every tool is one HTTP call to a running Hub.
// Source of truth for the API contract: GET {MBXHUB_URL}/llms.txt
//
// Runs directly under Node >= 22.18 (native type stripping): node index.ts

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";

const HUB = (process.env.MBXHUB_URL ?? "http://localhost:8080").replace(/\/+$/, "");

// stdout is the MCP transport — all logging goes to stderr.
function log(msg: string): void {
  process.stderr.write(`[mbxhub-mcp] ${msg}\n`);
}

async function hub(method: string, path: string, body?: unknown): Promise<string> {
  const started = Date.now();
  let res: Response;
  try {
    res = await fetch(`${HUB}${path}`, {
      method,
      headers: body !== undefined ? { "Content-Type": "application/json" } : undefined,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch (e) {
    log(`${method} ${path} -> unreachable (${Date.now() - started}ms)`);
    throw new Error(
      `MBXHub unreachable at ${HUB} (${e instanceof Error ? e.message : e}). ` +
        `Is MusicBee running with the MBXHub plugin enabled? Set MBXHUB_URL if the Hub is not on ${HUB}.`,
    );
  }
  const text = await res.text();
  log(`${method} ${path} -> ${res.status} (${Date.now() - started}ms)`);
  if (!res.ok) {
    // Surface the Hub's error body verbatim so the calling agent can self-correct.
    throw new Error(`MBXHub ${res.status} on ${method} ${path}: ${text || res.statusText}`);
  }
  return text || JSON.stringify({ ok: true, status: res.status });
}

// Playlist URLs are Windows file paths returned by list_playlists — they must be
// percent-encoded to travel as a single route segment.
const seg = (url: string) => encodeURIComponent(url);

const PLAYER_ACTIONS = ["play", "pause", "stop", "next", "previous"];

interface ToolDef {
  description: string;
  inputSchema: Record<string, unknown>;
  run: (args: Record<string, any>) => Promise<string>;
}

const tools: Record<string, ToolDef> = {
  search_library: {
    description:
      "Search the MusicBee library. Supports MBXHub's query DSL: qualifiers like " +
      "artist:, album:, genre:, year:1985..1990, rating:>=4, decade:80s, playcount:0; " +
      "operators OR, -exclusion, (grouping). Quote multi-word qualifier values: " +
      'artist:"miles davis" (unquoted, only the first word binds to the qualifier). ' +
      "Plain text also works. " +
      "Results include full file paths — use those paths with the playlist tools.",
    inputSchema: {
      type: "object",
      properties: {
        query: {
          type: "string",
          description: 'Search text, e.g. "artist:Miles Davis" or "rock -genre:metal"',
        },
        limit: {
          type: "number",
          description: "Max tracks to return (default 50, Hub caps at 500)",
        },
      },
      required: ["query"],
    },
    run: (a) =>
      hub("GET", `/search?q=${encodeURIComponent(a.query)}&limit=${Math.min(Number(a.limit) || 50, 500)}`),
  },

  now_playing: {
    description: "Get metadata for the track currently playing in MusicBee.",
    inputSchema: { type: "object", properties: {} },
    run: () => hub("GET", "/nowplaying"),
  },

  player: {
    description: "Control MusicBee playback: play, pause, stop, next, previous.",
    inputSchema: {
      type: "object",
      properties: {
        action: { type: "string", enum: PLAYER_ACTIONS, description: "Transport action" },
      },
      required: ["action"],
    },
    run: (a) => {
      if (!PLAYER_ACTIONS.includes(a.action)) {
        throw new Error(`Unknown action "${a.action}". Valid: ${PLAYER_ACTIONS.join(", ")}`);
      }
      return hub("POST", `/player/${a.action}`);
    },
  },

  list_playlists: {
    description:
      "List all MusicBee playlists. Each entry has a url (its identifier) and a name — " +
      "pass the url to add_to_playlist or play_playlist.",
    inputSchema: { type: "object", properties: {} },
    run: () => hub("GET", "/playlists"),
  },

  create_playlist: {
    description:
      "Create a new MusicBee playlist from full file paths (as returned by search_library).",
    inputSchema: {
      type: "object",
      properties: {
        name: { type: "string", description: "Playlist name" },
        files: {
          type: "array",
          items: { type: "string" },
          description: "Full file paths of the tracks",
        },
      },
      required: ["name", "files"],
    },
    run: (a) => hub("POST", "/playlists", { name: a.name, files: a.files }),
  },

  add_to_playlist: {
    description: "Add tracks (full file paths) to an existing playlist.",
    inputSchema: {
      type: "object",
      properties: {
        playlistUrl: { type: "string", description: "Playlist url from list_playlists" },
        files: {
          type: "array",
          items: { type: "string" },
          description: "Full file paths to add",
        },
      },
      required: ["playlistUrl", "files"],
    },
    run: (a) => hub("POST", `/playlists/${seg(a.playlistUrl)}/files`, { urls: a.files }),
  },

  play_playlist: {
    description: "Start playing a playlist.",
    inputSchema: {
      type: "object",
      properties: {
        playlistUrl: { type: "string", description: "Playlist url from list_playlists" },
      },
      required: ["playlistUrl"],
    },
    run: (a) => hub("POST", `/playlists/${seg(a.playlistUrl)}/play`),
  },
};

const server = new Server(
  { name: "mbxhub-mcp", version: "0.1.0" },
  { capabilities: { tools: {} } },
);

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: Object.entries(tools).map(([name, t]) => ({
    name,
    description: t.description,
    inputSchema: t.inputSchema,
  })),
}));

server.setRequestHandler(CallToolRequestSchema, async (req) => {
  const tool = tools[req.params.name];
  if (!tool) {
    return {
      content: [{ type: "text", text: `Unknown tool: ${req.params.name}` }],
      isError: true,
    };
  }
  try {
    const text = await tool.run(req.params.arguments ?? {});
    return { content: [{ type: "text", text }] };
  } catch (e) {
    return {
      content: [{ type: "text", text: e instanceof Error ? e.message : String(e) }],
      isError: true,
    };
  }
});

await server.connect(new StdioServerTransport());
log(`ready — hub: ${HUB}`);
