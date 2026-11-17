"use strict";
var assert = require("assert");
var health = require("./components/adobe/js/claude-health.js");
assert.strictEqual(health.classify({ type: "result", subtype: "success", is_error: true, result: "Failed to authenticate. API Error: 401 OAuth access token has been revoked." }, "", 1).code, "auth_revoked");
assert.strictEqual(health.classify({ result: "OAuth session expired and could not be refreshed" }, "", 1).auth, true);
assert.strictEqual(health.classify({ loggedIn: true }, "", 0).ok, false);
assert.strictEqual(health.classify({ type: "result", is_error: false }, "", 0).ok, true);
assert.strictEqual(health.classify(null, "429 rate limit", 1).auth, false);
assert.strictEqual(health.classify(null, "network timeout", 1).auth, false);
console.log("PASS 6 classificações de conexão Claude (JavaScript)");
var platform = require("./components/adobe/js/platform.js");
var path = require("path").win32;
var home = "C:\\fixture\\home", roaming = "C:\\fixture\\roaming", local = "C:\\fixture\\local";
var oldBinary = path.join(home, ".local", "bin", "claude.exe");
var bundleRoot = path.join(roaming, "Claude", "claude-code");
var newBinary = path.join(bundleRoot, "2.1.325", "hash", "claude.exe");
var dirs = {};
dirs[bundleRoot] = ["2.1.325"];
dirs[path.dirname(newBinary)] = ["claude.exe"];
dirs[path.dirname(path.dirname(newBinary))] = ["hash"];
var mockNode = {
  path: path, os: { homedir: function () { return home; } },
  process: { platform: "win32", env: { APPDATA: roaming, LOCALAPPDATA: local, PATH: "" } },
  fs: {
    existsSync: function (file) { return file === oldBinary || file === newBinary || Object.prototype.hasOwnProperty.call(dirs, file); },
    readdirSync: function (file) { return dirs[file] || []; },
    statSync: function (file) { return { isFile: function () { return file === oldBinary || file === newBinary; }, isDirectory: function () { return Object.prototype.hasOwnProperty.call(dirs, file); } }; }
  },
  childProcess: { execFileSync: function (file) { return file === newBinary ? "2.1.325 (Claude Code)" : "2.1.158 (Claude Code)"; } }
};
assert.strictEqual(platform.findClaudeBinary(mockNode), newBinary);
mockNode.process.env.CLAUDE_BIN = oldBinary;
assert.strictEqual(platform.findClaudeBinary(mockNode), oldBinary);
console.log("PASS 2 cenários de seleção de binário Claude (JavaScript)");
