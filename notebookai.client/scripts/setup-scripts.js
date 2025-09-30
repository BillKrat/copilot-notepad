#!/usr/bin/env node
/* Merges npm scripts into package.json without clobbering existing fields. Run once: node scripts/setup-scripts.js */
const fs = require('fs');
const path = require('path');

const pkgPath = path.resolve(process.cwd(), 'package.json');
if (!fs.existsSync(pkgPath)) {
  console.error('package.json not found. Run this from the notebookai.client folder.');
  process.exit(1);
}

const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
pkg.scripts = pkg.scripts || {};

const scriptsToAdd = {
  // Dev workflow
  prestart: 'npm run env:dev',
  start: 'node ./scripts/start-ng.js',

  // Environment switching (prefers .bat, falls back to PowerShell)
  'env:dev': 'switch-env.bat dev || powershell -NoProfile -ExecutionPolicy Bypass -File ./switch-env.ps1 -Environment dev',
  'env:prod': 'switch-env.bat prod || powershell -NoProfile -ExecutionPolicy Bypass -File ./switch-env.ps1 -Environment prod',

  // Builds
  build: 'ng build',
  'build:prod': 'ng build --configuration production',

  // Convenience pipelines from your docs
  'deploy:dev': 'npm run env:dev && npm run build',
  'deploy:prod': 'npm run env:prod && npm run build:prod',
  'package:dev': 'npm run deploy:dev',
  'package:prod': 'npm run deploy:prod'
};

// Merge without removing existing custom scripts
for (const [k, v] of Object.entries(scriptsToAdd)) {
  pkg.scripts[k] = v;
}

fs.writeFileSync(pkgPath, JSON.stringify(pkg, null, 2));
console.log('Updated scripts in package.json.');
