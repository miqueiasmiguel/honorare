import 'zone.js';
import 'zone.js/testing';
import { getTestBed } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';
import { ɵresolveComponentResources as resolveComponentResources } from '@angular/core';
import { readFileSync, readdirSync } from 'node:fs';
import { basename, dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// Resolve templateUrl/styleUrl resources for components using the file system.
// Required because @angular/build removed the Vite plugin in 20.3.x — there is
// no build-time template compilation pipeline for Vitest. We read HTML templates
// from disk; styles return empty string (not needed for behaviour tests).
// jsdom não implementa URL.createObjectURL/revokeObjectURL. Algumas libs (ex.:
// ngx-image-cropper) os chamam em tempo de avaliação de módulo, antes de qualquer
// beforeAll, então o polyfill precisa existir já no carregamento do setup.
if (typeof URL.createObjectURL !== 'function') {
  URL.createObjectURL = (): string => 'blob:test';
  URL.revokeObjectURL = (): void => undefined;
}

const setupDir = dirname(fileURLToPath(import.meta.url));

function collectHtmlFiles(dir: string, map = new Map<string, string>()): Map<string, string> {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      collectHtmlFiles(full, map);
    } else if (entry.name.endsWith('.html')) {
      map.set(entry.name, readFileSync(full, 'utf-8'));
    }
  }
  return map;
}

const htmlFiles = collectHtmlFiles(resolve(setupDir, 'app'));

beforeAll(async () => {
  await resolveComponentResources((url: string) => {
    const content = url.endsWith('.html') ? (htmlFiles.get(basename(url)) ?? '') : '';
    return Promise.resolve({ text: () => Promise.resolve(content) });
  });
});

getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
