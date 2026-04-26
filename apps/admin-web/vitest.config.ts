import { defineConfig } from 'vitest/config';
import angular from '@angular/build/vite-plugin';

export default defineConfig({
  plugins: [
    // Angular's official Vite plugin — ships with @angular/build (already a devDep)
    angular({ tsconfig: './tsconfig.spec.json' }),
  ],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['src/test-setup.ts'],
    include: ['src/**/*.spec.ts'],
    reporters: ['default'],
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
