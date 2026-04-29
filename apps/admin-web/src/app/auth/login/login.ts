import { Component } from '@angular/core';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login',
  template: `
    <div class="login-page">
      <div class="login-card">
        <div class="login-card__header">
          <span class="login-card__wordmark">honorare</span>
          <p class="login-card__tagline">Conciliação de pagamentos médicos</p>
        </div>

        <div class="login-card__body">
          <button type="button" class="btn-google" (click)="navigateToGoogle()">
            <svg
              class="btn-google__icon"
              viewBox="0 0 24 24"
              aria-hidden="true"
              width="18"
              height="18"
            >
              <path
                fill="#4285F4"
                d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
              />
              <path
                fill="#34A853"
                d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
              />
              <path
                fill="#FBBC05"
                d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l3.66-2.84z"
              />
              <path
                fill="#EA4335"
                d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"
              />
            </svg>
            Entrar com Google
          </button>
        </div>

        <p class="login-card__footer">Acesso restrito a usuários autorizados.</p>
      </div>
    </div>
  `,
  styles: [
    `
      .login-page {
        display: flex;
        align-items: center;
        justify-content: center;
        min-height: 100vh;
        padding: 24px;
        background-color: var(--color-pergaminho);
      }

      .login-card {
        width: 100%;
        max-width: 400px;
        background-color: var(--color-pergaminho-claro);
        border: 1px solid var(--color-borda-discreta);
        border-radius: 8px;
        padding: 48px 40px 32px;
        display: flex;
        flex-direction: column;
        gap: 32px;
      }

      .login-card__header {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }

      .login-card__wordmark {
        font-family: var(--font-mono);
        font-size: 28px;
        font-weight: 500;
        color: var(--color-terracota);
        letter-spacing: -0.01em;
        line-height: 1;
      }

      .login-card__tagline {
        margin: 0;
        font-size: 14px;
        color: var(--color-tinta-secundaria);
        line-height: 1.5;
      }

      .login-card__body {
        display: flex;
        flex-direction: column;
      }

      .btn-google {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 10px;
        height: 40px;
        padding: 0 20px;
        border-radius: 6px;
        border: 1px solid var(--color-borda-media);
        background-color: var(--color-pergaminho-claro);
        color: var(--color-tinta);
        font-family: var(--font-sans);
        font-size: 14px;
        font-weight: 500;
        cursor: pointer;
        transition:
          background-color 150ms ease-out,
          border-color 150ms ease-out;
        width: 100%;
      }

      .btn-google:hover {
        background-color: var(--color-pergaminho);
        border-color: var(--color-tinta-terciaria);
      }

      .btn-google:active {
        background-color: var(--color-borda-discreta);
      }

      .btn-google__icon {
        flex-shrink: 0;
      }

      .login-card__footer {
        margin: 0;
        font-size: 12px;
        color: var(--color-tinta-terciaria);
        text-align: center;
        line-height: 1.5;
      }
    `,
  ],
})
export class Login {
  readonly _callbackUrl = environment.googleAuthCallbackUrl;

  navigateToGoogle(): void {
    const returnUrl = encodeURIComponent(this._callbackUrl);
    window.location.href = `/api/v1/auth/google?returnUrl=${returnUrl}`;
  }
}
