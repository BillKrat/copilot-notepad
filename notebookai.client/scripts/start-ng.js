#!/usr/bin/env node
/* Starts Angular dev server on the port provided by Aspire (process.env.PORT) with sensible defaults. */
const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');

const port =
  process.env.PORT ||
  process.env.SSL_PORT ||
  process.env.HTTP_PORT ||
  '4200';

const useHttps =
  String(process.env.HTTPS || process.env.SSL || 'false').toLowerCase() === 'true';

const proxyCfg = path.resolve(process.cwd(), 'proxy.conf.json');
const hasProxy = fs.existsSync(proxyCfg);

const npx = process.platform === 'win32' ? 'npx.cmd' : 'npx';
const args = [
  '--no-install',
  'ng',
  'serve',
  '--host',
  'localhost',
  '--port',
  String(port),
  '--open',
  'false'
];

if (useHttps) {
  args.push('--ssl', 'true');
}

if (hasProxy) {
  args.push('--proxy-config', 'proxy.conf.json');
}

console.log('[start-ng] Starting Angular dev server...');
console.log(`[start-ng] PORT=${port} HTTPS=${useHttps} Proxy=${hasProxy ? 'on' : 'off'}`);

const child = spawn(npx, args, { stdio: 'inherit', shell: true });

child.on('exit', code => process.exit(code ?? 0));
child.on('error', err => {
  console.error('[start-ng] Failed to start Angular CLI:', err);
  process.exit(1);
});
