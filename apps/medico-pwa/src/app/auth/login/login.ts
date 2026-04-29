import { Component } from '@angular/core';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login',
  template: `
    <div class="login-container">
      <h1>Honorare</h1>
      <p>Portal do Médico</p>
      <button type="button" (click)="navigateToGoogle()">Entrar com Google</button>
    </div>
  `,
  styles: [
    `
      .login-container {
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        min-height: 100vh;
        gap: 1rem;
      }
      button {
        padding: 0.75rem 1.5rem;
        font-size: 1rem;
        cursor: pointer;
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
