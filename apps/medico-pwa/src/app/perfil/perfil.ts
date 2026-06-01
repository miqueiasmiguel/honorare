import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-perfil',
  imports: [],
  template: `
    <div class="perfil">
      <div class="perfil__card">
        <span class="perfil__label">E-mail</span>
        <span class="perfil__value">{{ userEmail() ?? '—' }}</span>
      </div>
      <button class="perfil__logout" type="button" (click)="logout()">Sair da conta</button>
    </div>
  `,
  styles: [
    `
      .perfil {
        padding: 24px 16px;
        display: flex;
        flex-direction: column;
        gap: 24px;
      }

      .perfil__card {
        display: flex;
        flex-direction: column;
        gap: 4px;
        padding: 16px;
        border: 1px solid var(--color-borda-discreta);
        border-radius: 8px;
        background-color: var(--color-pergaminho-claro);
      }

      .perfil__label {
        font-family: var(--font-sans);
        font-size: 12px;
        color: var(--color-tinta-terciaria);
        text-transform: uppercase;
        letter-spacing: 0.04em;
      }

      .perfil__value {
        font-family: var(--font-sans);
        font-size: 15px;
        color: var(--color-tinta);
      }

      .perfil__logout {
        padding: 12px 16px;
        border: 1px solid var(--color-ferrugem);
        border-radius: 8px;
        background: none;
        color: var(--color-ferrugem);
        font-family: var(--font-sans);
        font-size: 15px;
        font-weight: 500;
        cursor: pointer;
        align-self: stretch;
        transition: background-color var(--duration-fast) var(--easing-default);
      }

      .perfil__logout:hover {
        background-color: var(--color-ferrugem-claro);
      }
    `,
  ],
})
export class Perfil {
  private readonly _auth = inject(AuthService);
  private readonly _router = inject(Router);

  readonly userEmail = this._auth.userEmail;

  logout(): void {
    this._auth.logout();
    void this._router.navigate(['/auth/login']);
  }
}
