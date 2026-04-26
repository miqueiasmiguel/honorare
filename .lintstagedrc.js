// lint-staged runs from the repo root. Function-returning rules prevent file
// paths from being appended to workspace commands (which don't accept them).
export default {
  'apps/admin-web/src/**/*.{ts,html}': () => 'pnpm -F admin-web lint',
  'apps/admin-web/src/**/*.scss': () => 'pnpm -F admin-web stylelint',
  'apps/admin-web/src/**/*.{ts,html,scss,json}': () =>
    'pnpm -F admin-web prettier:fix',

  'apps/medico-pwa/src/**/*.{ts,html}': () => 'pnpm -F medico-pwa lint',
  'apps/medico-pwa/src/**/*.scss': () => 'pnpm -F medico-pwa stylelint',
  'apps/medico-pwa/src/**/*.{ts,html,scss,json}': () =>
    'pnpm -F medico-pwa prettier:fix',

  // Root-level files (CI, infra, docs)
  '*.{json,yml,yaml,md}': 'prettier --ignore-unknown --write',
};
