import { validateEnvironment, type AppEnvironment } from './environment-validator';

describe('validateEnvironment', () => {
  const validEnv: AppEnvironment = {
    production: false,
    googleAuthCallbackUrl: 'http://localhost:4201/app/auth/callback',
  };

  it('does not throw when all required values are present', () => {
    expect(() => {
      validateEnvironment(validEnv);
    }).not.toThrow();
  });

  it('does not throw in production mode with valid values', () => {
    const prodEnv: AppEnvironment = {
      production: true,
      googleAuthCallbackUrl: '/app/auth/callback',
    };
    expect(() => {
      validateEnvironment(prodEnv);
    }).not.toThrow();
  });

  it('throws when googleAuthCallbackUrl is empty string', () => {
    expect(() => {
      validateEnvironment({ ...validEnv, googleAuthCallbackUrl: '' });
    }).toThrow(/googleAuthCallbackUrl/);
  });

  it('throws when googleAuthCallbackUrl is whitespace only', () => {
    expect(() => {
      validateEnvironment({ ...validEnv, googleAuthCallbackUrl: '   ' });
    }).toThrow(/googleAuthCallbackUrl/);
  });

  it('error message mentions all missing keys', () => {
    expect(() => {
      validateEnvironment({ ...validEnv, googleAuthCallbackUrl: '' });
    }).toThrow(/Variáveis de ambiente obrigatórias/);
  });
});
