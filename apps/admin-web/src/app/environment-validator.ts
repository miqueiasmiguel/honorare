export interface AppEnvironment {
  production: boolean;
  googleAuthCallbackUrl: string;
}

export function validateEnvironment(env: AppEnvironment): void {
  const missing: string[] = [];

  if (!env.googleAuthCallbackUrl.trim()) {
    missing.push('googleAuthCallbackUrl');
  }

  if (missing.length === 0) {
    return;
  }

  throw new Error(
    `A aplicação não pode iniciar. Variáveis de ambiente obrigatórias ausentes ou inválidas: ${missing.join(', ')}`,
  );
}
