import { defineConfig } from 'vitest/config';

// Note: @angular/build/vite-plugin was removed in @angular/build 20.3.x.
// All components in this project use inline templates (no templateUrl/styleUrl),
// so the Angular Vite plugin is not required — Angular's JIT compiler handles
// decorators at runtime via experimentalDecorators + @angular/compiler.
export default defineConfig({
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['src/test-setup.ts'],
    include: ['src/**/*.spec.ts'],
    reporters: ['default'],
    // Zone.js + v8 coverage crashes in multi-threaded mode on this platform.
    // singleThread keeps all tests in one worker to avoid the IPC channel error.
    poolOptions: { threads: { singleThread: true } },
    coverage: {
      provider: 'v8',
      reporter: ['text-summary', 'json-summary', 'html', 'lcov'],
      reportsDirectory: './coverage',
      include: ['src/**/*.ts'],
      exclude: [
        'src/main.ts',
        'src/test-setup.ts',
        'src/**/*.spec.ts',
        'src/environments/**',
      ],
    },
  },
});
